namespace CafManagerConection.Domain.Credentials;

/// <summary>Lo que la base guarda del vault. Todo en claro salvo las dos envolturas de la clave: una sal y un número de iteraciones no son secretos, y sin ellos el vault no se abre nunca más.</summary>
public sealed record FilaDelVault(
    int Formato,
    byte[]? ClaveDpapi,
    byte[]? KdfSal,
    int? KdfIteraciones,
    byte[]? ClaveMaestraNonce,
    byte[]? ClaveMaestraEnvuelta)
{
    public const int FormatoActual = 1;

    /// <summary>Hay clave maestra configurada. Es opcional a propósito.</summary>
    public bool PideClaveMaestra =>
        KdfSal is { Length: > 0 }
        && KdfIteraciones is > 0
        && ClaveMaestraNonce is { Length: > 0 }
        && ClaveMaestraEnvuelta is { Length: > 0 };

    /// <summary>Se puede abrir sin preguntar nada, porque este equipo y este usuario están recordados.</summary>
    public bool AbreSola => ClaveDpapi is { Length: > 0 };

    /// <summary>La única forma de tener un vault que no se puede abrir por ningún camino. Es un defecto, no un estado.</summary>
    public bool EstaHuerfano => !PideClaveMaestra && !AbreSola;

    public SobreCifrado? SobreDeLaClaveMaestra =>
        PideClaveMaestra ? new SobreCifrado(ClaveMaestraNonce!, ClaveMaestraEnvuelta!) : null;
}
