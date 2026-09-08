using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class DockerUsageTests
{
    private const string Tab = "\t";

    [Fact]
    public void Lee_cpu_y_memoria_de_docker_stats()
    {
        var salida = string.Join('\n',
        [
            "a1b2c3d4e5f6" + Tab + "12.34%" + Tab + "1.5GiB / 8GiB",
            "0f1e2d3c4b5a" + Tab + "0.00%" + Tab + "24.5MiB / 512MiB",
        ]);

        var uso = DockerStatsParser.Parse(salida);

        Assert.Equal(2, uso.Count);
        Assert.Equal(12.34, uso["a1b2c3d4e5f6"].CpuPercent, 2);
        Assert.Equal((long)(1.5 * 1024 * 1024 * 1024), uso["a1b2c3d4e5f6"].MemoryBytes);
        Assert.Equal(8L * 1024 * 1024 * 1024, uso["a1b2c3d4e5f6"].MemoryLimitBytes);
    }

    [Fact]
    public void El_porcentaje_de_memoria_se_calcula_sobre_el_limite()
    {
        var uso = DockerStatsParser.Parse("abc" + Tab + "1%" + Tab + "2GiB / 8GiB");

        Assert.Equal(25, uso["abc"].MemoryPercent, 1);
    }

    [Fact]
    public void Sin_limite_de_memoria_el_porcentaje_es_cero_y_no_divide_por_cero()
    {
        var uso = DockerStatsParser.Parse("abc" + Tab + "1%" + Tab + "2GiB");

        Assert.Equal(2L * 1024 * 1024 * 1024, uso["abc"].MemoryBytes);
        Assert.Equal(0, uso["abc"].MemoryPercent);
    }

    [Theory]
    // Docker mezcla las dos convenciones: los sufijos con "i" son potencias de 1024 y los
    // otros de 1000. Tratar todo como 1024 da un 7 % de error en los valores en GB.
    [InlineData("1KiB", 1024L)]
    [InlineData("1kB", 1000L)]
    [InlineData("1MiB", 1048576L)]
    [InlineData("1MB", 1000000L)]
    [InlineData("1GiB", 1073741824L)]
    [InlineData("1GB", 1000000000L)]
    [InlineData("512B", 512L)]
    [InlineData("0B", 0L)]
    public void Los_sufijos_binarios_y_decimales_no_se_confunden(string texto, long esperado)
    {
        Assert.Equal(esperado, DockerStatsParser.ParseTamano(texto));
    }

    [Fact]
    public void El_porcentaje_de_cpu_puede_pasar_de_cien_con_varios_nucleos()
    {
        var uso = DockerStatsParser.Parse("abc" + Tab + "347.5%" + Tab + "1GiB / 8GiB");

        Assert.Equal(347.5, uso["abc"].CpuPercent, 1);
    }

    [Fact]
    public void Una_linea_incompleta_no_invalida_el_resto()
    {
        var salida = string.Join('\n',
        [
            "basura sin tabuladores",
            "abc" + Tab + "5%" + Tab + "1GiB / 2GiB",
            string.Empty,
        ]);

        var uso = DockerStatsParser.Parse(salida);

        Assert.Single(uso);
        Assert.True(uso.ContainsKey("abc"));
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var salida = string.Join('\n',
        [
            "a1b2c3d4e5f6" + Tab + "12.34%" + Tab + "1.5GiB / 8GiB",
            "0f1e2d3c4b5a" + Tab + "0.00%" + Tab + "24.5MiB / 512MiB",
        ]);
        var crlf = salida.Replace("\n", "\r\n");

        var uso = DockerStatsParser.Parse(crlf);
        var esperado = DockerStatsParser.Parse(salida);

        Assert.Equal(esperado.Count, uso.Count);
        Assert.Equal(esperado["a1b2c3d4e5f6"].MemoryLimitBytes, uso["a1b2c3d4e5f6"].MemoryLimitBytes);
        Assert.Equal(esperado["a1b2c3d4e5f6"].CpuPercent, uso["a1b2c3d4e5f6"].CpuPercent);
    }

    [Fact]
    public void Las_etiquetas_de_compose_identifican_proyecto_y_servicio()
    {
        var (proyecto, servicio) = DockerPsParser.ParseComposeLabels(
            "com.docker.compose.project=superset,com.docker.compose.service=worker,"
            + "com.docker.compose.version=2.24.5");

        Assert.Equal("superset", proyecto);
        Assert.Equal("worker", servicio);
    }

    [Fact]
    public void Un_contenedor_sin_etiquetas_de_compose_queda_suelto()
    {
        var (proyecto, servicio) = DockerPsParser.ParseComposeLabels(
            "maintainer=nginx,org.opencontainers.image.version=1.25");

        Assert.Null(proyecto);
        Assert.Null(servicio);

        var c = new ContainerInfo("id", "nginx", "nginx:1.25", "running", "Up 2 days", []);
        Assert.True(c.IsStandalone);
    }

    [Fact]
    public void Un_contenedor_con_proyecto_no_es_suelto()
    {
        var c = new ContainerInfo(
            "id", "superset-worker-1", "superset", "running", "Up 2 days", [],
            ComposeProject: "superset", ComposeService: "worker");

        Assert.False(c.IsStandalone);
    }

    [Fact]
    public void Sin_etiquetas_no_se_inventa_proyecto()
    {
        Assert.Equal((null, null), DockerPsParser.ParseComposeLabels(string.Empty));
    }

    [Fact]
    public void Una_etiqueta_con_valor_vacio_se_ignora()
    {
        var (proyecto, _) = DockerPsParser.ParseComposeLabels("com.docker.compose.project=");

        Assert.Null(proyecto);
    }
}
