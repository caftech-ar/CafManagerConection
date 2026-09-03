using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class MonitorDeProcesosTests
{
    private sealed class RelojFalso(DateTimeOffset arranque) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = arranque;

        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class RunnerFalso : IRemoteCommandRunner
    {
        public int Llamadas { get; private set; }

        public string UltimoComando { get; private set; } = string.Empty;

        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            Llamadas++;
            UltimoComando = command;

            return Task.FromResult((true, Salida(), string.Empty));
        }
    }

    private static string Salida() =>
        "100" + Tramo() + "4096" + Tramo() + "root:0" + Tramo() + "9000.00 8000.00" + Tramo()
        + "0 /proc/907" + Tramo()
        + "907 (malva) S 730 907 730 0 -1 4194304 512 0 0 0 40 0 0 0 20 0 82 0 500 "
        + "9999999 1024 18446744073709551615 0 0 0 0 0 0 0 0 0 0 0 0 17 1 0 0 0 0";

    private static string Tramo() => "\n" + ParserDeProcesos.Marca + "\n";

    private static (MonitorDeProcesos Monitor, RunnerFalso Runner, RelojFalso Reloj) Armar()
    {
        var runner = new RunnerFalso();
        var reloj = new RelojFalso(DateTimeOffset.UnixEpoch);

        return (new MonitorDeProcesos(new ColectorDeProcesos(runner, reloj), reloj), runner, reloj);
    }

    [Fact]
    public async Task La_primera_muestra_lee_del_servidor()
    {
        var (monitor, runner, _) = Armar();

        Assert.NotNull(await monitor.MuestraAsync(TimeSpan.FromSeconds(5), 10));
        Assert.Equal(1, runner.Llamadas);
        Assert.Equal(1, monitor.Lecturas);
    }

    // SC-050a: con el panel de estado y el de procesos abiertos, la muestra se comparte y el servidor lee una sola vez.
    [Fact]
    public async Task Dos_paneles_dentro_de_la_frescura_comparten_una_sola_lectura()
    {
        var (monitor, runner, reloj) = Armar();

        await monitor.MuestraAsync(TimeSpan.FromSeconds(5), 10);
        reloj.Ahora = reloj.Ahora.AddSeconds(1);
        await monitor.MuestraAsync(TimeSpan.FromSeconds(5), 10);

        Assert.Equal(1, runner.Llamadas);
    }

    [Fact]
    public async Task Pasada_la_frescura_vuelve_a_leer()
    {
        var (monitor, runner, reloj) = Armar();

        await monitor.MuestraAsync(TimeSpan.FromSeconds(5), 10);
        reloj.Ahora = reloj.Ahora.AddSeconds(6);
        await monitor.MuestraAsync(TimeSpan.FromSeconds(5), 10);

        Assert.Equal(2, runner.Llamadas);
    }

    [Fact]
    public async Task Dos_pedidos_a_la_vez_esperan_la_misma_lectura()
    {
        var (monitor, runner, _) = Armar();

        var uno = monitor.MuestraAsync(TimeSpan.Zero, 10);
        var otro = monitor.MuestraAsync(TimeSpan.Zero, 10);

        await Task.WhenAll(uno, otro);

        Assert.Equal(1, runner.Llamadas);
        Assert.Same(await uno, await otro);
    }

    [Fact]
    public async Task La_ultima_muestra_queda_a_mano_sin_volver_al_servidor()
    {
        var (monitor, runner, _) = Armar();

        var filas = await monitor.MuestraAsync(TimeSpan.FromSeconds(5), 10);

        Assert.Same(filas, monitor.Ultima);
        Assert.Equal(1, runner.Llamadas);
    }

    [Fact]
    public async Task Olvidar_borra_la_muestra_y_la_siguiente_vuelve_a_leer()
    {
        var (monitor, runner, _) = Armar();

        await monitor.MuestraAsync(TimeSpan.FromSeconds(30), 10);
        monitor.Olvidar();

        Assert.Null(monitor.Instante);
        Assert.Null(monitor.Ultima);

        await monitor.MuestraAsync(TimeSpan.FromSeconds(30), 10);

        Assert.Equal(2, runner.Llamadas);
    }

    [Fact]
    public async Task Sin_ejecutor_privilegiado_el_monitor_no_ofrece_escalar()
    {
        var (monitor, _, _) = Armar();

        Assert.False(monitor.PuedeEscalar);

        await monitor.MuestraAsync(TimeSpan.Zero, 10);

        Assert.False(monitor.ConPrivilegios);
    }

    [Fact]
    public async Task Con_privilegios_la_lectura_va_por_el_ejecutor_elevado()
    {
        var comun = new RunnerFalso();
        var elevado = new RunnerFalso();
        var reloj = new RelojFalso(DateTimeOffset.UnixEpoch);

        var monitor = new MonitorDeProcesos(
            new ColectorDeProcesos(comun, reloj, elevado), reloj);

        Assert.True(monitor.PuedeEscalar);

        monitor.ConPrivilegios = true;
        await monitor.MuestraAsync(TimeSpan.Zero, 10);

        Assert.Equal(0, comun.Llamadas);
        Assert.Equal(1, elevado.Llamadas);
    }
}
