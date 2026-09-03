using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Platform;

// Aparte de PlatformInventory: no existe ningún método que escriba, y no tiene acciones a propósito (FR-165c).
public sealed class ConsultorDeProcesos
{
    private readonly IPlatformCommandRunner _runner;
    private readonly IAppLogger? _logger;
    private readonly Guid _conexion;
    private readonly int _timeout;

    public ConsultorDeProcesos(
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

    /// <summary>Sólo dígitos y con tope: el PID vuelve a entrar en una línea que puede correr como root (FR-165b).</summary>
    public static bool EsPidValido(int pid) => pid is > 0 and <= 4194304;

    public async Task<InventoryResult<DetalleDeProceso>> LeerAsync(
        int pid, string nombre, CancellationToken ct = default)
    {
        if (!EsPidValido(pid))
        {
            return InventoryResult<DetalleDeProceso>.Fail(
                $"«{pid}» no es un identificador de proceso válido.");
        }

        _logger?.PlatformActionPerformed(_conexion, "ps");

        // Los errores van a la salida normal: sin permisos readlink escribe ahí el motivo (FR-165a).
        // user:32 y no user: con el ancho por omisión ps trunca el nombre a ocho caracteres y le agrega un +.
        // readlink -v y no readlink: sin -v no imprime nada cuando no tiene permiso, ni en la salida de error.
        var guion = string.Join('\n', [
            $"echo '{MarcaDeProceso.Ps}'",
            $"ps -p {pid} -o comm=,user:32=,etime=,ppid=,nlwp=,args= "
            + "| sed -E 's/^ *//' "
            + "| awk '{ printf \"%s|%s|%s|%s|%s|\", $1, $2, $3, $4, $5; "
            + "for (i = 6; i <= NF; i++) printf \"%s%s\", $i, (i < NF ? \" \" : \"\"); print \"\" }'",
            $"echo '{MarcaDeProceso.Binario}'",
            $"readlink -v -f /proc/{pid}/exe 2>&1",
            $"echo '{MarcaDeProceso.Directorio}'",
            $"readlink -v -f /proc/{pid}/cwd 2>&1",
        ]);

        var (ok, salida, error) = await _runner
            .RunWithSudoAsync(guion, _timeout, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(salida))
        {
            return InventoryResult<DetalleDeProceso>.Fail(
                string.IsNullOrWhiteSpace(error)
                    ? "El servidor no devolvió nada sobre ese proceso."
                    : error.Trim());
        }

        var detalle = DetalleDeProceso.Interpretar(pid, nombre, salida);

        if (!detalle.Existe && !detalle.TieneAlgo)
        {
            return InventoryResult<DetalleDeProceso>.Fail(
                ok
                    ? $"El proceso {pid} ya no existe en el servidor."
                    : "No se pudo consultar ese proceso.");
        }

        return InventoryResult<DetalleDeProceso>.Ok(detalle);
    }
}
