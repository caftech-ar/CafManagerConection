namespace CafManagerConection.Ssh;

/// <summary>Le pide al usuario la contraseña de <c>sudo</c> cuando la de la conexión no sirve (FR-184e). Escribe en <c>destino</c> en lugar de devolver un <c>string</c>: una cadena queda en el montón hasta que el recolector la levante y no se puede pisar con ceros.</summary>
public interface IPedidoDeContrasenaDeSudo
{
    /// <returns><c>false</c> cuando el usuario cancela, que deja la escalada imposible.</returns>
    Task<bool> PedirAsync(
        string servidor,
        string usuario,
        ContrasenaDeSudoDeSesion destino,
        CancellationToken ct = default);
}
