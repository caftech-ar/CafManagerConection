using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CafManagerConection.Infrastructure.Credentials;

/// <summary>DPAPI del usuario actual por P/Invoke. El tipo <c>ProtectedData</c> no está en .NET 10: pedirlo exigiría un paquete, y esto son treinta líneas con el mismo patrón que <see cref="CredentialManagerNative"/>.</summary>
public static partial class ProteccionDpapi
{
    private const uint AlUsuarioActual = 0;

    public static byte[] Proteger(ReadOnlySpan<byte> claro) => Llamar(claro, proteger: true);

    /// <summary>Lanza <see cref="CryptographicException"/> cuando el blob es de otro usuario, de otra máquina o está tocado. Ese fallo es un camino normal, no un error del programa.</summary>
    public static byte[] Desproteger(ReadOnlySpan<byte> envuelto) =>
        Llamar(envuelto, proteger: false);

    private static byte[] Llamar(ReadOnlySpan<byte> entrada, bool proteger)
    {
        var copia = entrada.ToArray();
        var dentro = default(Blob);
        var fuera = default(Blob);
        var vacio = default(Blob);

        try
        {
            dentro.cbData = copia.Length;
            dentro.pbData = Marshal.AllocHGlobal(copia.Length);
            Marshal.Copy(copia, 0, dentro.pbData, copia.Length);

            var ok = proteger
                ? CryptProtectData(ref dentro, null, ref vacio, 0, 0, AlUsuarioActual, out fuera)
                : CryptUnprotectData(ref dentro, 0, ref vacio, 0, 0, AlUsuarioActual, out fuera);

            if (!ok)
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            var salida = new byte[fuera.cbData];
            Marshal.Copy(fuera.pbData, salida, 0, fuera.cbData);
            return salida;
        }
        finally
        {
            if (dentro.pbData != 0)
            {
                // La clave del vault estuvo en memoria no administrada: la pisa antes de liberar.
                Marshal.Copy(new byte[copia.Length], 0, dentro.pbData, copia.Length);
                Marshal.FreeHGlobal(dentro.pbData);
            }

            if (fuera.pbData != 0)
            {
                LocalFree(fuera.pbData);
            }

            CryptographicOperations.ZeroMemory(copia);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public int cbData;
        public nint pbData;
    }

    [LibraryImport("crypt32.dll", EntryPoint = "CryptProtectData", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptProtectData(
        ref Blob entrada,
        string? descripcion,
        ref Blob entropia,
        nint reservado,
        nint pedido,
        uint banderas,
        out Blob salida);

    [LibraryImport("crypt32.dll", EntryPoint = "CryptUnprotectData", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptUnprotectData(
        ref Blob entrada,
        nint descripcion,
        ref Blob entropia,
        nint reservado,
        nint pedido,
        uint banderas,
        out Blob salida);

    [LibraryImport("kernel32.dll", EntryPoint = "LocalFree")]
    private static partial nint LocalFree(nint memoria);
}
