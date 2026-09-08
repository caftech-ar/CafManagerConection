using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Credentials;

/// <summary>Resuelve la contraseña propia o heredada de la conexión, y la pide si falta.</summary>
public sealed class CredentialProvider : ICredentialProvider
{
    private readonly IConnectionRepository _connections;
    private readonly IFolderRepository _folders;
    private readonly ICredentialStore _store;
    private readonly ICredentialPrompt? _prompt;

    /// <param name="prompt">Sin él, una contraseña ausente devuelve <c>null</c> en lugar de preguntar.</param>
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

        if (efectivo.Secreto.IsDefined
            && await GuardadaAsync(efectivo, ct).ConfigureAwait(false) is { } guardada)
        {
            return guardada;
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
            await GuardarEnLaConexionAsync(registro, credencial, ct).ConfigureAwait(false);
        }

        return credencial;
    }

    private async Task<StoredCredential?> GuardadaAsync(
        EffectiveSettings efectivo, CancellationToken ct)
    {
        char[]? secreto;

        try
        {
            secreto = await _store.ReadAsync(efectivo.Secreto.Value, ct).ConfigureAwait(false);
        }
        catch (CredencialIlegibleException)
        {
            // Quedó cifrada con otra clave maestra. No se recupera, así que se pide de nuevo en
            // lugar de dejar caer la conexión con un error de criptografía.
            return null;
        }
        catch (VaultCerradoException)
        {
            // El vault cerrado no impide conectar: se pide la contraseña para esta sesión y no se
            // guarda.
            return null;
        }

        if (secreto is null)
        {
            return null;
        }

        try
        {
            return new StoredCredential(
                efectivo.UserName.Value ?? string.Empty, efectivo.Domain.Value, secreto);
        }
        finally
        {
            Array.Clear(secreto);
        }
    }

    // Contra la fila de la conexion y no la heredada: no se redefine la contraseña de la carpeta
    // entera. Con el vault cerrado no se puede guardar, y no poder guardar no puede costarle la
    // sesion al usuario: se conecta con lo que escribio y no se persiste nada.
    private async Task GuardarEnLaConexionAsync(
        ConnectionRecord registro, StoredCredential credencial, CancellationToken ct)
    {
        var secreto = credencial.Secret.ToArray();

        try
        {
            await _store.WriteAsync(
                ReferenciaDeSecreto.DeConexion(
                    registro.Connection.Id, registro.Connection.Protocol),
                secreto,
                ct).ConfigureAwait(false);
        }
        catch (VaultCerradoException)
        {
            return;
        }
        finally
        {
            Array.Clear(secreto);
        }

        registro.Connection.TieneSecreto = true;
        registro.Connection.UserName = credencial.UserName;
        registro.Connection.Touch();

        await _connections.UpdateAsync(registro, ct).ConfigureAwait(false);
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
