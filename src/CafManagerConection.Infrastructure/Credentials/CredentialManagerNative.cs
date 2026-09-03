using System.Runtime.InteropServices;

namespace CafManagerConection.Infrastructure.Credentials;

/// <summary>P/Invoke a la API de credenciales de Windows (<c>advapi32</c>). El tope de 2560 bytes del blob impide guardar acá el contenido de una clave privada.</summary>
internal static partial class CredentialManagerNative
{
    internal const int CRED_TYPE_GENERIC = 1;
    internal const int CRED_PERSIST_LOCAL_MACHINE = 2;

    internal const int MaxCredentialBlobSize = 2560;

    internal const int ERROR_NOT_FOUND = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct CREDENTIALW
    {
        public int Flags;
        public int Type;
        public nint TargetName;
        public nint Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public nint CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public nint Attributes;
        public nint TargetAlias;
        public nint UserName;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredWrite(ref CREDENTIALW credential, int flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredRead(string target, int type, int reservedFlag, out nint credentialPtr);

    [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredDelete(string target, int type, int reservedFlag);

    [LibraryImport("advapi32.dll", EntryPoint = "CredFree")]
    internal static partial void CredFree(nint buffer);

    // El arreglo devuelto se libera con CredFree sobre el arreglo y no sobre cada elemento: son parte del mismo bloque. Del blob del secreto no se lee nada.
    [LibraryImport("advapi32.dll", EntryPoint = "CredEnumerateW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredEnumerate(
        string? filter, int flags, out int count, out nint credentialsPtr);
}
