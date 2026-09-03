using CafManagerConection.Domain.Credentials;

namespace CafManagerConection.UseCases.Abstractions;

/// <summary>El secreto que entra y sale de acá ya está cifrado: este repositorio no descifra nada.</summary>
public interface IRepositorioDelVault
{
    /// <summary><c>null</c> cuando el vault todavía no se creó.</summary>
    Task<FilaDelVault?> LeerAsync(CancellationToken ct = default);

    Task GuardarAsync(FilaDelVault fila, CancellationToken ct = default);

    Task<CredencialCifrada?> LeerCredencialAsync(string clave, CancellationToken ct = default);

    Task GuardarCredencialAsync(CredencialCifrada credencial, CancellationToken ct = default);

    Task BorrarCredencialAsync(string clave, CancellationToken ct = default);

    /// <summary>Se puede contestar con el vault cerrado: saber que hay una credencial no es leerla.</summary>
    Task<bool> ExisteCredencialAsync(string clave, CancellationToken ct = default);

    /// <summary>También se puede contestar con el vault cerrado, por el mismo motivo.</summary>
    Task<IReadOnlyList<string>> ClavesAsync(string prefijo, CancellationToken ct = default);

    Task RecifrarTodoAsync(
        Func<SobreCifrado, SobreCifrado> recifrar, CancellationToken ct = default);
}
