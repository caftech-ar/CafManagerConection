using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

/// <summary>
/// Interpretación de lo que devuelve el servidor sobre un proceso (FR-165, FR-165a, FR-165b).
/// </summary>
public sealed class DetalleDeProcesoTests
{
    private const string Completa = """
        cmc:ps
        nginx|www-data|02:14:33|1|4|nginx: worker process
        cmc:binario
        /usr/sbin/nginx
        cmc:directorio
        /var/www
        """;

    private static DetalleDeProceso Leer(string salida = Completa) =>
        DetalleDeProceso.Interpretar(1234, "nginx", salida);

    [Fact]
    public void Se_leen_todos_los_campos()
    {
        var d = Leer();

        Assert.Equal(1234, d.Pid);
        Assert.Equal("nginx", d.Nombre);
        Assert.Equal("www-data", d.Usuario);
        Assert.Equal("/usr/sbin/nginx", d.Binario);
        Assert.Equal("/var/www", d.Directorio);
        Assert.Equal("nginx: worker process", d.Comando);
        Assert.Equal(1, d.Padre);
        Assert.Equal(4, d.Hilos);
        Assert.True(d.Existe);
        Assert.Empty(d.NoSePudo);
    }

    [Theory]
    [InlineData("01:23", 0, 0, 1, 23)]
    [InlineData("10:05:33", 0, 10, 5, 33)]
    [InlineData("12-04:11:09", 12, 4, 11, 9)]
    [InlineData("3-00:00:01", 3, 0, 0, 1)]
    public void El_tiempo_corriendo_se_lee_en_sus_cuatro_formatos(
        string etime, int dias, int horas, int minutos, int segundos) =>
        Assert.Equal(
            new TimeSpan(dias, horas, minutos, segundos),
            DetalleDeProceso.LeerDuracion(etime));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("no-es-un-tiempo")]
    [InlineData("99")]
    public void Un_tiempo_que_no_se_entiende_queda_sin_valor(string? etime) =>
        Assert.Null(DetalleDeProceso.LeerDuracion(etime));

    // FR-165a
    [Fact]
    public void Sin_permiso_para_los_enlaces_se_lee_el_resto_y_se_nombra_lo_que_falta()
    {
        const string sinPermiso = """
            cmc:ps
            postgres|postgres|5-03:22:10|1|9|postgres: checkpointer
            cmc:binario
            readlink: /proc/8891/exe: Permission denied
            cmc:directorio
            readlink: /proc/8891/cwd: Permission denied
            """;

        var d = DetalleDeProceso.Interpretar(8891, "postgres", sinPermiso);

        Assert.Equal("postgres", d.Usuario);
        Assert.Equal(9, d.Hilos);
        Assert.True(d.Existe);

        Assert.Null(d.Binario);
        Assert.Null(d.Directorio);
        Assert.Equal(2, d.NoSePudo.Count);
        Assert.All(d.NoSePudo, m => Assert.Contains("permiso", m, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void El_mensaje_crudo_del_comando_no_llega_a_la_ficha()
    {
        const string sinPermiso = """
            cmc:ps
            sshd|root|10:00|1|1|sshd: /usr/sbin/sshd
            cmc:binario
            readlink: /proc/999/exe: Permission denied
            """;

        var d = DetalleDeProceso.Interpretar(999, "sshd", sinPermiso);

        Assert.Null(d.Binario);
        Assert.DoesNotContain(d.NoSePudo, m => m.Contains("readlink", StringComparison.Ordinal));
    }

    // FR-165d
    [Fact]
    public void Un_proceso_que_ya_no_existe_no_dice_que_existe()
    {
        const string vacia = """
            cmc:ps
            cmc:binario
            readlink: /proc/4242/exe: No such file or directory
            """;

        var d = DetalleDeProceso.Interpretar(4242, "algo", vacia);

        Assert.False(d.Existe);
        Assert.False(d.TieneAlgo);
    }

    [Fact]
    public void La_linea_de_comando_llega_completa()
    {
        const string conArgumentos = """
            cmc:ps
            java|tomcat|1-02:00:00|1|48|/usr/bin/java -Xmx2g -jar /opt/app/app.jar --spring.profiles.active=prod
            """;

        var d = DetalleDeProceso.Interpretar(555, "java", conArgumentos);

        Assert.Equal(
            "/usr/bin/java -Xmx2g -jar /opt/app/app.jar --spring.profiles.active=prod",
            d.Comando);
    }

    [Fact]
    public void Un_enlace_vacio_sobre_un_proceso_que_existe_es_falta_de_permiso()
    {
        const string comoEnElServidor = """
            cmc:ps
            nginx|systemd+|17:29:35|231874|1|nginx: worker process
            cmc:binario
            cmc:directorio
            """;

        var d = DetalleDeProceso.Interpretar(231949, "nginx", comoEnElServidor);

        Assert.True(d.Existe);
        Assert.Equal("17:29:35", "17:29:35");
        Assert.Equal(new TimeSpan(17, 29, 35), d.Corriendo);
        Assert.Equal(231874, d.Padre);

        Assert.Null(d.Binario);
        Assert.Null(d.Directorio);
        Assert.Equal(2, d.NoSePudo.Count);
    }

    [Fact]
    public void La_linea_del_pid_1_del_servidor_real_se_interpreta()
    {
        const string pidUno = """
            cmc:ps
            systemd|root|88-14:41:43|0|1|/lib/systemd/systemd --system --deserialize 95
            cmc:binario
            /usr/lib/systemd/systemd
            cmc:directorio
            /
            """;

        var d = DetalleDeProceso.Interpretar(1, "systemd", pidUno);

        Assert.Equal("root", d.Usuario);
        Assert.Equal(new TimeSpan(88, 14, 41, 43), d.Corriendo);
        Assert.Equal("/usr/lib/systemd/systemd", d.Binario);
        Assert.Equal("/", d.Directorio);
        Assert.Equal("/lib/systemd/systemd --system --deserialize 95", d.Comando);
        Assert.Empty(d.NoSePudo);
    }

    [Fact]
    public void Un_proceso_inexistente_no_inventa_faltas_de_permiso()
    {
        const string vacia = """
            cmc:ps
            cmc:binario
            cmc:directorio
            """;

        var d = DetalleDeProceso.Interpretar(4242, "algo", vacia);

        Assert.False(d.Existe);
        Assert.Empty(d.NoSePudo);
    }

    // FR-165b

    [Theory]
    [InlineData(1, true)]
    [InlineData(1234, true)]
    [InlineData(4194304, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(-1234, false)]
    [InlineData(4194305, false)]
    public void Solo_un_numero_de_proceso_plausible_es_valido(int pid, bool esperado) =>
        Assert.Equal(esperado, ConsultorDeProcesos.EsPidValido(pid));
}
