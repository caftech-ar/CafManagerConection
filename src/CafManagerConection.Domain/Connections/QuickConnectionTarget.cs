namespace CafManagerConection.Domain.Connections;

/// <summary>Destino de una conexión rápida leído de <c>usuario@host:puerto</c> (FR-149).</summary>
public sealed record QuickConnectionTarget(string? UserName, string Host, int Port)
{
    public const int DefaultPort = 22;

    public static bool TryParse(
        string? texto, out string? userName, out string host, out int port, out string? error)
    {
        userName = null;
        host = string.Empty;
        port = DefaultPort;

        var recortado = texto?.Trim() ?? string.Empty;

        if (recortado.Length == 0)
        {
            error = "Escribí «usuario@host» o «usuario@host:puerto».";
            return false;
        }

        var arroba = recortado.IndexOf('@');
        string destino;

        if (arroba >= 0)
        {
            var usuarioTexto = recortado[..arroba].Trim();

            if (usuarioTexto.Length == 0)
            {
                error = "Falta el usuario antes de la arroba.";
                return false;
            }

            userName = usuarioTexto;
            destino = recortado[(arroba + 1)..].Trim();
        }
        else
        {
            destino = recortado;
        }

        if (destino.Length == 0)
        {
            error = "Falta el host.";
            return false;
        }

        string hostTexto;
        string? puertoTexto;

        if (destino[0] == '[')
        {
            var cierre = destino.IndexOf(']');

            if (cierre < 0)
            {
                error = "Falta el corchete de cierre «]» de la dirección IPv6.";
                return false;
            }

            hostTexto = destino[1..cierre].Trim();
            var resto = destino[(cierre + 1)..];

            if (resto.Length == 0)
            {
                puertoTexto = null;
            }
            else if (resto[0] == ':')
            {
                puertoTexto = resto[1..];
            }
            else
            {
                error = "Después de la dirección IPv6 sólo puede ir «:puerto».";
                return false;
            }
        }
        else
        {
            var puntos = destino.Count(c => c == ':');

            switch (puntos)
            {
                case 0:
                    hostTexto = destino;
                    puertoTexto = null;
                    break;

                case 1:
                    var pos = destino.IndexOf(':');
                    hostTexto = destino[..pos];
                    puertoTexto = destino[(pos + 1)..];
                    break;

                default:
                    error = "El host tiene más de un «:»; las direcciones IPv6 van entre "
                        + "corchetes, por ejemplo [2001:db8::1]:22.";
                    return false;
            }
        }

        hostTexto = hostTexto.Trim();

        if (hostTexto.Length == 0)
        {
            error = "Falta el host.";
            return false;
        }

        host = hostTexto;

        if (puertoTexto is not null)
        {
            var puertoRecortado = puertoTexto.Trim();

            if (puertoRecortado.Length == 0)
            {
                error = "Falta el número de puerto después de los dos puntos.";
                return false;
            }

            if (!int.TryParse(puertoRecortado, out var numero))
            {
                error = $"«{puertoRecortado}» no es un número de puerto válido.";
                return false;
            }

            if (numero is < 1 or > 65535)
            {
                error = "El puerto debe estar entre 1 y 65535.";
                return false;
            }

            port = numero;
        }

        error = null;
        return true;
    }
}
