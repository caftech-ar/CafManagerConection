using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.App.Services;

/// <summary>Estado combinado de un host después de las dos pruebas.</summary>
public enum EstadoDeHost
{
    Sondeando,

    Vivo,

    /// <summary>Responde el equipo, pero el puerto del protocolo no acepta conexiones.</summary>
    SinServicio,

    SinRespuesta,

    SinResolver,

    /// <summary>No se sondea: su destino real no es el host y el puerto del resumen.</summary>
    NoSeSondea,
}

/// <summary>Lo que la ventana muestra de una conexión sondeada.</summary>
public sealed record FilaDeSondeo(
    ConnectionSummary Conexion,
    EstadoDeHost Estado,
    RespuestaIcmp? Icmp = null,
    bool? Puerto = null)
{
    public string Titulo => SondeoDeHosts.Titulo(Estado);

    /// <summary>Qué se probó y qué contestó cada prueba, para la ayuda emergente de la fila.</summary>
    public string Detalle => Estado switch
    {
        EstadoDeHost.Sondeando => "Probando el equipo y su puerto.",
        EstadoDeHost.NoSeSondea =>
            "Una conexión web apunta a su dirección completa, que no es este host y este puerto.",
        EstadoDeHost.SinResolver => $"No se pudo resolver «{Conexion.Host}» a una dirección.",
        _ => string.Join(
            " ",
            Icmp switch
            {
                RespuestaIcmp.Responde => "Responde al ping.",
                RespuestaIcmp.NoResponde => "No responde al ping.",
                RespuestaIcmp.NoConcluyente => "El ping no se pudo probar.",
                _ => "No responde al ping.",
            },
            Puerto == true
                ? $"El puerto {Conexion.EffectivePort} acepta la conexión."
                : $"El puerto {Conexion.EffectivePort} no acepta la conexión."),
    };
}

/// <summary>Sondea hosts con una prueba ICMP y una de puerto, en paralelo.</summary>
public static class SondeoDeHosts
{
    /// <summary>Cuántos hosts se sondean a la vez. Más no gana tiempo y el patrón se parece a un escaneo de red.</summary>
    public const int EnParalelo = 16;

    /// <summary>Cómo se nombra cada estado en la ventana.</summary>
    /// <param name="estado">El estado a nombrar.</param>
    public static string Titulo(EstadoDeHost estado) => estado switch
    {
        EstadoDeHost.Sondeando => "sondeando…",
        EstadoDeHost.Vivo => "vivo",
        EstadoDeHost.SinServicio => "sin servicio",
        EstadoDeHost.SinRespuesta => "sin respuesta",
        EstadoDeHost.SinResolver => "no resuelve el nombre",
        _ => "no se sondea",
    };

    /// <summary>Si la conexión se puede sondear con su host y su puerto efectivo.</summary>
    /// <param name="conexion">La conexión a evaluar.</param>
    public static bool SePuedeSondear(ConnectionSummary conexion)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        return conexion.Protocol != Protocol.Web
               && !string.IsNullOrWhiteSpace(conexion.Host);
    }

    /// <summary>Combina el resultado de las dos pruebas en el estado que se muestra.</summary>
    /// <param name="icmp">Lo que contestó la prueba ICMP.</param>
    /// <param name="puerto">Si el puerto aceptó la conexión.</param>
    public static EstadoDeHost Combinar(RespuestaIcmp icmp, bool puerto) => (icmp, puerto) switch
    {
        (RespuestaIcmp.SinResolver, false) => EstadoDeHost.SinResolver,
        (_, true) => EstadoDeHost.Vivo,
        (RespuestaIcmp.Responde, false) => EstadoDeHost.SinServicio,
        _ => EstadoDeHost.SinRespuesta,
    };

    /// <summary>Sondea una conexión con las dos pruebas a la vez.</summary>
    /// <param name="conexion">La conexión a sondear.</param>
    /// <param name="limite">Cuánto espera cada prueba.</param>
    /// <param name="ct">Para cortar el sondeo desde afuera.</param>
    public static async Task<FilaDeSondeo> SondearAsync(
        ConnectionSummary conexion, TimeSpan? limite = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        if (!SePuedeSondear(conexion))
        {
            return new FilaDeSondeo(conexion, EstadoDeHost.NoSeSondea);
        }

        var icmp = SondaIcmp.ProbarAsync(conexion.Host, limite, ct);
        var puerto = SondaDePuerto.RespondeAsync(conexion.Host, conexion.EffectivePort, limite, ct);

        await Task.WhenAll(icmp, puerto).ConfigureAwait(false);

        return new FilaDeSondeo(
            conexion, Combinar(icmp.Result, puerto.Result), icmp.Result, puerto.Result);
    }

    /// <summary>Sondea un puerto suelto de un host, para el despliegue de una fila.</summary>
    /// <param name="puerto">Host, puerto y de dónde salió.</param>
    /// <param name="limite">Cuánto espera la prueba.</param>
    /// <param name="ct">Para cortar el sondeo desde afuera.</param>
    public static async Task<(PuertoDeLaConexion Puerto, bool Abre)> SondearPuertoAsync(
        PuertoDeLaConexion puerto, TimeSpan? limite = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(puerto);

        var abre = await SondaDePuerto
            .RespondeAsync(puerto.Host, puerto.Puerto, limite, ct).ConfigureAwait(false);

        return (puerto, abre);
    }

    /// <summary>Sondea los puertos de una conexión, de a <see cref="EnParalelo"/>.</summary>
    /// <param name="puertos">Los puertos configurados de la conexión.</param>
    /// <param name="limite">Cuánto espera cada prueba.</param>
    /// <param name="ct">Para cortar el sondeo desde afuera.</param>
    public static async Task<IReadOnlyList<(PuertoDeLaConexion Puerto, bool Abre)>>
        SondearPuertosAsync(
            IEnumerable<PuertoDeLaConexion> puertos,
            TimeSpan? limite = null,
            CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(puertos);

        using var turnos = new SemaphoreSlim(EnParalelo);

        var tareas = puertos.Select(async puerto =>
        {
            await turnos.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                return await SondearPuertoAsync(puerto, limite, ct).ConfigureAwait(false);
            }
            finally
            {
                turnos.Release();
            }
        });

        return await Task.WhenAll(tareas).ConfigureAwait(false);
    }

    /// <summary>Sondea un lote de a <see cref="EnParalelo"/>, informando cada resultado al llegar.</summary>
    /// <param name="conexiones">Las conexiones a sondear.</param>
    /// <param name="alLlegar">Se llama con cada fila resuelta.</param>
    /// <param name="limite">Cuánto espera cada prueba.</param>
    /// <param name="ct">Para cortar el lote desde afuera.</param>
    public static async Task SondearLoteAsync(
        IEnumerable<ConnectionSummary> conexiones,
        Action<FilaDeSondeo> alLlegar,
        TimeSpan? limite = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conexiones);
        ArgumentNullException.ThrowIfNull(alLlegar);

        using var turnos = new SemaphoreSlim(EnParalelo);

        var tareas = conexiones.Select(async conexion =>
        {
            await turnos.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                alLlegar(await SondearAsync(conexion, limite, ct).ConfigureAwait(false));
            }
            finally
            {
                turnos.Release();
            }
        });

        await Task.WhenAll(tareas).ConfigureAwait(false);
    }
}
