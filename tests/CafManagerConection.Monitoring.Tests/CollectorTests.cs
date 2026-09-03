using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class CollectorTests
{
    private sealed class RunnerFalso(bool exito, string salida, string error = "")
        : IRemoteCommandRunner
    {
        public string? ComandoRecibido { get; private set; }

        public Task<(bool Success, string Output, string Error)> RunAsync(
            string command, int timeoutSeconds, CancellationToken ct = default)
        {
            ComandoRecibido = command;
            return Task.FromResult((exito, salida, error));
        }
    }

    /// <summary>Salida mínima con las secciones que el recolector espera, separadas por marca.</summary>
    private static string SalidaValida(string ultimaSeccion)
    {
        const string M = "###CMC###";

        return string.Join($"\n{M}\n",
        [
            "cpu  100 20 30 400 5 0 10 0 0 0",
            "MemTotal:       16000000 kB\nMemAvailable:   10000000 kB",
            "0.15 0.10 0.05 1/234 5678",
            "1234.56 9876.54",
            "Inter-|   Receive                    |  Transmit\n"
                + " face |bytes    packets\n"
                + "  eth0: 1000 10 0 0 0 0 0 0 2000 20 0 0 0 0 0 0",
            "/dev/sda1 100000000 60000000 40000000 60% /",
            "servidor-prueba",
            "PRETTY_NAME=\"Ubuntu 24.04.2 LTS\"",
            "6.8.0-60-generic",
            "2026-08-25T09:00:00-03:00",
            string.Empty,
            "334",
            ultimaSeccion,
        ]);
    }

    [Fact]
    public void La_marca_separadora_va_entre_comillas_para_no_ser_un_comentario()
    {
        var comando = MetricsCollector.ComandoDeLectura;

        Assert.Contains("echo '###CMC###'", comando);
        Assert.DoesNotContain("echo ###CMC###", comando);
    }

    [Fact]
    public void Ningun_tramo_del_comando_queda_vacio()
    {
        var comando = MetricsCollector.ComandoDeLectura;

        Assert.DoesNotContain("; ; ", comando);
        Assert.DoesNotContain("'; echo", comando.Replace("###CMC###'; echo", string.Empty));

        var tramos = comando.Split("; echo '###CMC###'; ");

        Assert.Equal(MetricsCollector.Tramos, tramos.Length);
        Assert.All(tramos, t => Assert.False(string.IsNullOrWhiteSpace(t)));
    }

    // FR-173d: el top salía de ps --sort=-pcpu, que informa el promedio de toda la vida del proceso; ahora sale de la muestra de /proc del ColectorDeProcesos.
    [Fact]
    public void El_estado_ya_no_pide_el_listado_de_procesos_a_ps()
    {
        var comando = MetricsCollector.ComandoDeLectura;

        Assert.DoesNotContain("--sort=-pcpu", comando);
        Assert.DoesNotContain("ps -eo ", comando);
    }

    /// <remarks>df traduce su encabezado; contra un servidor con locale en español desaparecían
    /// todos los discos.</remarks>
    [Fact]
    public void Los_comandos_cuya_salida_se_interpreta_por_texto_fijan_el_locale()
    {
        var comando = MetricsCollector.ComandoDeLectura;

        Assert.Contains("LC_ALL=C df ", comando);
        Assert.Contains("LC_ALL=C lscpu", comando);
        Assert.Contains("LC_ALL=C sensors", comando);
    }

    /// <remarks>FR-165e: la línea de comando de un proceso no se pide, por las contraseñas
    /// que lleva en los argumentos.</remarks>
    [Fact]
    public void El_comando_no_pide_la_linea_de_comando_de_ningun_proceso()
    {
        var comando = MetricsCollector.ComandoDeLectura;

        Assert.DoesNotContain(",args", comando);
        Assert.DoesNotContain(",cmd", comando);
        Assert.DoesNotContain("/cmdline", comando);
    }

    [Fact]
    public async Task El_comando_termina_en_exit_0_para_no_depender_de_systemctl()
    {
        var runner = new RunnerFalso(true, SalidaValida(string.Empty));
        await new MetricsCollector(runner).CollectAsync(10);

        Assert.NotNull(runner.ComandoRecibido);
        Assert.EndsWith("exit 0", runner.ComandoRecibido.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_lectura_completa_sin_systemctl_igual_devuelve_metricas()
    {
        var runner = new RunnerFalso(true, SalidaValida(string.Empty));

        var snapshot = await new MetricsCollector(runner).CollectAsync(10);

        Assert.NotNull(snapshot);
        Assert.Equal("servidor-prueba", snapshot.System.HostName);
        Assert.True(snapshot.Memory.TotalBytes > 0);
        Assert.Equal("Ubuntu 24.04.2 LTS", snapshot.System.Distribution);
    }

    [Fact]
    public async Task Los_servicios_fallados_se_leen_cuando_systemctl_existe()
    {
        var runner = new RunnerFalso(
            true, SalidaValida("nginx.service loaded failed failed A high performance web server"));

        var snapshot = await new MetricsCollector(runner).CollectAsync(10);

        Assert.NotNull(snapshot);
        Assert.Contains("nginx.service", snapshot.System.FailedServices);
    }

    [Fact]
    public async Task Si_el_comando_no_se_pudo_ejecutar_no_hay_lectura()
    {
        var runner = new RunnerFalso(false, string.Empty);

        Assert.Null(await new MetricsCollector(runner).CollectAsync(10));
    }

    [Fact]
    public async Task Una_salida_vacia_no_produce_una_lectura_falsa()
    {
        var runner = new RunnerFalso(true, "   ");

        Assert.Null(await new MetricsCollector(runner).CollectAsync(10));
    }

    [Fact]
    public async Task El_motivo_del_fallo_es_el_que_dio_el_canal()
    {
        var runner = new RunnerFalso(
            false, string.Empty, "El comando excedió los 3 segundos.");

        var recolector = new MetricsCollector(runner);

        Assert.Null(await recolector.CollectAsync(3));
        Assert.Equal("El comando excedió los 3 segundos.", recolector.UltimoError);
    }

    [Fact]
    public async Task Sin_texto_de_error_el_motivo_lo_dice_igual_en_vez_de_quedar_vacio()
    {
        var recolector = new MetricsCollector(new RunnerFalso(false, string.Empty));

        Assert.Null(await recolector.CollectAsync(10));
        Assert.False(string.IsNullOrWhiteSpace(recolector.UltimoError));
    }

    [Fact]
    public async Task Una_respuesta_vacia_del_servidor_no_se_confunde_con_un_fallo_del_canal()
    {
        var recolector = new MetricsCollector(new RunnerFalso(true, "   "));

        Assert.Null(await recolector.CollectAsync(10));
        Assert.Contains("/proc", recolector.UltimoError);
    }

    [Fact]
    public async Task Una_lectura_buena_borra_el_motivo_del_fallo_anterior()
    {
        var recolector = new MetricsCollector(new RunnerFalso(false, string.Empty, "se cayó"));

        await recolector.CollectAsync(10);
        Assert.Equal("se cayó", recolector.UltimoError);

        var bueno = new MetricsCollector(new RunnerFalso(true, SalidaValida(string.Empty)));
        await bueno.CollectAsync(10);

        Assert.Null(bueno.UltimoError);
    }
}
