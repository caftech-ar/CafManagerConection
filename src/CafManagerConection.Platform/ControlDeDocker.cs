using System.Text.RegularExpressions;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Platform;

public enum AccionDeContenedor
{
    Iniciar,
    Detener,
    Reiniciar,
}

public sealed class ControlDeDocker
{
    private readonly IPlatformCommandRunner _runner;
    private readonly int _timeout;
    private readonly IAppLogger? _logger;
    private readonly Guid _conexion;
    private readonly IPlatformLogStreamer? _streamer;

    public ControlDeDocker(
        IPlatformCommandRunner runner,
        int timeoutSeconds = 30,
        IAppLogger? logger = null,
        Guid connectionId = default)
    {
        _runner = runner;
        _timeout = timeoutSeconds;
        _logger = logger;
        _conexion = connectionId;
        _streamer = runner as IPlatformLogStreamer;
    }

    public bool PuedeSeguirRegistro => _streamer is not null;

    /// <summary>Cada línea nueva del registro del contenedor llega a <paramref name="onLinea"/> (FR-150).</summary>
    /// <returns>Desecharlo es lo único que cierra la conexión SSH; sin eso <c>docker logs -f</c> sigue corriendo allá.</returns>
    public async Task<IAsyncDisposable> SeguirRegistroAsync(
        string contenedor,
        Action<string> onLinea,
        Action<string?> onCerrado,
        CancellationToken ct = default)
    {
        if (_streamer is null)
        {
            throw new InvalidOperationException(
                "Este servidor no tiene disponible el canal de registro en vivo.");
        }

        if (!EsNombreValido(contenedor))
        {
            throw new ArgumentException(
                $"«{contenedor}» no parece un nombre de contenedor.", nameof(contenedor));
        }

        _logger?.PlatformActionPerformed(_conexion, "docker logs -f");

        return await _streamer.SeguirAsync(
                $"docker logs -f --tail 200 {contenedor} 2>&1", onLinea, onCerrado, ct)
            .ConfigureAwait(false);
    }

    /// <summary>El nombre viene de <c>docker ps</c> y termina en una línea con sudo: uno con <c>;</c> ejecutaría cualquier cosa.</summary>
    private static readonly Regex NombreValido =
        new("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$", RegexOptions.Compiled);

    public static bool EsNombreValido(string? nombre) =>
        nombre is not null && NombreValido.IsMatch(nombre);

    public async Task<InventoryResult<string>> EjecutarAsync(
        AccionDeContenedor accion, string contenedor, CancellationToken ct = default)
    {
        if (!EsNombreValido(contenedor))
        {
            return InventoryResult<string>.Fail(
                $"«{contenedor}» no parece un nombre de contenedor y no se envió nada.");
        }

        var verbo = accion switch
        {
            AccionDeContenedor.Iniciar => "start",
            AccionDeContenedor.Detener => "stop",
            _ => "restart",
        };

        // Se registra el verbo y no el contenedor: el nombre es contenido de sesión (Principio II).
        _logger?.PlatformActionPerformed(_conexion, $"docker {verbo}");

        var (ok, salida, error) = await _runner
            .RunWithSudoAsync($"docker {verbo} {contenedor}", _timeout, ct)
            .ConfigureAwait(false);

        if (ok)
        {
            return InventoryResult<string>.Ok($"Listo: docker {verbo} {contenedor}");
        }

        var texto = string.IsNullOrWhiteSpace(error) ? salida : error;

        return InventoryResult<string>.Fail(
            string.IsNullOrWhiteSpace(texto)
                ? $"Docker no contestó nada al pedirle {verbo}."
                : texto.Trim());
    }

    // docker stats --no-stream va último: es el que tarda, y así lo demás ya está leído si corta el tiempo límite.
    public async Task<InventoryResult<DetalleDeContenedor>> GetDetalleAsync(
        string contenedor, CancellationToken ct = default)
    {
        if (!EsNombreValido(contenedor))
        {
            return InventoryResult<DetalleDeContenedor>.Fail(
                $"«{contenedor}» no parece un nombre de contenedor.");
        }

        var guion = string.Join('\n', [
            $"echo '{Marca.Resumen}'",
            // Las variables de entorno no se piden (FR-150d): ahí viven las contraseñas de base y las claves de API.
            "docker inspect --format "
            + "'{{.Name}}|{{.Config.Image}}|{{.State.Status}}"
            + "|{{.RestartCount}}|{{.State.StartedAt}}|{{.HostConfig.RestartPolicy.Name}}"
            + "|{{.Config.WorkingDir}}|{{.Path}} {{range .Args}}{{.}} {{end}}"
            + "|{{.Id}}|{{.Image}}|{{.Created}}' "
            + contenedor,
            $"echo '{Marca.Salud}'",
            "docker inspect --format '{{.State.Health.Status}}' " + contenedor + " 2>/dev/null",
            $"echo '{Marca.Compose}'",
            "docker inspect --format "
            + "'{{index .Config.Labels \"com.docker.compose.project\"}}"
            + "|{{index .Config.Labels \"com.docker.compose.service\"}}' "
            + contenedor + " 2>/dev/null",
            $"echo '{Marca.Redes}'",
            "docker inspect --format "
            + "'{{range $red, $conf := .NetworkSettings.Networks}}{{$red}}"
            + "{{if $conf.IPAddress}} -> {{$conf.IPAddress}}{{end}}{{\"\\n\"}}{{end}}' "
            + contenedor,
            $"echo '{Marca.Puertos}'",
            "docker port " + contenedor,
            $"echo '{Marca.Volumenes}'",
            "docker inspect --format "
            + "'{{range .Mounts}}{{.Source}} -> {{.Destination}} ({{.Mode}}){{\"\\n\"}}{{end}}' "
            + contenedor,
            $"echo '{Marca.Registro}'",
            "docker logs --tail 40 " + contenedor + " 2>&1",
            $"echo '{Marca.Consumo}'",
            "docker stats --no-stream --format "
            + "'{{.CPUPerc}}|{{.MemUsage}}|{{.MemPerc}}|{{.NetIO}}|{{.BlockIO}}|{{.PIDs}}' "
            + contenedor,
        ]);

        var (ok, salida, error) = await _runner
            .RunWithSudoAsync(guion, _timeout, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(salida))
        {
            return InventoryResult<DetalleDeContenedor>.Fail(
                string.IsNullOrWhiteSpace(error)
                    ? "Docker no devolvió nada sobre ese contenedor."
                    : error.Trim());
        }

        var detalle = DetalleDeContenedor.Interpretar(contenedor, salida);

        // El estado de salida no decide: docker port termina distinto de cero si el contenedor no publica ninguno.
        return ok || detalle.TieneAlgo
            ? InventoryResult<DetalleDeContenedor>.Ok(detalle)
            : InventoryResult<DetalleDeContenedor>.Fail(
                string.IsNullOrWhiteSpace(error) ? "No se pudo leer el detalle." : error.Trim());
    }

    internal static class Marca
    {
        public const string Resumen = "cmc:resumen";
        public const string Puertos = "cmc:puertos";
        public const string Volumenes = "cmc:volumenes";
        public const string Registro = "cmc:registro";
        public const string Consumo = "cmc:consumo";
        public const string Redes = "cmc:redes";

        public const string Salud = "cmc:salud";

        public const string Compose = "cmc:compose";
    }
}
