using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Credentials;

/// <summary>Resuelve la credencial propia o heredada de la conexión, y la pide si falta (FR-039).</summary>
public sealed class CredentialProvider : ICredentialProvider
{
    private readonly IConnectionRepository _connections;
    private readonly IFolderRepository _folders;
    private readonly ICredentialStore _store;
    private readonly ICredentialPrompt? _prompt;

    /// <param name="prompt">Sin él, una credencial ausente devuelve <c>null</c> en lugar de preguntar.</param>
    public CredentialProvider(
        IConnectionRepository connections,
        IFolderRepository folders,
        ICredentialStore store,
        ICredentialPrompt? prompt = null)
    {
        _connections = connections;
        _folders = folders;
        _store = store;
        _prompt = prompt;
    }

    public async Task<StoredCredential?> GetForConnectionAsync(
        Guid connectionId, CancellationToken ct = default)
    {
        var registro = await _connections.GetByIdAsync(connectionId, ct).ConfigureAwait(false);

        if (registro is null)
        {
            return null;
        }

        var carpetas = await _folders.GetAllAsync(ct).ConfigureAwait(false);
        var efectivo = new SettingsResolver(carpetas)
            .Resolve(registro.Connection, registro.Rdp, registro.Ssh);

        if (efectivo.CredentialKey.Value is { } clave)
        {
            var guardada = await _store.ReadAsync(clave, ct).ConfigureAwait(false);

            if (guardada is not null)
            {
                return guardada;
            }

            // Hay clave pero no hay secreto: se borró del Administrador de credenciales por fuera.
        }

        if (_prompt is null || !NecesitaContraseña(registro, efectivo))
        {
            return null;
        }

        var pedida = await _prompt.RequestAsync(
            registro.Connection.Name,
            efectivo.UserName.Value,
            needsDomain: registro.Connection.Protocol == Domain.Connections.Protocol.Rdp,
            ct).ConfigureAwait(false);

        if (pedida is null)
        {
            return null;
        }

        var credencial = new StoredCredential(pedida.UserName, pedida.Domain, pedida.Secret);

        if (pedida.Remember)
        {
            // Contra la clave propia y no la heredada: no se redefine la contraseña de la carpeta entera.
            var propia = CredentialKey.ForConnection(
                registro.Connection.Id, registro.Connection.Protocol).Value;

            await _store.WriteAsync(propia, credencial, ct).ConfigureAwait(false);

            registro.Connection.CredentialKey = propia;
            registro.Connection.Touch();
            await _connections.UpdateAsync(registro, ct).ConfigureAwait(false);
        }

        return credencial;
    }

    /// <summary>Con clave privada la passphrase la pide SSH.NET, y una entrada web la pide el navegador.</summary>
    private static bool NecesitaContraseña(ConnectionRecord registro, EffectiveSettings efectivo) =>
        registro.Connection.Protocol switch
        {
            Domain.Connections.Protocol.Ssh =>
                efectivo.ResolvedAuthMethod == Domain.Connections.SshAuthMethod.Password,
            Domain.Connections.Protocol.Web => false,
            _ => true,
        };
}
