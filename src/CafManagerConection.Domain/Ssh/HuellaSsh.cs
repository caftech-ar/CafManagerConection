using System.Security.Cryptography;

namespace CafManagerConection.Domain.Ssh;

/// <summary>Igual que <c>ssh-keygen -lf</c>: SHA-256 del blob binario en base64 sin relleno.</summary>
public static class HuellaSsh
{
    public static string CalcularSha256(ReadOnlySpan<byte> blobPublico)
    {
        var hash = SHA256.HashData(blobPublico);
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
    }
}
