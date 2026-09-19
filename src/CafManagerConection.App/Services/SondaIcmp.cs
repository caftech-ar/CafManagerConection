using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CafManagerConection.App.Services;

/// <summary>Resultado de una prueba ICMP contra un host.</summary>
public enum RespuestaIcmp
{
    Responde,

    NoResponde,

    /// <summary>El nombre no se pudo resolver a una dirección.</summary>
    SinResolver,

    /// <summary>No se pudo probar —por ejemplo, por permisos—, así que no dice nada del host.</summary>
    NoConcluyente,
}

/// <summary>Prueba si un host responde ICMP.</summary>
public static class SondaIcmp
{
    /// <summary>Manda un eco y espera la respuesta.</summary>
    /// <param name="host">Nombre o dirección del equipo.</param>
    /// <param name="limite">Cuánto esperar la respuesta.</param>
    /// <param name="ct">Para cortar la prueba desde afuera.</param>
    public static async Task<RespuestaIcmp> ProbarAsync(
        string host, TimeSpan? limite = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return RespuestaIcmp.SinResolver;
        }

        var espera = limite ?? TimeSpan.FromSeconds(1);

        using var ping = new Ping();

        try
        {
            var respuesta = await ping.SendPingAsync(host, espera, cancellationToken: ct)
                .ConfigureAwait(false);

            return respuesta.Status switch
            {
                IPStatus.Success => RespuestaIcmp.Responde,
                _ => RespuestaIcmp.NoResponde,
            };
        }
        catch (PingException ex) when (ex.InnerException is SocketException)
        {
            return RespuestaIcmp.SinResolver;
        }
        catch (OperationCanceledException)
        {
            return RespuestaIcmp.NoResponde;
        }
        catch (Exception ex) when (ex is PingException or InvalidOperationException
                                       or NotSupportedException)
        {
            return RespuestaIcmp.NoConcluyente;
        }
    }
}
