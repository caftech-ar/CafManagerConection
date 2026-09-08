using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Credentials;

/// <summary>Dónde vive un secreto: en la fila de una conexión, o en la de los ajustes de una carpeta. Nunca es el secreto.</summary>
public readonly record struct ReferenciaDeSecreto
{
    private ReferenciaDeSecreto(Guid id, bool esDeCarpeta, Protocol protocolo)
    {
        Id = id;
        EsDeCarpeta = esDeCarpeta;
        Protocolo = protocolo;
    }

    public Guid Id { get; }

    public bool EsDeCarpeta { get; }

    /// <summary>Una conexión tiene un solo secreto y su protocolo ya lo fija; en una carpeta elige cuál de los tres.</summary>
    public Protocol Protocolo { get; }

    public static ReferenciaDeSecreto DeConexion(Guid conexion, Protocol protocolo) =>
        new(conexion, esDeCarpeta: false, protocolo);

    public static ReferenciaDeSecreto DeCarpeta(Guid carpeta, Protocol protocolo) =>
        new(carpeta, esDeCarpeta: true, protocolo);

    public override string ToString() =>
        EsDeCarpeta ? $"carpeta {Id:D} · {Protocolo}" : $"conexión {Id:D}";
}
