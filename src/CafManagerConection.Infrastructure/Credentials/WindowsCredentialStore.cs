using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Credentials;

/// <summary>Único lugar donde puede vivir un secreto (Principio II); la base local sólo guarda la clave con la que se lo busca.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    public Task<StoredCredential?> ReadAsync(string credentialKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialKey);

        if (!CredentialManagerNative.CredRead(
                credentialKey, CredentialManagerNative.CRED_TYPE_GENERIC, 0, out var ptr))
        {
            var error = Marshal.GetLastWin32Error();

            if (error == CredentialManagerNative.ERROR_NOT_FOUND)
            {
                return Task.FromResult<StoredCredential?>(null);
            }

            throw new Win32Exception(error, $"No se pudo leer la credencial '{credentialKey}'.");
        }

        try
        {
            var cred = Marshal.PtrToStructure<CredentialManagerNative.CREDENTIALW>(ptr);

            var userName = cred.UserName == nint.Zero
                ? string.Empty
                : Marshal.PtrToStringUni(cred.UserName) ?? string.Empty;

            var comment = cred.Comment == nint.Zero
                ? null
                : Marshal.PtrToStringUni(cred.Comment);

            var secret = ReadSecret(cred);

            try
            {
                var (domain, user) = SplitDomain(userName, comment);
                return Task.FromResult<StoredCredential?>(new StoredCredential(user, domain, secret));
            }
            finally
            {
                Array.Clear(secret);
            }
        }
        finally
        {
            CredentialManagerNative.CredFree(ptr);
        }
    }

    public Task WriteAsync(
        string credentialKey, StoredCredential credential, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialKey);
        ArgumentNullException.ThrowIfNull(credential);

        var secretBytes = System.Text.Encoding.Unicode.GetBytes(credential.Secret.ToArray());

        if (secretBytes.Length > CredentialManagerNative.MaxCredentialBlobSize)
        {
            Array.Clear(secretBytes);
            throw new ArgumentException(
                $"El secreto supera el máximo de {CredentialManagerNative.MaxCredentialBlobSize} " +
                "bytes que admite Windows. Las claves privadas se referencian por ruta, no se " +
                "guardan acá.",
                nameof(credential));
        }

        var targetPtr = Marshal.StringToCoTaskMemUni(credentialKey);
        var userPtr = Marshal.StringToCoTaskMemUni(
            string.IsNullOrEmpty(credential.Domain)
                ? credential.UserName
                : $"{credential.Domain}\\{credential.UserName}");
        var blobPtr = Marshal.AllocCoTaskMem(secretBytes.Length);

        try
        {
            Marshal.Copy(secretBytes, 0, blobPtr, secretBytes.Length);

            var cred = new CredentialManagerNative.CREDENTIALW
            {
                Type = CredentialManagerNative.CRED_TYPE_GENERIC,
                TargetName = targetPtr,
                CredentialBlobSize = secretBytes.Length,
                CredentialBlob = blobPtr,
                Persist = CredentialManagerNative.CRED_PERSIST_LOCAL_MACHINE,
                UserName = userPtr,
            };

            if (!CredentialManagerNative.CredWrite(ref cred, 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"No se pudo guardar la credencial '{credentialKey}'.");
            }

            return Task.CompletedTask;
        }
        finally
        {
            // Se limpia el búfer nativo antes de liberarlo: no se deja el secreto en memoria reutilizable.
            for (var i = 0; i < secretBytes.Length; i++)
            {
                Marshal.WriteByte(blobPtr, i, 0);
            }

            Array.Clear(secretBytes);
            Marshal.FreeCoTaskMem(blobPtr);
            Marshal.FreeCoTaskMem(targetPtr);
            Marshal.FreeCoTaskMem(userPtr);
        }
    }

    /// <summary>Borrar una clave inexistente es una operación exitosa, no un fallo.</summary>
    public Task DeleteAsync(string credentialKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialKey);

        if (!CredentialManagerNative.CredDelete(
                credentialKey, CredentialManagerNative.CRED_TYPE_GENERIC, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != CredentialManagerNative.ERROR_NOT_FOUND)
            {
                throw new Win32Exception(
                    error, $"No se pudo borrar la credencial '{credentialKey}'.");
            }
        }

        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string credentialKey, CancellationToken ct = default)
    {
        using var credential = await ReadAsync(credentialKey, ct).ConfigureAwait(false);
        return credential is not null;
    }

    /// <summary>Las claves guardadas que empiezan con el prefijo (FR-158). El filtro lo aplica <c>CredEnumerateW</c>: enumerar todo traería las credenciales de los demás programas.</summary>
    public Task<IReadOnlyList<string>> EnumerateKeysAsync(
        string prefix, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var claves = new List<string>();
        var puntero = nint.Zero;

        try
        {
            if (!CredentialManagerNative.CredEnumerate(
                    prefix + "*", 0, out var cuantas, out puntero))
            {
                var error = Marshal.GetLastWin32Error();

                return error == CredentialManagerNative.ERROR_NOT_FOUND
                    ? Task.FromResult<IReadOnlyList<string>>([])
                    : throw new System.ComponentModel.Win32Exception(error);
            }

            for (var i = 0; i < cuantas; i++)
            {
                var entrada = Marshal.ReadIntPtr(puntero, i * nint.Size);

                var cred = Marshal.PtrToStructure<CredentialManagerNative.CREDENTIALW>(entrada);

                if (Marshal.PtrToStringUni(cred.TargetName) is { Length: > 0 } nombre)
                {
                    claves.Add(nombre);
                }
            }
        }
        finally
        {
            // Se libera el arreglo y no cada elemento: son parte del mismo bloque y liberarlos por separado corrompe el montón.
            if (puntero != nint.Zero)
            {
                CredentialManagerNative.CredFree(puntero);
            }
        }

        claves.Sort(StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyList<string>>(claves);
    }

    private static char[] ReadSecret(CredentialManagerNative.CREDENTIALW cred)
    {
        if (cred.CredentialBlob == nint.Zero || cred.CredentialBlobSize <= 0)
        {
            return [];
        }

        var bytes = new byte[cred.CredentialBlobSize];
        Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);

        try
        {
            return System.Text.Encoding.Unicode.GetChars(bytes);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static (string? Domain, string User) SplitDomain(string userName, string? comment)
    {
        var slash = userName.IndexOf('\\', StringComparison.Ordinal);

        return slash >= 0
            ? (userName[..slash], userName[(slash + 1)..])
            : (comment, userName);
    }
}
