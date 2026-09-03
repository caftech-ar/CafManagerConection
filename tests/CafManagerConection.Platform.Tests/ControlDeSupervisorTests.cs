using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class ControlDeSupervisorTests
{
    private sealed class RunnerFalso(bool exito = true, string salida = "listo")
        : IPlatformCommandRunner
    {
        public List<string> Comandos { get; } = [];

        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            Comandos.Add(command);
            return Task.FromResult((exito, salida, string.Empty));
        }

        public Task<(bool Success, string Output, string Error)> RunWithSudoAsync(
            string command, int timeoutSeconds, CancellationToken ct = default) =>
            RunAsync(command, timeoutSeconds, ct);
    }

    private static ControlDeSupervisor Control(RunnerFalso runner, string ctl = "supervisorctl") =>
        new(runner, ctl);

    [Theory]
    [InlineData(AccionDeProceso.Iniciar, "start")]
    [InlineData(AccionDeProceso.Detener, "stop")]
    [InlineData(AccionDeProceso.Reiniciar, "restart")]
    public async Task Cada_accion_manda_su_verbo(AccionDeProceso accion, string verbo)
    {
        var runner = new RunnerFalso();

        await Control(runner).EjecutarAsync(accion, "operador-inventario");

        Assert.Equal($"supervisorctl {verbo} operador-inventario", Assert.Single(runner.Comandos));
    }

    [Fact]
    public async Task Usa_el_comando_resuelto_con_su_configuracion()
    {
        var runner = new RunnerFalso();
        var ctl = "/opt/app/venv/bin/supervisorctl -c /opt/app/supervisord.conf";

        await Control(runner, ctl).EjecutarAsync(AccionDeProceso.Reiniciar, "api");

        Assert.Equal($"{ctl} restart api", Assert.Single(runner.Comandos));
    }

    [Theory]
    [InlineData("operador-inventario")]
    [InlineData("zona-sur-inventario-to")]
    [InlineData("grupo:proceso")]
    [InlineData("grupo:*")]
    [InlineData("app_worker.1")]
    public void Los_nombres_de_supervisor_se_aceptan(string nombre) =>
        Assert.True(ControlDeSupervisor.EsNombreValido(nombre));

    [Theory]
    [InlineData("api; rm -rf /")]
    [InlineData("api && reboot")]
    [InlineData("api | tee /etc/passwd")]
    [InlineData("api$(whoami)")]
    [InlineData("api`id`")]
    [InlineData("api\nreboot")]
    [InlineData("api 'algo'")]
    [InlineData("../../etc/passwd")]
    [InlineData("")]
    [InlineData(null)]
    public void Un_nombre_con_sintaxis_de_shell_se_rechaza(string? nombre) =>
        Assert.False(ControlDeSupervisor.EsNombreValido(nombre));

    [Fact]
    public async Task Un_nombre_invalido_no_llega_a_ejecutar_nada()
    {
        var runner = new RunnerFalso();

        var r = await Control(runner).EjecutarAsync(AccionDeProceso.Detener, "api; reboot");

        Assert.False(r.Success);
        Assert.Empty(runner.Comandos);
    }

    [Fact]
    public void Un_nombre_larguisimo_se_rechaza() =>
        Assert.False(ControlDeSupervisor.EsNombreValido(new string('a', 200)));

    [Fact]
    public async Task Una_respuesta_con_estado_no_cero_se_devuelve_igual()
    {
        var runner = new RunnerFalso(exito: false, salida: "api: ERROR (already started)");

        var r = await Control(runner).EjecutarAsync(AccionDeProceso.Iniciar, "api");

        Assert.True(r.Success);
        Assert.Equal("api: ERROR (already started)", r.Value);
    }

    [Fact]
    public async Task Un_fallo_sin_ninguna_respuesta_si_es_un_fallo()
    {
        var runner = new RunnerFalso(exito: false, salida: string.Empty);

        var r = await Control(runner).EjecutarAsync(AccionDeProceso.Detener, "api");

        Assert.False(r.Success);
    }
}
