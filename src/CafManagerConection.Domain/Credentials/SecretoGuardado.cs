namespace CafManagerConection.Domain.Credentials;

/// <summary>Un secreto tal como está en su fila: en claro cuando no hay nonce, cifrado cuando lo hay. El nonce no es secreto y va al lado.</summary>
public sealed record SecretoGuardado(byte[] Bytes, byte[]? Nonce)
{
    public bool EstaCifrado => Nonce is not null;

    public SobreCifrado Sobre => new(
        Nonce ?? throw new InvalidOperationException("Este secreto está en claro: no tiene sobre."),
        Bytes);

    public static SecretoGuardado Cifrado(SobreCifrado sobre) =>
        new(sobre.Cifrado, sobre.Nonce);

    public override string ToString() =>
        EstaCifrado ? $"SecretoGuardado(cifrado, {Bytes.Length} bytes)" : "SecretoGuardado(en claro)";
}
