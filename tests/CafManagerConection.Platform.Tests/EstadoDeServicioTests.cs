using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class EstadoDeServicioTests
{
    private sealed class RunnerFalso(string salida) : IPlatformCommandRunner
    {
        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default) =>
            Task.FromResult((true, salida, string.Empty));

        public Task<(bool Success, string Output, string Error)> RunWithSudoAsync(
            string command, int timeoutSeconds, CancellationToken ct = default) =>
            RunAsync(command, timeoutSeconds, ct);
    }

    private static async Task<ServerCapabilities> Detectar(params string[] marcas) =>
        await new PlatformInventory(new RunnerFalso(string.Join("\n", marcas) + "\n"))
            .DetectAsync();

    [Theory]
    [InlineData("no", ServiceState.NotInstalled)]
    [InlineData("parado", ServiceState.NotRunning)]
    [InlineData("permiso", ServiceState.NoPermission)]
    [InlineData("ok", ServiceState.Available)]
    public async Task Cada_marca_se_traduce_a_su_estado(string marca, ServiceState esperado)
    {
        var caps = await Detectar("cmc:linux", $"cmc:supervisord={marca}");

        Assert.Equal(esperado, caps.Supervisord);
    }

    [Fact]
    public async Task Un_servicio_sin_marca_se_da_por_no_instalado()
    {
        var caps = await Detectar("cmc:linux");

        Assert.Equal(ServiceState.NotInstalled, caps.Docker);
        Assert.Equal(ServiceState.NotInstalled, caps.Nginx);
        Assert.Equal(ServiceState.NotInstalled, caps.Supervisord);
    }

    [Fact]
    public async Task Los_tres_servicios_se_leen_de_forma_independiente()
    {
        var caps = await Detectar(
            "cmc:linux", "cmc:docker=ok", "cmc:nginx=parado", "cmc:supervisord=permiso");

        Assert.Equal(ServiceState.Available, caps.Docker);
        Assert.Equal(ServiceState.NotRunning, caps.Nginx);
        Assert.Equal(ServiceState.NoPermission, caps.Supervisord);
    }

    [Fact]
    public async Task Un_servicio_que_existe_pero_no_se_puede_consultar_igual_ofrece_su_panel()
    {
        var caps = await Detectar("cmc:linux", "cmc:supervisord=permiso");

        Assert.True(caps.HasSupervisord);
    }

    [Theory]
    [InlineData(ServiceState.NotInstalled, "no está instalado")]
    [InlineData(ServiceState.NotRunning, "no está corriendo")]
    [InlineData(ServiceState.NoPermission, "no puede consultarlo")]
    public void Cada_estado_tiene_una_explicacion_accionable(ServiceState estado, string esperado)
    {
        var texto = ServerCapabilities.Explicar("supervisord", estado);

        Assert.Contains(esperado, texto, StringComparison.Ordinal);
        Assert.Contains("supervisord", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_servicio_disponible_no_necesita_explicacion()
    {
        Assert.Empty(ServerCapabilities.Explicar("docker", ServiceState.Available));
    }

    [Fact]
    public async Task El_guion_consulta_si_el_proceso_corre_y_no_solo_si_el_binario_existe()
    {
        string? comando = null;

        var runner = new RunnerCaptura(c => comando = c);
        await new PlatformInventory(runner).DetectAsync();

        Assert.Contains("pgrep", comando!, StringComparison.Ordinal);
    }

    private sealed class RunnerCaptura(Action<string> capturar) : IPlatformCommandRunner
    {
        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            capturar(command);
            return Task.FromResult((true, "cmc:linux\n", string.Empty));
        }

        public Task<(bool Success, string Output, string Error)> RunWithSudoAsync(
            string command, int timeoutSeconds, CancellationToken ct = default) =>
            RunAsync(command, timeoutSeconds, ct);
    }
}
