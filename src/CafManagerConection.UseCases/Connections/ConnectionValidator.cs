using System.Net;
using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Connections;

public static class ConnectionValidator
{
    // FR-127: colgar de alguien que ya cuelga, o colgar a alguien que ya tiene hijas, no está permitido.
    public static ValidationResult ValidateParent(
        Connection conexion, Connection? padre, bool conexionTieneHijas)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        if (padre is null)
        {
            return ValidationResult.Valid;
        }

        if (padre.Id == conexion.Id)
        {
            return ValidationResult.Invalid(new ValidationError(
                nameof(Connection.ParentConnectionId),
                "Una conexión no puede colgar de sí misma."));
        }

        if (padre.ParentConnectionId is not null)
        {
            return ValidationResult.Invalid(new ValidationError(
                nameof(Connection.ParentConnectionId),
                $"«{padre.Name}» ya cuelga de otra conexión, y sólo se admite un nivel."));
        }

        if (conexionTieneHijas)
        {
            return ValidationResult.Invalid(new ValidationError(
                nameof(Connection.ParentConnectionId),
                $"«{conexion.Name}» tiene servicios colgando, así que no puede colgar de otra "
                + "conexión: sólo se admite un nivel."));
        }

        return ValidationResult.Valid;
    }

    public static ValidationResult Validate(ConnectionRecord record)
    {
        var errors = new List<ValidationError>();
        var c = record.Connection;

        if (string.IsNullOrWhiteSpace(c.Name))
        {
            errors.Add(new ValidationError(nameof(c.Name), "El nombre es obligatorio."));
        }
        else if (c.Name.Length > Connection.MaxNameLength)
        {
            errors.Add(new ValidationError(
                nameof(c.Name),
                $"El nombre no puede superar los {Connection.MaxNameLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(c.Host))
        {
            errors.Add(new ValidationError(nameof(c.Host), "El host es obligatorio."));
        }
        else if (!IsValidHost(c.Host, c.Protocol))
        {
            errors.Add(new ValidationError(
                nameof(c.Host), "El host no es un nombre ni una dirección IP válida."));
        }

        if (c.Port is { } port && (port < 1 || port > 65535))
        {
            errors.Add(new ValidationError(nameof(c.Port), "El puerto debe estar entre 1 y 65535."));
        }

        if (c.Notes is { Length: > Connection.MaxNotesLength })
        {
            errors.Add(new ValidationError(
                nameof(c.Notes),
                $"Las notas no pueden superar los {Connection.MaxNotesLength} caracteres."));
        }

        if (record.Ssh is { AuthMethod: SshAuthMethod.PrivateKey } ssh &&
            string.IsNullOrWhiteSpace(ssh.PrivateKeyPath))
        {
            errors.Add(new ValidationError(
                nameof(ssh.PrivateKeyPath),
                "Con autenticación por clave privada, la ruta de la clave es obligatoria."));
        }

        if (record.Ssh is { KeepAliveSeconds: { } keepAlive } && (keepAlive < 0 || keepAlive > 3600))
        {
            errors.Add(new ValidationError(
                "KeepAliveSeconds", "El keep-alive debe estar entre 0 y 3600 segundos."));
        }

        if (record.Web is { } web)
        {
            if (string.IsNullOrWhiteSpace(web.Url))
            {
                errors.Add(new ValidationError(nameof(web.Url), "La dirección URL es obligatoria."));
            }
            else if (!Uri.TryCreate(web.Url, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add(new ValidationError(
                    nameof(web.Url), "La dirección debe ser una URL http o https válida."));
            }
        }

        return errors.Count == 0 ? ValidationResult.Valid : new ValidationResult(false, errors);
    }

    private static bool IsValidHost(string host, Protocol protocol)
    {
        var trimmed = host.Trim();

        if (protocol == Protocol.Web)
        {
            return true;
        }

        if (IPAddress.TryParse(trimmed, out _))
        {
            return true;
        }

        return Uri.CheckHostName(trimmed) != UriHostNameType.Unknown;
    }
}
