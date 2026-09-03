using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

// Defecto medido contra servidor-uno: supervisorctl tail escribe sus errores en stdout, no en stderr.
public sealed class RegistroDeSupervisorTests
{
    private sealed class RunnerPorCanal(string stderr, string stdout) : IPlatformCommandRunner
    {
        public List<string> Comandos { get; } = [];

        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            Comandos.Add(command);

            var salida = command.EndsWith("stderr", StringComparison.Ordinal) ? stderr : stdout;

            return Task.FromResult((true, salida, string.Empty));
        }

        public Task<(bool Success, string Output, string Error)> RunWithSudoAsync(
            string command, int timeoutSeconds, CancellationToken ct = default) =>
            RunAsync(command, timeoutSeconds, ct);
    }

    private static PlatformInventory Inventario(IPlatformCommandRunner runner) => new(runner);

    [Fact]
    public async Task El_registro_del_canal_de_error_se_devuelve()
    {
        var runner = new RunnerPorCanal("Traceback: se cayo feo", "otra cosa");

        var r = await Inventario(runner).GetSupervisorLogAsync("api");

        Assert.True(r.Success);
        Assert.Equal("Traceback: se cayo feo", r.Value);
    }

    [Fact]
    public async Task Sin_archivo_en_el_canal_de_error_se_prueba_el_de_salida()
    {
        var runner = new RunnerPorCanal(
            "operador-inventario-to: ERROR (no log file)",
            "arranco y se cayo a los dos segundos");

        var r = await Inventario(runner).GetSupervisorLogAsync("operador-inventario-to");

        Assert.True(r.Success);
        Assert.Equal("arranco y se cayo a los dos segundos", r.Value);
        Assert.DoesNotContain("ERROR (no log file)", r.Value!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_archivo_en_ninguno_se_explica_en_lugar_de_mostrar_el_error_crudo()
    {
        var runner = new RunnerPorCanal(
            "api: ERROR (no log file)", "api: ERROR (no log file)");

        var r = await Inventario(runner).GetSupervisorLogAsync("api");

        Assert.True(r.Success);
        Assert.DoesNotContain("ERROR (no log file)", r.Value!, StringComparison.Ordinal);
        Assert.Contains("no tiene archivo de registro", r.Value!, StringComparison.Ordinal);

        Assert.Contains("stdout_logfile", r.Value!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Se_consultan_los_dos_canales_y_en_ese_orden()
    {
        var runner = new RunnerPorCanal("api: ERROR (no log file)", "api: ERROR (no log file)");

        await Inventario(runner).GetSupervisorLogAsync("api");

        Assert.Equal(2, runner.Comandos.Count);
        Assert.EndsWith("stderr", runner.Comandos[0], StringComparison.Ordinal);
        Assert.EndsWith("stdout", runner.Comandos[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_registro_que_menciona_ERROR_no_se_confunde_con_una_queja()
    {
        var registro =
            "2026-08-26 10:00:01 INFO arrancando\n"
            + "2026-08-26 10:00:02 ERROR (no se pudo abrir la base)\n"
            + "2026-08-26 10:00:03 INFO reintentando\n";

        var runner = new RunnerPorCanal(registro, string.Empty);

        var r = await Inventario(runner).GetSupervisorLogAsync("api");

        Assert.True(r.Success);
        Assert.Equal(registro, r.Value);
    }

    [Fact]
    public async Task Los_dos_canales_vacios_se_informan_como_vacio()
    {
        var runner = new RunnerPorCanal(string.Empty, string.Empty);

        var r = await Inventario(runner).GetSupervisorLogAsync("api");

        Assert.True(r.Success);
        Assert.Contains("vacío", r.Value!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_nombre_con_sintaxis_de_shell_no_llega_a_ejecutar_nada()
    {
        var runner = new RunnerPorCanal("algo", "algo");

        var r = await Inventario(runner).GetSupervisorLogAsync("api; rm -rf /");

        Assert.False(r.Success);
        Assert.Empty(runner.Comandos);
    }
}
