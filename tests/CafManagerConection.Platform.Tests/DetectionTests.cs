using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class DetectionTests
{
    private sealed class RunnerFalso(bool exito, string salida) : IPlatformCommandRunner
    {
        public string? ComandoRecibido { get; private set; }

        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            ComandoRecibido = command;
            return Task.FromResult((exito, salida, string.Empty));
        }

        public Task<(bool Success, string Output, string Error)> RunWithSudoAsync(
            string command, int timeoutSeconds, CancellationToken ct = default) =>
            RunAsync(command, timeoutSeconds, ct);
    }

    private static async Task<ServerCapabilities> Detectar(string salida, bool exito = true)
    {
        var inventario = new PlatformInventory(new RunnerFalso(exito, salida));
        return await inventario.DetectAsync();
    }

    [Fact]
    public async Task Un_servidor_sin_supervisord_no_pierde_las_demas_capacidades()
    {
        var caps = await Detectar("cmc:linux\ncmc:docker=ok\ncmc:nginx=ok\ncmc:supervisord=no\n");

        Assert.True(caps.IsLinux);
        Assert.True(caps.HasDocker);
        Assert.True(caps.HasNginx);
        Assert.False(caps.HasSupervisord);
    }

    [Fact]
    public async Task El_guion_termina_en_exit_0_para_no_depender_del_ultimo_comando()
    {
        var runner = new RunnerFalso(true, "cmc:linux\n");
        await new PlatformInventory(runner).DetectAsync();

        Assert.NotNull(runner.ComandoRecibido);
        Assert.EndsWith("exit 0", runner.ComandoRecibido.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_guion_viaja_sin_retornos_de_carro()
    {
        var runner = new RunnerFalso(true, "cmc:linux\n");
        await new PlatformInventory(runner).DetectAsync();

        Assert.NotNull(runner.ComandoRecibido);
        Assert.DoesNotContain('\r', runner.ComandoRecibido);
    }

    [Fact]
    public async Task El_guion_amplia_el_PATH_porque_docker_y_nginx_suelen_quedar_afuera()
    {
        var runner = new RunnerFalso(true, "cmc:linux\n");
        await new PlatformInventory(runner).DetectAsync();

        Assert.Contains("/usr/local/bin", runner.ComandoRecibido);
        Assert.Contains("/usr/sbin", runner.ComandoRecibido);
    }

    [Fact]
    public async Task Docker_no_instalado_no_ofrece_el_panel()
    {
        var caps = await Detectar("cmc:linux\ncmc:docker=no\n");

        Assert.False(caps.HasDocker);
    }

    [Fact]
    public async Task Docker_sin_permiso_igual_ofrece_el_panel()
    {
        var caps = await Detectar("cmc:linux\ncmc:docker=permiso\n");

        Assert.True(caps.HasDocker);
        Assert.True(caps.DockerNeedsSudo);
    }

    [Fact]
    public async Task Docker_sin_sudo_no_queda_marcado_como_que_lo_necesita()
    {
        var caps = await Detectar("cmc:linux\ncmc:docker=ok\n");

        Assert.True(caps.HasDocker);
        Assert.False(caps.DockerNeedsSudo);
        Assert.Equal(ServiceState.Available, caps.Docker);
    }

    [Fact]
    public async Task El_retorno_de_carro_de_algunos_servidores_no_rompe_las_marcas()
    {
        var caps = await Detectar("cmc:linux\r\ncmc:docker=ok\r\ncmc:supervisord=ok\r\n");

        Assert.True(caps.IsLinux);
        Assert.True(caps.HasDocker);
        Assert.True(caps.HasSupervisord);
    }

    [Fact]
    public async Task Un_servidor_que_no_es_Linux_no_declara_ninguna_capacidad()
    {
        var caps = await Detectar(string.Empty);

        Assert.False(caps.IsLinux);
        Assert.False(caps.HasDocker);
        Assert.False(caps.HasNginx);
        Assert.False(caps.HasSupervisord);
    }

    [Fact]
    public async Task Si_el_comando_no_se_pudo_ejecutar_no_se_declara_nada()
    {
        var caps = await Detectar("cmc:linux\ncmc:docker=ok\n", exito: false);

        Assert.Equal(ServerCapabilities.None, caps);
    }

    [Fact]
    public async Task Una_marca_parecida_pero_distinta_no_cuenta()
    {
        var caps = await Detectar("cmc:linux\ncmc:dockerfoo\nalgo cmc:nginx mas\n");

        Assert.True(caps.IsLinux);
        Assert.False(caps.HasDocker);
        Assert.False(caps.HasNginx);
    }

    private sealed class RunnerEnDosPasos(string deteccion) : IPlatformCommandRunner
    {
        public string? UltimoComando { get; private set; }

        private bool _yaDetecto;

        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            UltimoComando = command;

            if (!_yaDetecto)
            {
                _yaDetecto = true;
                return Task.FromResult((true, deteccion, string.Empty));
            }

            return Task.FromResult((true, string.Empty, string.Empty));
        }

        public Task<(bool Success, string Output, string Error)> RunWithSudoAsync(
            string command, int timeoutSeconds, CancellationToken ct = default) =>
            RunAsync(command, timeoutSeconds, ct);
    }

    [Fact]
    public async Task La_consulta_usa_el_supervisorctl_que_resolvio_la_deteccion()
    {
        const string Resuelto = "/opt/app/venv/bin/supervisorctl -c /opt/app/supervisord.conf";

        var runner = new RunnerEnDosPasos(
            $"cmc:linux\ncmc:supctl={Resuelto}\ncmc:supervisord=ok\n");

        var inventario = new PlatformInventory(runner);
        await inventario.DetectAsync();
        await inventario.GetSupervisorAsync();

        Assert.Equal($"{Resuelto} status", runner.UltimoComando);
    }

    [Fact]
    public async Task Sin_marca_resuelta_la_consulta_usa_supervisorctl_a_secas()
    {
        var runner = new RunnerEnDosPasos("cmc:linux\ncmc:supervisord=ok\n");

        var inventario = new PlatformInventory(runner);
        await inventario.DetectAsync();
        await inventario.GetSupervisorAsync();

        Assert.Equal("supervisorctl status", runner.UltimoComando);
    }
}
