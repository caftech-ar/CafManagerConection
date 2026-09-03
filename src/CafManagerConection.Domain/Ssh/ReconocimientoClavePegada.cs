namespace CafManagerConection.Domain.Ssh;

public sealed record HuellaClavePublica(string Sha256, string LineaPublica);

/// <summary>Resultado de interpretar un texto pegado donde se esperaba una clave privada.</summary>
public sealed record ReconocimientoClavePegada
{
    public required FormatoClavePegada Formato { get; init; }

    /// <summary><c>null</c> cuando el formato no lo informa o no se pudo determinar.</summary>
    public bool? Cifrada { get; init; }

    /// <summary>Nombre OpenSSH del algoritmo (<c>ssh-ed25519</c>) o el corto del PEM (<c>EC</c>).</summary>
    public string? Algoritmo { get; init; }

    public string? Comentario { get; init; }

    public HuellaClavePublica? Huella { get; init; }

    public string? NotaHuella { get; init; }

    /// <summary>Por qué no se reconoció el texto; sólo con <see cref="FormatoClavePegada.Desconocido"/>.</summary>
    public string? Motivo { get; init; }

    public bool EsReconocida => Formato != FormatoClavePegada.Desconocido;
}
