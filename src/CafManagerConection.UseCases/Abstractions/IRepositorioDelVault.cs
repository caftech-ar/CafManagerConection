using CafManagerConection.Domain.Credentials;

namespace CafManagerConection.UseCases.Abstractions;

/// <summary>Los secretos entran y salen de acá tal como están en su fila: este repositorio no cifra ni descifra nada.</summary>
public interface IRepositorioDelVault
{
    /// <summary><c>null</c> cuando no hay clave maestra: es lo que significa que los secretos están en claro.</summary>
    Task<FilaDelVault?> LeerAsync(CancellationToken ct = default);

    Task GuardarAsync(FilaDelVault fila, CancellationToken ct = default);

    /// <summary>Quita la clave maestra. Los secretos no se tocan acá: los recifra quien llama, en la misma transacción.</summary>
    Task BorrarAsync(CancellationToken ct = default);

    /// <summary><c>null</c> cuando esa fila no tiene secreto guardado.</summary>
    Task<SecretoGuardado?> LeerSecretoAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default);

    Task GuardarSecretoAsync(
        ReferenciaDeSecreto referencia, SecretoGuardado secreto, CancellationToken ct = default);

    Task BorrarSecretoAsync(ReferenciaDeSecreto referencia, CancellationToken ct = default);

    /// <summary>Se puede contestar con el vault cerrado: saber que hay un secreto no es leerlo.</summary>
    Task<bool> HaySecretoAsync(ReferenciaDeSecreto referencia, CancellationToken ct = default);

    /// <summary>Dónde hay secreto guardado, sin traer ninguno. También se contesta con el vault cerrado.</summary>
    Task<IReadOnlyList<ReferenciaDeSecreto>> ReferenciasConSecretoAsync(
        CancellationToken ct = default);

    /// <summary>Todos los secretos con su referencia. Sólo la usa el recifrado masivo al poner, cambiar o quitar la clave maestra.</summary>
    Task<IReadOnlyList<(ReferenciaDeSecreto Referencia, SecretoGuardado Secreto)>>
        TodosLosSecretosAsync(CancellationToken ct = default);

    /// <summary>Reemplaza la fila de la clave maestra y todos los secretos de una sola vez. Es lo que impide que una interrupción deje unos secretos legibles y otros no.</summary>
    Task ReemplazarTodoAsync(
        FilaDelVault? fila,
        IReadOnlyList<(ReferenciaDeSecreto Referencia, SecretoGuardado Secreto)> secretos,
        CancellationToken ct = default);
}
