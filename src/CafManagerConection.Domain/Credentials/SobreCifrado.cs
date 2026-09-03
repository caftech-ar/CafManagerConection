namespace CafManagerConection.Domain.Credentials;

/// <summary>Un secreto cifrado con el nonce que le tocó. El nonce no es secreto y va al lado.</summary>
public sealed record SobreCifrado(byte[] Nonce, byte[] Cifrado)
{
    public const int LargoDelNonce = 12;

    public const int LargoDeLaEtiqueta = 16;

    public bool EsValido =>
        Nonce.Length == LargoDelNonce && Cifrado.Length > LargoDeLaEtiqueta;

    public override string ToString() => $"SobreCifrado({Cifrado.Length} bytes)";
}
