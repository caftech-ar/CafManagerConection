using System.Text.RegularExpressions;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Platform;

public enum AccionDeProceso
{
    Iniciar,
    Detener,
    Reiniciar,
}

// Aparte de PlatformInventory: la garantía de sólo lectura del inventario es que no tiene métodos de escritura (FR-100).
public sealed class ControlDeSupervisor
{
    private readonly IPlatformCommandRunner _runner;
    private readonly string _supervisorctl;
    private readonly int _timeout;
    private readonly IAppLogger? _logger;
    private readonly Guid _conexion;

    /// <param name="supervisorctl">Ya resuelto, con su <c>-c</c> si hace falta; lo averigua la detección del inventario.</param>
    public ControlDeSupervisor(
        IPlatformCommandRunner runner,
        string supervisorctl,
        int timeoutSeconds = 20,
        IAppLogger? logger = null,
        Guid connectionId = default)
    {
        _runner = runner;
        _supervisorctl = supervisorctl;
        _timeout = timeoutSeconds;
        _logger = logger;
        _conexion = connectionId;
    }

    /// <summary>El nombre viene de <c>supervisorctl status</c> y termina en una línea con sudo.</summary>
    // Los dos puntos y el asterisco se aceptan: supervisor nombra grupo:proceso y grupo:*.
    private static readonly Regex NombreValido = new(@"^[A-Za-z0-9_.:*-]{1,128}$", RegexOptions.Compiled);

    public static bool EsNombreValido(string? nombre) =>
        nombre is not null && NombreValido.IsMatch(nombre);

    public async Task<InventoryResult<string>> EjecutarAsync(
        AccionDeProceso accion, string proceso, CancellationToken ct = default)
    {
        if (!EsNombreValido(proceso))
        {
            return InventoryResult<string>.Fail(
                $"El nombre «{proceso}» no parece un proceso de supervisord y no se envió nada.");
        }

        var verbo = accion switch
        {
            AccionDeProceso.Iniciar => "start",
            AccionDeProceso.Detener => "stop",
            _ => "restart",
        };

        // Se registra la acción y no el nombre del proceso: es contenido de sesión (Principio II).
        _logger?.PlatformActionPerformed(_conexion, $"supervisorctl {verbo}");

        var (ok, salida, error) = await _runner
            .RunWithSudoAsync($"{_supervisorctl} {verbo} {proceso}", _timeout, ct)
            .ConfigureAwait(false);

        var texto = string.IsNullOrWhiteSpace(salida) ? error : salida;

        return string.IsNullOrWhiteSpace(texto)
            ? ok
                ? InventoryResult<string>.Ok("Listo.")
                : InventoryResult<string>.Fail("supervisord no contestó nada.")
            : InventoryResult<string>.Ok(texto.Trim());
    }
}
