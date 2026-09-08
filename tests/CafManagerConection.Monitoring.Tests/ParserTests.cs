using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public class CpuStatParserTests
{
    private const string ProcStat = """
        cpu  1234567 8901 234567 45678901 12345 0 6789 0 0 0
        cpu0 308641 2225 58641 11419725 3086 0 1697 0 0 0
        cpu1 308641 2225 58641 11419725 3086 0 1697 0 0 0
        cpu2 308641 2225 58641 11419725 3086 0 1697 0 0 0
        cpu3 308644 2226 58644 11419726 3087 0 1698 0 0 0
        intr 123456789 0 0 0
        ctxt 987654321
        """;

    [Fact]
    public void Lee_los_totales_y_cuenta_los_nucleos()
    {
        var m = CpuStatParser.Parse(ProcStat);

        Assert.Equal(4, m.CoreCount);
        Assert.True(m.Total > 0);
        Assert.Equal(45678901 + 12345, m.Idle); // idle + iowait
    }

    [Fact]
    public void El_uso_se_calcula_por_diferencia_entre_dos_lecturas()
    {
        var antes = new CpuSample(1000, 500, 4);
        var despues = new CpuSample(1100, 525, 4);

        var uso = CpuStatParser.Between(antes, despues);

        Assert.Equal(75, uso.UsedPercent, precision: 1);
    }

    [Fact]
    public void Sin_tiempo_transcurrido_el_uso_es_cero_y_no_divide_por_cero()
    {
        var muestra = new CpuSample(1000, 500, 4);

        var uso = CpuStatParser.Between(muestra, muestra);

        Assert.Equal(0, uso.UsedPercent);
    }

    [Fact]
    public void El_uso_queda_acotado_entre_cero_y_cien()
    {
        var antes = new CpuSample(1000, 900, 4);
        var despues = new CpuSample(1100, 800, 4); 

        var uso = CpuStatParser.Between(antes, despues);

        Assert.InRange(uso.UsedPercent, 0, 100);
    }

    /// <remarks>No reutiliza <see cref="ProcStat"/>: ahí el último campo (guest_nice) ya vale 0,
    /// así que un CRLF pegado a ese campo sería invisible; acá vale 50.</remarks>
    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        const string conUltimoCampoDistinto = "cpu  100 0 0 200 99 0 0 0 0 50";
        var crlf = conUltimoCampoDistinto + "\r\n";

        var m = CpuStatParser.Parse(crlf);
        var esperado = CpuStatParser.Parse(conUltimoCampoDistinto);

        Assert.Equal(esperado.Total, m.Total);
        Assert.Equal(449, m.Total); // suma de los diez campos
    }
}

public class MemoryInfoParserTests
{
    private const string MemInfo = """
        MemTotal:       16326152 kB
        MemFree:          234567 kB
        MemAvailable:   10123456 kB
        Buffers:          123456 kB
        Cached:          5678901 kB
        SwapTotal:       2097148 kB
        SwapFree:        2097148 kB
        """;

    [Fact]
    public void La_memoria_usada_es_total_menos_disponible()
    {
        var m = MemoryInfoParser.Parse(MemInfo);

        Assert.Equal(16326152L * 1024, m.TotalBytes);
        Assert.Equal(10123456L * 1024, m.AvailableBytes);
        Assert.Equal((16326152L - 10123456L) * 1024, m.UsedBytes);
    }

    [Fact]
    public void Usar_MemFree_habria_dado_un_numero_enganoso()
    {
        var m = MemoryInfoParser.Parse(MemInfo);

        Assert.InRange(m.UsedPercent, 35, 42);
    }

    [Fact]
    public void Sin_MemAvailable_se_aproxima_con_free_mas_buffers_y_cache()
    {
        // Kernels anteriores a 3.14 no exponen MemAvailable.
        const string viejo = """
            MemTotal:       1000000 kB
            MemFree:         100000 kB
            Buffers:          50000 kB
            Cached:          200000 kB
            """;

        var m = MemoryInfoParser.Parse(viejo);

        Assert.Equal(350000L * 1024, m.AvailableBytes);
    }

    [Fact]
    public void Una_entrada_vacia_no_rompe()
    {
        var m = MemoryInfoParser.Parse(string.Empty);

        Assert.Equal(0, m.TotalBytes);
        Assert.Equal(0, m.UsedPercent);
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var crlf = MemInfo.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        var m = MemoryInfoParser.Parse(crlf);
        var esperado = MemoryInfoParser.Parse(MemInfo);

        Assert.Equal(esperado.TotalBytes, m.TotalBytes);
        Assert.Equal(esperado.AvailableBytes, m.AvailableBytes);
        Assert.Equal(esperado.UsedBytes, m.UsedBytes);
    }
}

public class LoadAverageParserTests
{
    [Fact]
    public void Lee_las_tres_cargas()
    {
        var m = LoadAverageParser.ParseLoad("0.75 0.62 0.51 2/1234 56789");

        Assert.Equal(0.75, m.OneMinute, precision: 2);
        Assert.Equal(0.62, m.FiveMinutes, precision: 2);
        Assert.Equal(0.51, m.FifteenMinutes, precision: 2);
    }

    [Fact]
    public void La_carga_usa_punto_decimal_sin_importar_la_cultura_local()
    {
        var previa = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-AR");

        try
        {
            var m = LoadAverageParser.ParseLoad("1.50 2.25 3.75 1/100 200");

            Assert.Equal(1.5, m.OneMinute, precision: 2);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previa;
        }
    }

    [Fact]
    public void Lee_el_tiempo_encendido()
    {
        var uptime = LoadAverageParser.ParseUptime("1580449.71 12228877.05");

        Assert.Equal(18, uptime.Days);
    }
}

public class NetworkStatsParserTests
{
    private const string NetDev = """
        Inter-|   Receive                                                |  Transmit
         face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets
            lo: 1234567    1234    0    0    0     0          0         0  1234567    1234
          eth0: 98765432   54321    0    0    0     0          0         0 12345678    9876
        docker0:       0       0    0    0    0     0          0         0        0       0
        veth1a2b3c:   500       5    0    0    0     0          0         0      500       5
        """;

    [Fact]
    public void Lee_todas_las_interfaces()
    {
        var muestras = NetworkStatsParser.Parse(NetDev);

        Assert.Equal(4, muestras.Count);
        Assert.Contains(muestras, m => m.Interface == "eth0" && m.BytesIn == 98765432);
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var crlf = NetDev.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        var muestras = NetworkStatsParser.Parse(crlf);
        var esperado = NetworkStatsParser.Parse(NetDev);

        Assert.Equal(esperado.Count, muestras.Count);
        Assert.Equal(
            esperado.Select(s => (s.Interface, s.BytesIn, s.BytesOut)).OrderBy(t => t.Interface),
            muestras.Select(s => (s.Interface, s.BytesIn, s.BytesOut)).OrderBy(t => t.Interface));
    }

    [Theory]
    [InlineData("lo", true)]
    [InlineData("docker0", true)]
    [InlineData("veth1a2b3c", true)]
    [InlineData("br-abc123", true)]
    [InlineData("eth0", false)]
    [InlineData("ens18", false)]
    public void Reconoce_las_interfaces_virtuales(string nombre, bool esperado)
    {
        Assert.Equal(esperado, NetworkStatsParser.EsVirtual(nombre));
    }

    [Fact]
    public void La_velocidad_se_calcula_por_diferencia_en_el_tiempo()
    {
        var antes = new[] { new NetworkSample("eth0", 1000, 2000) };
        var despues = new[] { new NetworkSample("eth0", 6000, 4000) };

        var m = NetworkStatsParser.Between(antes, despues, segundos: 5);

        var eth0 = Assert.Single(m);
        Assert.Equal(1000, eth0.BytesInPerSecond); 
        Assert.Equal(400, eth0.BytesOutPerSecond);
    }

    [Fact]
    public void Las_virtuales_quedan_afuera_por_omision()
    {
        var muestras = NetworkStatsParser.Parse(NetDev);
        var m = NetworkStatsParser.Between(muestras, muestras, 5);

        Assert.DoesNotContain(m, x => x.Interface == "lo");
        Assert.DoesNotContain(m, x => x.Interface.StartsWith("veth", StringComparison.Ordinal));
    }

    [Fact]
    public void Un_contador_que_se_reinicia_no_da_velocidad_negativa()
    {
        var antes = new[] { new NetworkSample("eth0", 999999, 999999) };
        var despues = new[] { new NetworkSample("eth0", 100, 100) };

        var m = NetworkStatsParser.Between(antes, despues, 5);

        Assert.All(m, x =>
        {
            Assert.True(x.BytesInPerSecond >= 0);
            Assert.True(x.BytesOutPerSecond >= 0);
        });
    }
}

public class DiskUsageParserTests
{
    private const string Df = """
        Filesystem                1B-blocks        Used   Available Capacity Mounted on
        /dev/mapper/vg0-root   107374182400 45097156608 62277025792      43% /
        tmpfs                    8367181824           0   8367181824       0% /dev/shm
        devtmpfs                 8367181824           0   8367181824       0% /dev
        /dev/sdb1              536870912000 193273528320 343597383680     36% /srv
        overlay                107374182400 45097156608 62277025792      43% /var/lib/docker/overlay2/abc
        /dev/sda1                1073741824   134217728    939524096      13% /boot
        """;

    [Fact]
    public void Lee_los_discos_reales()
    {
        var discos = DiskUsageParser.Parse(Df);

        Assert.Equal(3, discos.Count);
        Assert.Contains(discos, d => d.MountPoint == "/");
        Assert.Contains(discos, d => d.MountPoint == "/srv");
        Assert.Contains(discos, d => d.MountPoint == "/boot");
    }

    [Fact]
    public void Descarta_los_sistemas_de_archivos_virtuales()
    {
        var discos = DiskUsageParser.Parse(Df);

        Assert.DoesNotContain(discos, d => d.FileSystem == "tmpfs");
        Assert.DoesNotContain(discos, d => d.FileSystem == "devtmpfs");
        Assert.DoesNotContain(discos, d => d.FileSystem == "overlay");
    }

    [Fact]
    public void Descarta_los_montajes_internos_de_docker()
    {
        var discos = DiskUsageParser.Parse(Df);

        Assert.DoesNotContain(discos, d => d.MountPoint.Contains("/var/lib/docker"));
    }

    [Fact]
    public void El_mismo_dispositivo_montado_dos_veces_se_informa_una_sola()
    {
        const string repetido = """
            Filesystem      1B-blocks       Used  Available Capacity Mounted on
            /dev/sda1     10000000000 5000000000 5000000000      50% /
            /dev/sda1     10000000000 5000000000 5000000000      50% /mnt/copia
            """;

        var discos = DiskUsageParser.Parse(repetido);

        Assert.Single(discos);
    }

    [Fact]
    public void Calcula_el_porcentaje_usado()
    {
        var discos = DiskUsageParser.Parse(Df);
        var raiz = discos.First(d => d.MountPoint == "/");

        Assert.Equal(42, raiz.UsedPercent, precision: 0);
    }

    [Fact]
    public void Una_salida_sin_datos_no_rompe()
    {
        Assert.Empty(DiskUsageParser.Parse("Filesystem 1B-blocks Used Available Capacity Mounted on"));
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var crlf = Df.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        var discos = DiskUsageParser.Parse(crlf);
        var esperado = DiskUsageParser.Parse(Df);

        Assert.Equal(esperado.Count, discos.Count);
        Assert.Equal(
            esperado.Select(d => (d.MountPoint, d.FileSystem, d.TotalBytes)).OrderBy(t => t.MountPoint),
            discos.Select(d => (d.MountPoint, d.FileSystem, d.TotalBytes)).OrderBy(t => t.MountPoint));

        var raiz = discos.First(d => d.MountPoint == "/");
        Assert.Equal(42, raiz.UsedPercent, precision: 0);
    }
}

public class SystemInfoParserTests
{
    [Fact]
    public void Lee_la_distribucion()
    {
        const string osRelease = """
            NAME="Ubuntu"
            VERSION="22.04.3 LTS (Jammy Jellyfish)"
            PRETTY_NAME="Ubuntu 22.04.3 LTS"
            ID=ubuntu
            """;

        Assert.Equal("Ubuntu 22.04.3 LTS", SystemInfoParser.ParseDistribution(osRelease));
    }

    [Fact]
    public void Sin_PRETTY_NAME_devuelve_nulo()
    {
        Assert.Null(SystemInfoParser.ParseDistribution("ID=ubuntu"));
    }

    [Fact]
    public void Cuenta_los_usuarios_conectados()
    {
        const string who = """
            root     pts/0        2026-08-24 10:15 (192.0.2.5)
            admin    pts/1        2026-08-24 11:02 (192.0.2.9)
            """;

        Assert.Equal(2, SystemInfoParser.ParseConnectedUsers(who));
    }

    [Fact]
    public void Lee_los_servicios_fallidos_descartando_los_encabezados()
    {
        const string salida = """
              UNIT                LOAD   ACTIVE SUB    DESCRIPTION
            ● nginx.service       loaded failed failed A high performance web server
            ● postgresql.service  loaded failed failed PostgreSQL RDBMS

            LOAD   = Reflects whether the unit definition was properly loaded.
            2 loaded units listed.
            """;

        var fallidos = SystemInfoParser.ParseFailedServices(salida);

        Assert.Equal(2, fallidos.Count);
        Assert.Contains("nginx.service", fallidos);
        Assert.Contains("postgresql.service", fallidos);
    }

    [Fact]
    public void Sin_servicios_fallidos_la_lista_queda_vacia()
    {
        const string salida = """
              UNIT LOAD ACTIVE SUB DESCRIPTION
            0 loaded units listed.
            """;

        Assert.Empty(SystemInfoParser.ParseFailedServices(salida));
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        const string osRelease = """
            NAME="Ubuntu"
            PRETTY_NAME="Ubuntu 22.04.3 LTS"
            """;
        const string who = """
            root     pts/0        2026-08-24 10:15 (192.0.2.5)
            admin    pts/1        2026-08-24 11:02 (192.0.2.9)
            """;
        const string failed = """
              UNIT                LOAD   ACTIVE SUB    DESCRIPTION
            ● nginx.service       loaded failed failed A high performance web server
            ● postgresql.service  loaded failed failed PostgreSQL RDBMS
            """;

        string ACrlf(string s) => s.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        Assert.Equal(
            SystemInfoParser.ParseDistribution(osRelease),
            SystemInfoParser.ParseDistribution(ACrlf(osRelease)));

        Assert.Equal(
            SystemInfoParser.ParseConnectedUsers(who),
            SystemInfoParser.ParseConnectedUsers(ACrlf(who)));

        Assert.Equal(
            SystemInfoParser.ParseFailedServices(failed),
            SystemInfoParser.ParseFailedServices(ACrlf(failed)));
    }
}
