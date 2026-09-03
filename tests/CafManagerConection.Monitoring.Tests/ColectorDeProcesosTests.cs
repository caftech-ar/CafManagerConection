using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class ColectorDeProcesosTests
{
    private sealed class RelojFalso(DateTimeOffset arranque) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = arranque;

        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class RunnerFalso(params string[] salidas) : IRemoteCommandRunner
    {
        private int _vuelta;

        public int Llamadas { get; private set; }

        public bool Exito { get; set; } = true;

        public string Error { get; set; } = string.Empty;

        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            Llamadas++;
            var salida = salidas[Math.Min(_vuelta++, salidas.Length - 1)];

            return Task.FromResult((Exito, salida, Error));
        }
    }

    private static string Tramo() => "\n" + ParserDeProcesos.Marca + "\n";

    private static string Salida(long tics) =>
        "100" + Tramo() + "4096" + Tramo() + "malva:1000" + Tramo() + "9000.0 8000.0" + Tramo()
        + "1000 /proc/907" + Tramo()
        + $"907 (malva) S 730 907 730 0 -1 4194304 512 0 0 0 {tics} 0 0 0 20 0 82 0 500 "
        + "9999999 1024 18446744073709551615 0 0 0 0 0 0 0 0 0 0 0 0 17 1 0 0 0 0";

    [Fact]
    public async Task La_primera_medicion_no_informa_porcentaje()
    {
        var colector = new ColectorDeProcesos(
            new RunnerFalso(Salida(0)), new RelojFalso(DateTimeOffset.UnixEpoch));

        var filas = await colector.MedirAsync(10);

        Assert.NotNull(filas);
        Assert.Null(Assert.Single(filas).PorcentajeDeCpu);
        Assert.True(colector.TienePorcentajes);
    }

    [Fact]
    public async Task La_segunda_medicion_mide_contra_la_primera()
    {
        var reloj = new RelojFalso(DateTimeOffset.UnixEpoch);
        var colector = new ColectorDeProcesos(new RunnerFalso(Salida(0), Salida(341)), reloj);

        await colector.MedirAsync(10);
        reloj.Ahora = reloj.Ahora.AddSeconds(1);
        var filas = await colector.MedirAsync(10);

        Assert.NotNull(filas);
        Assert.Equal(341, Assert.Single(filas).PorcentajeDeCpu!.Value, precision: 6);
    }

    [Fact]
    public async Task Olvidar_la_muestra_anterior_deja_la_siguiente_sin_porcentaje()
    {
        var reloj = new RelojFalso(DateTimeOffset.UnixEpoch);
        var colector = new ColectorDeProcesos(new RunnerFalso(Salida(0), Salida(341)), reloj);

        await colector.MedirAsync(10);
        colector.Olvidar();
        reloj.Ahora = reloj.Ahora.AddSeconds(1);
        var filas = await colector.MedirAsync(10);

        Assert.NotNull(filas);
        Assert.Null(Assert.Single(filas).PorcentajeDeCpu);
    }

    [Fact]
    public async Task Un_canal_caido_deja_el_motivo_y_no_una_lista_vacia()
    {
        var runner = new RunnerFalso(string.Empty) { Exito = false, Error = "canal cerrado" };
        var colector = new ColectorDeProcesos(runner, new RelojFalso(DateTimeOffset.UnixEpoch));

        Assert.Null(await colector.MedirAsync(10));
        Assert.Equal("canal cerrado", colector.UltimoError);
    }

    [Fact]
    public async Task Un_servidor_sin_proc_dice_que_puede_no_ser_Linux()
    {
        var colector = new ColectorDeProcesos(
            new RunnerFalso(string.Empty), new RelojFalso(DateTimeOffset.UnixEpoch));

        Assert.Null(await colector.MedirAsync(10));
        Assert.Contains("/proc", colector.UltimoError);
    }

    [Fact]
    public async Task Una_medicion_pide_una_sola_lectura_al_servidor()
    {
        var runner = new RunnerFalso(Salida(0));
        var colector = new ColectorDeProcesos(runner, new RelojFalso(DateTimeOffset.UnixEpoch));

        await colector.MedirAsync(10);

        Assert.Equal(1, runner.Llamadas);
    }
}
