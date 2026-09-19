using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Monitoring.Tests;

public sealed class ColectorDeFranjaTests
{
    private sealed class EjecutorFalso : IEjecutorRemoto
    {
        private readonly Queue<string> _respuestas;

        public EjecutorFalso(params string[] respuestas) => _respuestas = new(respuestas);

        public List<string> Comandos { get; } = [];

        public bool Falla { get; set; }

        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            Comandos.Add(command);

            return Task.FromResult(Falla
                ? (false, string.Empty, "sin canal")
                : (true, _respuestas.Count > 0 ? _respuestas.Dequeue() : string.Empty, string.Empty));
        }
    }

    private const string Marca = "###CMC###";

    private static string Vuelta(long idle, long total, long memLibre = 8_000_000) => string.Join(
        Marca,
        $"cpu  {total - idle} 0 0 {idle} 0 0 0 0 0 0\ncpu0 1 1 1 1 1 1 1 1 1 1",
        $"MemTotal: 16000000 kB\nMemAvailable: {memLibre} kB\nMemFree: {memLibre} kB",
        "0.80 0.50 0.40 1/200 1234",
        "123456.78 987654.32",
        "Inter-|   Receive  |  Transmit\n face |bytes\n  eth0: 1000 0 0 0 0 0 0 0 2000 0 0 0 0 0 0 0");

    private static string ConDisco(string vuelta) =>
        vuelta + Marca
        + "Filesystem Type 1B-blocks Used Available Use% Mounted on\n"
        + "/dev/sda1 ext4 100000000 93000000 7000000 93% /var\n"
        + "/dev/sda2 ext4 100000000 40000000 60000000 40% /";

    private static ColectorDeFranja Colector(EjecutorFalso ejecutor, out FakeTimeProvider reloj)
    {
        reloj = new FakeTimeProvider();
        return new ColectorDeFranja(ejecutor, reloj);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _ahora = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan cuanto) => _ahora = _ahora.Add(cuanto);
    }

    [Fact]
    public async Task La_primera_muestra_no_trae_cpu()
    {
        var ejecutor = new EjecutorFalso(ConDisco(Vuelta(900, 1000)));
        var colector = Colector(ejecutor, out _);

        var muestra = await colector.MuestrearAsync(5);

        Assert.NotNull(muestra);
        Assert.Null(muestra.Cpu);
    }

    [Fact]
    public async Task La_segunda_muestra_calcula_la_cpu_por_diferencia()
    {
        var ejecutor = new EjecutorFalso(
            ConDisco(Vuelta(900, 1000)), Vuelta(1800, 2000));

        var colector = Colector(ejecutor, out var reloj);

        await colector.MuestrearAsync(5);
        reloj.Avanzar(TimeSpan.FromSeconds(5));
        var segunda = await colector.MuestrearAsync(5);

        Assert.NotNull(segunda);
        Assert.NotNull(segunda.Cpu);
    }

    [Fact]
    public async Task El_comando_de_vuelta_no_trae_lo_caro()
    {
        var ejecutor = new EjecutorFalso(ConDisco(Vuelta(900, 1000)), Vuelta(1800, 2000));
        var colector = Colector(ejecutor, out _);

        await colector.MuestrearAsync(5);
        await colector.MuestrearAsync(5);

        var segundo = ejecutor.Comandos[1];

        Assert.DoesNotContain("lscpu", segundo, StringComparison.Ordinal);
        Assert.DoesNotContain("sensors", segundo, StringComparison.Ordinal);
        Assert.DoesNotContain("systemctl", segundo, StringComparison.Ordinal);
        Assert.DoesNotContain("lsblk", segundo, StringComparison.Ordinal);
        Assert.DoesNotContain("ip route", segundo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_disco_se_lee_en_la_primera_vuelta_y_no_en_las_siguientes()
    {
        var respuestas = new List<string> { ConDisco(Vuelta(900, 1000)) };
        respuestas.AddRange(Enumerable.Repeat(Vuelta(1800, 2000), 5));

        var ejecutor = new EjecutorFalso([.. respuestas]);
        var colector = Colector(ejecutor, out _);

        for (var i = 0; i < 6; i++)
        {
            await colector.MuestrearAsync(5);
        }

        Assert.Contains("df", ejecutor.Comandos[0], StringComparison.Ordinal);
        Assert.All(
            ejecutor.Comandos.Skip(1),
            c => Assert.DoesNotContain("df", c, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Entre_lecturas_de_disco_se_conserva_el_ultimo_valor()
    {
        var ejecutor = new EjecutorFalso(ConDisco(Vuelta(900, 1000)), Vuelta(1800, 2000));
        var colector = Colector(ejecutor, out _);

        await colector.MuestrearAsync(5);
        var segunda = await colector.MuestrearAsync(5);

        Assert.NotNull(segunda);
        Assert.NotNull(segunda.PeorDisco);
        Assert.Equal("/var", segunda.PeorDisco.MountPoint);
    }

    [Fact]
    public async Task El_peor_disco_es_el_mas_ocupado()
    {
        var ejecutor = new EjecutorFalso(ConDisco(Vuelta(900, 1000)));
        var colector = Colector(ejecutor, out _);

        var muestra = await colector.MuestrearAsync(5);

        Assert.NotNull(muestra?.PeorDisco);
        Assert.Equal("/var", muestra.PeorDisco.MountPoint);
        Assert.True(muestra.PeorDisco.UsedPercent > 90);
    }

    [Fact]
    public async Task Una_lectura_fallida_devuelve_nulo_y_conserva_la_anterior()
    {
        var ejecutor = new EjecutorFalso(ConDisco(Vuelta(900, 1000)));
        var colector = Colector(ejecutor, out _);

        var buena = await colector.MuestrearAsync(5);
        ejecutor.Falla = true;

        Assert.Null(await colector.MuestrearAsync(5));
        Assert.Equal(buena, colector.Ultima);
    }

    [Fact]
    public async Task La_identidad_se_lee_con_su_propio_comando()
    {
        var ejecutor = new EjecutorFalso("PRETTY_NAME=\"Ubuntu 22.04.3 LTS\"");
        var colector = Colector(ejecutor, out _);

        await colector.LeerIdentidadAsync(5);

        Assert.Equal("Ubuntu 22.04", colector.Distribucion);
        Assert.Single(ejecutor.Comandos);
        Assert.Contains("os-release", ejecutor.Comandos[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_identidad_no_entra_en_el_comando_periodico()
    {
        var ejecutor = new EjecutorFalso(ConDisco(Vuelta(900, 1000)));
        var colector = Colector(ejecutor, out _);

        await colector.MuestrearAsync(5);

        Assert.DoesNotContain("os-release", ejecutor.Comandos[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_discos_no_hay_peor() => Assert.Null(ColectorDeFranja.PeorDe([]));
}
