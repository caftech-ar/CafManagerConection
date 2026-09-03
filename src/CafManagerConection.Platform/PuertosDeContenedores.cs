namespace CafManagerConection.Platform;

// docker-proxy es dueño de la mitad de los puertos y no dice cuál de los contenedores lo abrió (FR-164e).
public static class PuertosDeContenedores
{
    // ss corta los nombres según la versión, así que se compara por prefijo: en un servidor ARM apareció como docker-pr.
    private static readonly string[] Reenviadores = ["docker-proxy", "docker-pr", "rootlesskit"];

    public static bool EsReenviadorDeDocker(string? proceso) =>
        proceso is { Length: > 0 } nombre
        && Reenviadores.Any(r =>
            nombre.StartsWith(r, StringComparison.OrdinalIgnoreCase)

            // Mínimo de 8: sin él un proceso llamado «d» o «do» pasaría por docker-proxy.
            || (nombre.Length >= 8 && r.StartsWith(nombre, StringComparison.OrdinalIgnoreCase)));

    public static IReadOnlyDictionary<int, string> PorPuertoDelServidor(
        IEnumerable<ContainerInfo> contenedores)
    {
        var mapa = new Dictionary<int, string>();
        var deUnoQueCorre = new HashSet<int>();

        foreach (var contenedor in contenedores)
        {
            foreach (var puerto in contenedor.PublishedPorts.SelectMany(PuertosDelServidor))
            {
                if (deUnoQueCorre.Contains(puerto))
                {
                    continue;
                }

                mapa[puerto] = contenedor.Name;

                if (contenedor.IsRunning)
                {
                    deUnoQueCorre.Add(puerto);
                }
            }
        }

        return mapa;
    }

    /// <summary>Puertos del servidor de un mapeo como <c>8080-&gt;80/tcp</c>; el del servidor es el de la izquierda.</summary>
    // Varios porque Docker publica rangos: 8000-8005->8000-8005/tcp es un mapeo y seis puertos.
    public static IEnumerable<int> PuertosDelServidor(string mapeo)
    {
        var flecha = mapeo.IndexOf("->", StringComparison.Ordinal);

        if (flecha < 0)
        {
            yield break;
        }

        var izquierda = mapeo[..flecha];
        var dosPuntos = izquierda.LastIndexOf(':');

        if (dosPuntos >= 0)
        {
            izquierda = izquierda[(dosPuntos + 1)..];
        }

        var guion = izquierda.IndexOf('-');

        if (guion < 0)
        {
            if (int.TryParse(izquierda, out var unico) && EsPuerto(unico))
            {
                yield return unico;
            }

            yield break;
        }

        if (!int.TryParse(izquierda[..guion], out var desde)
            || !int.TryParse(izquierda[(guion + 1)..], out var hasta)
            || !EsPuerto(desde)
            || !EsPuerto(hasta)
            || hasta < desde)
        {
            yield break;
        }

        for (var puerto = desde; puerto <= hasta; puerto++)
        {
            yield return puerto;
        }
    }

    private static bool EsPuerto(int valor) => valor is >= 1 and <= 65535;
}
