namespace CafManagerConection.Domain.Credentials;

/// <summary>Una credencial tal como vive en la base: el usuario y el dominio en claro, el secreto en su sobre.</summary>
public sealed record CredencialCifrada(
    string Clave, string Usuario, string? Dominio, SobreCifrado Sobre)
{
    public override string ToString() => $"CredencialCifrada({Clave})";
}
