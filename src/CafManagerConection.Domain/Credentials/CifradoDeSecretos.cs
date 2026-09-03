using System.Security.Cryptography;
using System.Text;

namespace CafManagerConection.Domain.Credentials;

/// <summary>AES-256-GCM y PBKDF2, los dos del BCL. Es el único lugar del proyecto que cifra.</summary>
public static class CifradoDeSecretos
{
    public const int LargoDeLaClave = 32;

    public const int LargoDeLaSal = 16;

    public const int IteracionesPorOmision = 600_000;

    public static readonly HashAlgorithmName Hash = HashAlgorithmName.SHA512;

    public static byte[] ClaveNueva()
    {
        var clave = new byte[LargoDeLaClave];
        RandomNumberGenerator.Fill(clave);
        return clave;
    }

    public static byte[] SalNueva()
    {
        var sal = new byte[LargoDeLaSal];
        RandomNumberGenerator.Fill(sal);
        return sal;
    }

    /// <summary>Nunca la sobrecarga que toma <c>string</c>: una clave maestra en un <c>string</c> queda en el montón sin poder pisarse.</summary>
    public static byte[] Derivar(ReadOnlySpan<char> claveMaestra, ReadOnlySpan<byte> sal, int iteraciones)
    {
        var derivada = new byte[LargoDeLaClave];
        Rfc2898DeriveBytes.Pbkdf2(claveMaestra, sal, derivada, iteraciones, Hash);
        return derivada;
    }

    public static SobreCifrado Cifrar(ReadOnlySpan<byte> clave, ReadOnlySpan<byte> claro)
    {
        var nonce = new byte[SobreCifrado.LargoDelNonce];
        RandomNumberGenerator.Fill(nonce);

        var cifrado = new byte[claro.Length + SobreCifrado.LargoDeLaEtiqueta];

        using var aes = new AesGcm(clave, SobreCifrado.LargoDeLaEtiqueta);

        aes.Encrypt(
            nonce,
            claro,
            cifrado.AsSpan(0, claro.Length),
            cifrado.AsSpan(claro.Length));

        return new SobreCifrado(nonce, cifrado);
    }

    /// <summary>Lanza <see cref="CryptographicException"/> si la clave es otra o el texto está tocado. No devuelve vacío.</summary>
    public static byte[] Descifrar(ReadOnlySpan<byte> clave, SobreCifrado sobre)
    {
        if (!sobre.EsValido)
        {
            throw new CryptographicException("El sobre cifrado no tiene la forma esperada.");
        }

        var largo = sobre.Cifrado.Length - SobreCifrado.LargoDeLaEtiqueta;
        var claro = new byte[largo];

        using var aes = new AesGcm(clave, SobreCifrado.LargoDeLaEtiqueta);

        aes.Decrypt(
            sobre.Nonce,
            sobre.Cifrado.AsSpan(0, largo),
            sobre.Cifrado.AsSpan(largo),
            claro);

        return claro;
    }

    public static SobreCifrado CifrarTexto(ReadOnlySpan<byte> clave, ReadOnlySpan<char> texto)
    {
        var bytes = new byte[Encoding.UTF8.GetByteCount(texto)];

        try
        {
            Encoding.UTF8.GetBytes(texto, bytes);
            return Cifrar(clave, bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Devuelve <c>char[]</c> y no <c>string</c> para que el llamador pueda pisarlo.</summary>
    public static char[] DescifrarTexto(ReadOnlySpan<byte> clave, SobreCifrado sobre)
    {
        var bytes = Descifrar(clave, sobre);

        try
        {
            var letras = new char[Encoding.UTF8.GetCharCount(bytes)];
            Encoding.UTF8.GetChars(bytes, letras);
            return letras;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
