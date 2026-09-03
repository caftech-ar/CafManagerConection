using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Credentials;

/// <summary>El almacén de secretos, contra el vault cifrado. Reemplaza a <c>WindowsCredentialStore</c>, que queda sólo como origen de la migración.</summary>
public sealed class VaultCredentialStore : ICredentialStore
{
    private readonly Vault _vault;
    private readonly IRepositorioDelVault _repositorio;

    public VaultCredentialStore(Vault vault, IRepositorioDelVault repositorio)
    {
        _vault = vault;
        _repositorio = repositorio;
    }

    /// <summary>Lanza <see cref="VaultCerradoException"/> con el vault cerrado, y NO devuelve <c>null</c>: <c>null</c> significa «no hay credencial guardada» y haría que la aplicación ofreciera guardarla de nuevo.</summary>
    public async Task<StoredCredential?> ReadAsync(
        string credentialKey, CancellationToken ct = default)
    {
        var leida = await _vault.LeerCredencialAsync(credentialKey, ct).ConfigureAwait(false);

        if (leida is not { } c)
        {
            return null;
        }

        try
        {
            return new StoredCredential(c.Usuario, c.Dominio, c.Secreto);
        }
        finally
        {
            Array.Clear(c.Secreto);
        }
    }

    public Task WriteAsync(
        string credentialKey, StoredCredential credential, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return _vault.GuardarCredencialAsync(
            credentialKey,
            credential.UserName,
            credential.Domain,
            credential.Secret.ToArray(),
            ct);
    }

    public Task DeleteAsync(string credentialKey, CancellationToken ct = default) =>
        _repositorio.BorrarCredencialAsync(credentialKey, ct);

    /// <summary>Se contesta con el vault cerrado: saber que hay una credencial guardada no es leerla, y el árbol tiene que poder mostrarse igual.</summary>
    public Task<bool> ExistsAsync(string credentialKey, CancellationToken ct = default) =>
        _repositorio.ExisteCredencialAsync(credentialKey, ct);

    /// <summary>También se contesta con el vault cerrado, por el mismo motivo.</summary>
    public Task<IReadOnlyList<string>> EnumerateKeysAsync(
        string prefix, CancellationToken ct = default) =>
        _repositorio.ClavesAsync(prefix, ct);
}
