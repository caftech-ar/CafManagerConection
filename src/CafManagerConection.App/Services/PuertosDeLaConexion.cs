using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.App.Services;

/// <summary>Un puerto que vale la pena sondear de una conexión, con de dónde salió.</summary>
public sealed record PuertoDeLaConexion(string Host, int Puerto, string Descripcion);

/// <summary>Reúne los puertos que una conexión tiene configurados. No barre rangos: sólo lo declarado.</summary>
public static class PuertosDeLaConexion
{
    /// <summary>Los puertos de una conexión: el de su protocolo, el de su dirección web y los extremos de sus túneles.</summary>
    /// <param name="conexion">El resumen de la conexión, que ya trae host y puerto efectivo.</param>
    /// <param name="detalle">El detalle, para la dirección web; puede venir nulo.</param>
    /// <param name="tuneles">Los túneles definidos para esa conexión.</param>
    public static IReadOnlyList<PuertoDeLaConexion> De(
        ConnectionSummary conexion,
        ConnectionRecord? detalle = null,
        IReadOnlyList<SshTunnel>? tuneles = null)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var puertos = new List<PuertoDeLaConexion>();

        void Sumar(string host, int puerto, string descripcion)
        {
            if (puerto is < 1 or > 65535 || string.IsNullOrWhiteSpace(host))
            {
                return;
            }

            if (puertos.Any(p => p.Puerto == puerto
                                 && p.Host.Equals(host, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            puertos.Add(new PuertoDeLaConexion(host, puerto, descripcion));
        }

        if (conexion.Protocol != Protocol.Web)
        {
            Sumar(conexion.Host, conexion.EffectivePort, NombreDelProtocolo(conexion.Protocol));
        }

        if (detalle?.Web?.Url is { Length: > 0 } url
            && Uri.TryCreate(url, UriKind.Absolute, out var direccion))
        {
            Sumar(direccion.Host, direccion.Port, $"web ({direccion.Scheme})");
        }

        foreach (var tunel in tuneles ?? [])
        {
            Sumar(tunel.RemoteHost, tunel.RemotePort, $"túnel «{tunel.Name}», extremo remoto");
        }

        return puertos;
    }

    private static string NombreDelProtocolo(Protocol protocolo) => protocolo switch
    {
        Protocol.Ssh => "SSH",
        Protocol.Rdp => "RDP",
        _ => protocolo.ToString(),
    };
}
