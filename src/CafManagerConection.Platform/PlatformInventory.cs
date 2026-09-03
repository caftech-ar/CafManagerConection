using System.Diagnostics;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Platform;

public interface IPlatformCommandRunner
{
    Task<(bool Success, string Output, string Error)> RunAsync(
        string command, int timeoutSeconds, CancellationToken ct = default);

    Task<(bool Success, string Output, string Error)> RunWithSudoAsync(
        string command, int timeoutSeconds, CancellationToken ct = default);
}

public sealed record InventoryResult<T>(bool Success, T? Value, string? Error)
{
    public static InventoryResult<T> Ok(T value) => new(true, value, null);

    public static InventoryResult<T> Fail(string error) => new(false, default, error);
}

// Sólo lectura: no hay ningún método que escriba (FR-100). Se consulta al abrir el panel y con el botón, nunca en bucle (FR-107).
public sealed class PlatformInventory
{
    private const string Marca = "###CMC###";

    private readonly IPlatformCommandRunner _runner;
    private readonly int _timeout;
    private readonly IAppLogger? _logger;
    private readonly Guid _conexion;

    public PlatformInventory(
        IPlatformCommandRunner runner,
        int timeoutSeconds = 10,
        IAppLogger? logger = null,
        Guid connectionId = default)
    {
        _runner = runner;
        _timeout = timeoutSeconds;
        _logger = logger;
        _conexion = connectionId;
    }

    private async Task<T> Medir<T>(RemoteWork trabajo, Func<Task<T>> consulta)
    {
        if (_logger is null)
        {
            return await consulta().ConfigureAwait(false);
        }

        var reloj = Stopwatch.StartNew();

        try
        {
            return await consulta().ConfigureAwait(false);
        }
        finally
        {
            _logger.WorkCompleted(_conexion, trabajo, reloj.Elapsed);
        }
    }

    // El exit 0 final: sin él un servidor sin supervisord terminaba en estado 1 y se descartaba la detección entera.
    private static readonly string GuionDeteccion = """
        PATH="$PATH:/usr/local/bin:/usr/local/sbin:/usr/sbin:/sbin:/snap/bin"

        test -r /proc/stat && echo "cmc:linux"

        # De cada servicio interesa distinguir cuatro situaciones, porque desde la aplicacion
        # las cuatro se veian igual -un panel que no aparece- y se arreglan de forma distinta:
        # no esta instalado, esta pero no corre, corre pero no se puede consultar, o anda.
        if ! command -v docker >/dev/null 2>&1; then
            echo "cmc:docker=no"
        elif ! pgrep -x dockerd >/dev/null 2>&1; then
            echo "cmc:docker=parado"
        else
            # Igual que con supervisord: si esta instalado y corriendo, hay panel. Probar aca
            # `sudo -n docker ps` solo contestaba si sudo anda SIN contraseña, y donde la pide
            # escondia el panel de un Docker perfectamente consultable con la credencial
            # guardada.
            echo "cmc:docker=ok"
        fi

        if ! command -v nginx >/dev/null 2>&1; then
            echo "cmc:nginx=no"
        elif ! pgrep -x nginx >/dev/null 2>&1; then
            echo "cmc:nginx=parado"
        else
            # `nginx -T` necesita root, pero eso ya no decide nada: si no se puede, la consulta
            # arma la configuracion leyendo los archivos, que casi siempre son legibles.
            echo "cmc:nginx=ok"
        fi

        # supervisorctl devuelve 3 cuando hay procesos detenidos, y eso NO es falta de
        # permiso: es una respuesta valida. Tomarlo por error escondia el panel justo en el
        # caso mas comun, que es tener algo caido y querer verlo.
        # Tampoco alcanza con `command -v supervisorctl`: es muy comun instalar supervisor en un
        # virtualenv de Python, y ahi el binario no queda en el PATH aunque supervisord este
        # corriendo. Se resuelve a partir del proceso vivo, que trae la ruta del binario y su
        # archivo de configuracion.
        sup=""
        conf=""
        linea=$(pgrep -af supervisord 2>/dev/null | head -1)

        if [ -n "$linea" ]; then
            conf=$(echo "$linea" | sed -n 's/.*-c[[:space:]][[:space:]]*\([^ ]*\).*/\1/p')

            for tramo in $linea; do
                case "$tramo" in
                    */supervisord)
                        if [ -x "${tramo%/supervisord}/supervisorctl" ]; then
                            sup="${tramo%/supervisord}/supervisorctl"
                        fi
                        break
                        ;;
                esac
            done
        fi

        if [ -z "$sup" ] && command -v supervisorctl >/dev/null 2>&1; then
            sup=supervisorctl
        fi

        # El -c explicito importa: sin el, supervisorctl busca supervisord.conf en el directorio
        # actual y en /etc, y con una instalacion fuera de /etc contesta que no encuentra nada.
        if [ -n "$sup" ] && [ -n "$conf" ]; then
            comando="$sup -c $conf"
        else
            comando="$sup"
        fi

        # Se publica el comando resuelto para que la consulta del panel use exactamente este y
        # no tenga que volver a averiguarlo: es un viaje de ida y vuelta menos, y sobre un
        # enlace lento eso se nota.
        [ -n "$sup" ] && echo "cmc:supctl=$comando"

        # Que la consulta ande o no NO se decide aca. `sudo -n` es sudo sin contraseña, y en
        # varios servidores sudo la pide: ahi esto fallaba, marcaba falta de permiso y escondia
        # el panel, cuando la consulta de verdad sabe escalar con la credencial guardada y
        # habria andado. La deteccion contesta si el servicio existe y corre; si despues la
        # consulta falla, el panel muestra por que, que es infinitamente mas util que no estar.
        if [ -z "$sup" ] && [ -z "$linea" ]; then
            echo "cmc:supervisord=no"
        elif [ -z "$linea" ]; then
            echo "cmc:supervisord=parado"
        elif [ -z "$sup" ]; then
            # Corre, pero no hay cliente con que consultarlo: no lo arregla ninguna contraseña.
            echo "cmc:supervisord=permiso"
        else
            echo "cmc:supervisord=ok"
        fi

        exit 0
        """.ReplaceLineEndings("\n");

    public string? LastDetectionOutput { get; private set; }

    private string _supervisorctl = "supervisorctl";

    public string SupervisorctlResuelto => _supervisorctl;

    public async Task<ServerCapabilities> DetectAsync(CancellationToken ct = default)
    {
        var (ok, salida, error) = await Medir(RemoteWork.PlatformDetection, () => _runner
            .RunAsync(GuionDeteccion, _timeout, ct)).ConfigureAwait(false);

        LastDetectionOutput = ok
            ? salida
            : $"(el comando falló) {error}";

        if (!ok)
        {
            return ServerCapabilities.None;
        }

        var marcas = salida
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);

        var resuelto = marcas.FirstOrDefault(m => m.StartsWith("cmc:supctl=", StringComparison.Ordinal));

        if (resuelto is not null && resuelto.Length > "cmc:supctl=".Length)
        {
            _supervisorctl = resuelto["cmc:supctl=".Length..];
        }

        ServiceState Estado(string servicio)
        {
            if (marcas.Contains($"cmc:{servicio}=ok")) return ServiceState.Available;
            if (marcas.Contains($"cmc:{servicio}=permiso")) return ServiceState.NoPermission;
            if (marcas.Contains($"cmc:{servicio}=parado")) return ServiceState.NotRunning;

            return ServiceState.NotInstalled;
        }

        return new ServerCapabilities(
            marcas.Contains("cmc:linux"),
            Estado("docker"),
            Estado("nginx"),
            Estado("supervisord"));
    }

    public async Task<InventoryResult<IReadOnlyList<ContainerInfo>>> GetContainersAsync(
        CancellationToken ct = default)
    {
        var (ok, salida, error) = await Medir(RemoteWork.Docker, () => _runner.RunWithSudoAsync(
            $"docker ps -a --no-trunc --format '{DockerPsParser.Format}'",
            _timeout,
            ct)).ConfigureAwait(false);

        if (!ok)
        {
            return InventoryResult<IReadOnlyList<ContainerInfo>>.Fail(TraducirDocker(error));
        }

        return InventoryResult<IReadOnlyList<ContainerInfo>>.Ok(DockerPsParser.Parse(salida));
    }

    // Una sola lectura (--no-stream): Docker toma dos muestras del cgroup y tarda alrededor de un segundo.
    public async Task<InventoryResult<IReadOnlyDictionary<string, ContainerUsage>>>
        GetUsageAsync(CancellationToken ct = default)
    {
        var (ok, salida, error) = await Medir(RemoteWork.Docker, () => _runner.RunWithSudoAsync(
            $"docker stats --no-stream --format '{DockerStatsParser.Format}'",
            _timeout,
            ct)).ConfigureAwait(false);

        if (!ok)
        {
            return InventoryResult<IReadOnlyDictionary<string, ContainerUsage>>
                .Fail(TraducirDocker(error));
        }

        return InventoryResult<IReadOnlyDictionary<string, ContainerUsage>>
            .Ok(DockerStatsParser.Parse(salida));
    }

    /// <summary>Proyectos compose con sus servicios ya relacionados con los contenedores (FR-097).</summary>
    /// <param name="incluirServicios">Apagado: se midió que traerlos agrega unos 870 ms al panel de Docker.</param>
    public async Task<InventoryResult<IReadOnlyList<ComposeProject>>> GetComposeProjectsAsync(
        IReadOnlyList<ContainerInfo> contenedores,
        CancellationToken ct = default,
        bool incluirServicios = false)
    {
        var (ok, salida, error) = await Medir(RemoteWork.Docker, () => _runner.RunWithSudoAsync(
            "docker compose ls --all --format json", _timeout, ct)).ConfigureAwait(false);

        if (!ok)
        {
            return InventoryResult<IReadOnlyList<ComposeProject>>.Fail(TraducirDocker(error));
        }

        var declarados = ComposeParser.ParseProjects(salida);

        var servicios = incluirServicios
            ? await LeerServiciosAsync(declarados, ct).ConfigureAwait(false)
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var proyectos = declarados
            .Select(p => new ComposeProject(
                p.Name,
                p.FilePath,
                ComposeParser.Correlate(
                    p.Name,
                    servicios.GetValueOrDefault(p.Name, []),
                    contenedores)))
            .ToList();

        return InventoryResult<IReadOnlyList<ComposeProject>>.Ok(proyectos);
    }

    // Con nueve proyectos eran nueve idas y vueltas de unos 97 ms; el exit 0 final evita que un compose faltante descarte todo.
    private async Task<Dictionary<string, IReadOnlyList<string>>> LeerServiciosAsync(
        IReadOnlyList<(string Name, string FilePath)> proyectos, CancellationToken ct)
    {
        var conArchivo = proyectos
            .Where(p => !string.IsNullOrEmpty(p.FilePath))
            .ToList();

        var resultado = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        if (conArchivo.Count == 0)
        {
            return resultado;
        }

        var partes = conArchivo.Select(p =>
        {
            var primero = p.FilePath.Split(',').First().Trim();

            // Una ruta con comilla simple rompería el entrecomillado y se llevaría puestos a los demás proyectos.
            return $"docker compose -f {EntreComillas(primero)} config --services 2>/dev/null";
        });

        // La marca va entre comillas simples: un # al principio de una palabra abre un comentario de shell y todo esto va en una línea.
        var comando = string.Join($"; echo '{Marca}'; ", partes) + $"; echo '{Marca}'; exit 0";

        var (ok, salida, _) = await Medir(RemoteWork.Docker, () => _runner.RunWithSudoAsync(
            comando, _timeout, ct)).ConfigureAwait(false);

        if (!ok)
        {
            return resultado;
        }

        var bloques = salida.Split(Marca);

        for (var i = 0; i < conArchivo.Count && i < bloques.Length; i++)
        {
            resultado[conArchivo[i].Name] = ComposeParser.ParseServices(bloques[i]).ToList();
        }

        return resultado;
    }

    // Con escalada a sudo: el nombre del proceso sólo se ve con privilegios; sin ellos la fila sale como desconocido.
    public async Task<InventoryResult<IReadOnlyList<ListeningPort>>> GetListeningPortsAsync(
        CancellationToken ct = default)
    {
        var (ok, salida, error) = await Medir(RemoteWork.Puertos, () => _runner.RunWithSudoAsync(
            "ss -tulnpH 2>/dev/null || netstat -tulnp 2>/dev/null",
            _timeout,
            ct)).ConfigureAwait(false);

        var puertos = PuertosParser.Parse(salida);

        // ss devuelve no cero cuando no puede resolver algún proceso: si hubo puertos, el estado da igual.
        if (puertos.Count == 0 && !ok)
        {
            return InventoryResult<IReadOnlyList<ListeningPort>>.Fail(
                string.IsNullOrWhiteSpace(error)
                    ? "No se pudieron leer los puertos: falta `ss` y `netstat` en el servidor."
                    : error.Trim());
        }

        return InventoryResult<IReadOnlyList<ListeningPort>>.Ok(puertos);
    }

    /// <summary>Respaldo cuando <c>nginx -T</c> no se puede: arma la misma salida leyendo los archivos.</summary>
    // Los include salen del propio nginx.conf: con sites-enabled y conf.d fijos no se veían otros directorios.
    private static readonly string GuionLecturaNginx = """
        raiz=/etc/nginx
        vistos=""

        emitir() {
            [ -f "$1" ] && [ -r "$1" ] || return 0
            case " $vistos " in *" $1 "*) return 0 ;; esac
            vistos="$vistos $1"
            echo "# configuration file $1:"
            cat "$1"
        }

        patrones="$raiz/nginx.conf"

        if [ -r "$raiz/nginx.conf" ]; then
            patrones="$patrones $(sed -n 's/^[[:space:]]*include[[:space:]][[:space:]]*\([^;]*\);.*/\1/p' "$raiz/nginx.conf")"
        fi

        patrones="$patrones $raiz/sites-enabled/* $raiz/conf.d/*.conf"

        for patron in $patrones; do
            case "$patron" in /*) ;; *) patron="$raiz/$patron" ;; esac

            for archivo in $patron; do
                emitir "$archivo"
            done
        done
        """;

    public async Task<InventoryResult<IReadOnlyList<NginxSite>>> GetNginxSitesAsync(
        CancellationToken ct = default)
    {
        var (ok, salida, error) = await Medir(RemoteWork.Nginx, () => _runner.RunWithSudoAsync(
            "nginx -T 2>&1", _timeout, ct)).ConfigureAwait(false);

        var sitios = ok || salida.Contains("server", StringComparison.Ordinal)
            ? NginxConfigParser.Parse(salida)
            : [];

        // nginx -T puede devolver cero y no imprimir nada si no pudo abrir los archivos incluidos.
        if (sitios.Count > 0)
        {
            return InventoryResult<IReadOnlyList<NginxSite>>.Ok(sitios);
        }

        var (leyo, texto, errorLectura) = await Medir(RemoteWork.Nginx, () => _runner.RunAsync(
            GuionLecturaNginx.ReplaceLineEndings("\n"), _timeout, ct)).ConfigureAwait(false);

        if (leyo)
        {
            sitios = NginxConfigParser.Parse(texto);

            if (sitios.Count > 0)
            {
                return InventoryResult<IReadOnlyList<NginxSite>>.Ok(sitios);
            }
        }

        var detalle = PrimeroNoVacio(error, errorLectura);

        return InventoryResult<IReadOnlyList<NginxSite>>.Fail(
            string.IsNullOrWhiteSpace(detalle)
                ? "No se pudo leer la configuración de nginx: `nginx -T` necesita root y los "
                  + "archivos de /etc/nginx no son legibles con este usuario."
                : $"No se pudo leer la configuración de nginx: {detalle.Trim()}");
    }

    private static string Recortar(string texto, int maximo = 400)
    {
        var limpio = (texto ?? string.Empty).Trim().ReplaceLineEndings(" | ");

        return limpio.Length <= maximo ? limpio : limpio[..maximo] + "…";
    }

    private static string EntreComillas(string texto) => ShellPosix.EntreComillas(texto);

    private static string PrimeroNoVacio(params string[] candidatos) =>
        Array.Find(candidatos, c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;

    public async Task<InventoryResult<string>> GetNginxConfigAsync(
        string archivo, CancellationToken ct = default)
    {
        var (ok, salida, error) = await Medir(RemoteWork.Nginx, () => _runner.RunWithSudoAsync(
            $"cat {EntreComillas(archivo)}", _timeout, ct)).ConfigureAwait(false);

        return ok
            ? InventoryResult<string>.Ok(salida)
            : InventoryResult<string>.Fail($"No se pudo leer {archivo}: {error.Trim()}");
    }

    public async Task<InventoryResult<IReadOnlyList<SupervisorProcess>>> GetSupervisorAsync(
        CancellationToken ct = default)
    {
        var (ok, salida, error) = await Medir(
            RemoteWork.Supervisor, () => _runner.RunWithSudoAsync(
                $"{_supervisorctl} status", _timeout, ct)).ConfigureAwait(false);

        // supervisorctl devuelve 3 cuando hay procesos detenidos: no es un error de consulta.
        var procesos = SupervisorStatusParser.Parse(salida);

        // «0 proceso(s), todos corriendo» mostraba sano un panel cuya consulta no había podido leer nada.
        if (procesos.Count == 0)
        {
            var crudo = PrimeroNoVacio(error, salida).Trim();

            _logger?.TechnicalError(
                $"supervisorctl no devolvió procesos. Comando: {_supervisorctl}. "
                + $"Salida: {Recortar(salida)} Error: {Recortar(error)}",
                new InvalidOperationException("supervisorctl sin procesos"));

            return InventoryResult<IReadOnlyList<SupervisorProcess>>.Fail(
                string.IsNullOrWhiteSpace(crudo)
                    ? $"supervisord no devolvió ningún proceso, y tampoco un error. "
                      + $"Comando usado: `{_supervisorctl} status`."
                    : $"No se pudo leer supervisord con `{_supervisorctl} status`: "
                      + TraducirSupervisor(crudo));
        }

        return InventoryResult<IReadOnlyList<SupervisorProcess>>.Ok(procesos);
    }

    public async Task<InventoryResult<string>> GetSupervisorLogAsync(
        string proceso, int lineas = 4000, CancellationToken ct = default)
    {
        if (!ControlDeSupervisor.EsNombreValido(proceso))
        {
            return InventoryResult<string>.Fail(
                $"El nombre «{proceso}» no parece un proceso de supervisord y no se envió nada.");
        }

        var (ok, salida, error) = await Medir(
            RemoteWork.Supervisor, () => _runner.RunWithSudoAsync(
                $"{_supervisorctl} tail -{lineas} {proceso} stderr", _timeout, ct))
            .ConfigureAwait(false);

        if (TieneContenido(salida))
        {
            return InventoryResult<string>.Ok(salida);
        }

        var (okSalida, texto, errorSalida) = await Medir(
            RemoteWork.Supervisor, () => _runner.RunWithSudoAsync(
                $"{_supervisorctl} tail -{lineas} {proceso} stdout", _timeout, ct))
            .ConfigureAwait(false);

        if (TieneContenido(texto))
        {
            return InventoryResult<string>.Ok(texto);
        }

        if (SinArchivoDeRegistro(salida) && SinArchivoDeRegistro(texto))
        {
            return InventoryResult<string>.Ok(
                $"El proceso «{proceso}» no tiene archivo de registro configurado en "
                + "supervisord, ni para la salida ni para el error." + Environment.NewLine
                + Environment.NewLine
                + "Se arregla del lado del servidor, en la sección [program:" + proceso + "] de "
                + "su configuración: definir stdout_logfile y, si corresponde, stderr_logfile "
                + "—o redirect_stderr=true para que todo vaya al de salida—.");
        }

        var detalle = PrimeroNoVacio(error, errorSalida).Trim();

        return ok || okSalida
            ? InventoryResult<string>.Ok("El registro de este proceso está vacío.")
            : InventoryResult<string>.Fail(
                string.IsNullOrWhiteSpace(detalle)
                    ? "No se pudo leer el registro del proceso."
                    : $"No se pudo leer el registro: {TraducirSupervisor(detalle)}");
    }

    /// <summary><c>supervisorctl tail</c> escribe sus errores en la salida estándar, no en la de error.</summary>
    private static bool TieneContenido(string salida) =>
        !string.IsNullOrWhiteSpace(salida) && !EsQuejaDeSupervisor(salida);

    private static bool EsQuejaDeSupervisor(string salida)
    {
        var texto = salida.Trim();

        // Una sola línea que empieza con ERROR es la queja; un registro real trae muchas más.
        return texto.Contains("ERROR (", StringComparison.Ordinal)
               && texto.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length <= 2;
    }

    private static bool SinArchivoDeRegistro(string salida) =>
        salida.Contains("no log file", StringComparison.OrdinalIgnoreCase);

    private static string TraducirDocker(string error)
    {
        if (error.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            return "El usuario remoto no tiene permisos sobre Docker. " +
                   "Agregalo al grupo 'docker' o habilitá sudo sin contraseña para él.";
        }

        if (error.Contains("Cannot connect to the Docker daemon", StringComparison.OrdinalIgnoreCase))
        {
            return "El servicio de Docker no está corriendo en el servidor.";
        }

        return string.IsNullOrWhiteSpace(error)
            ? "No se pudo consultar Docker."
            : $"No se pudo consultar Docker: {error.Trim()}";
    }

    // Sin permiso sobre el socket contesta con un volcado de la biblioteca de Python.
    private static string TraducirSupervisor(string error)
    {
        if (error.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || error.Contains("SocketPermissionError", StringComparison.Ordinal))
        {
            return "El usuario remoto no tiene permiso sobre el socket de supervisord. "
                   + "Agregalo al grupo dueño del socket o habilitale sudo para supervisorctl.";
        }

        if (error.Contains("refused connection", StringComparison.OrdinalIgnoreCase)
            || error.Contains("no such file", StringComparison.OrdinalIgnoreCase))
        {
            return "supervisord no está atendiendo en el socket que declara su configuración. "
                   + "Puede estar parado, o corriendo con otro archivo de configuración.";
        }

        if (error.Contains("sudo", StringComparison.OrdinalIgnoreCase)
            && error.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return "sudo pidió contraseña y la guardada no sirvió para este servidor.";
        }

        return error;
    }
}
