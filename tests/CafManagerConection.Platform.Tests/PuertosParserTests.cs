using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class PuertosParserTests
{
    private const string SalidaSs = """
        tcp   LISTEN 0      4096         0.0.0.0:22         0.0.0.0:*    users:(("sshd",pid=812,fd=3))
        tcp   LISTEN 0      511        127.0.0.1:9000       0.0.0.0:*    users:(("php-fpm",pid=1044,fd=6))
        tcp   LISTEN 0      511          0.0.0.0:80         0.0.0.0:*    users:(("nginx",pid=990,fd=6))
        tcp   LISTEN 0      511             [::]:80            [::]:*    users:(("nginx",pid=990,fd=7))
        udp   UNCONN 0      0          127.0.0.53:53         0.0.0.0:*    users:(("systemd-resolve",pid=700,fd=12))
        """;

    private const string SalidaNetstat = """
        Active Internet connections (only servers)
        Proto Recv-Q Send-Q Local Address           Foreign Address         State       PID/Program name
        tcp        0      0 0.0.0.0:22              0.0.0.0:*               LISTEN      812/sshd
        tcp        0      0 127.0.0.1:5432          0.0.0.0:*               LISTEN      1300/postgres
        """;

    [Fact]
    public void Lee_los_puertos_de_la_salida_de_ss()
    {
        var puertos = PuertosParser.Parse(SalidaSs);

        Assert.Contains(puertos, p => p.Port == 22 && p.Process == "sshd");
        Assert.Contains(puertos, p => p.Port == 80 && p.Process == "nginx");
        Assert.Contains(puertos, p => p.Port == 9000 && p.Process == "php-fpm");
    }

    [Fact]
    public void Lee_los_puertos_de_la_salida_de_netstat()
    {
        var puertos = PuertosParser.Parse(SalidaNetstat);

        Assert.Equal(2, puertos.Count);
        Assert.Contains(puertos, p => p.Port == 22 && p.Process == "sshd");
        Assert.Contains(puertos, p => p.Port == 5432 && p.Process == "postgres");
    }

    [Fact]
    public void Un_servicio_que_escucha_en_IPv4_y_en_IPv6_aparece_una_sola_vez()
    {
        var puertos = PuertosParser.Parse(SalidaSs);

        Assert.Single(puertos, p => p.Port == 80);
    }

    [Fact]
    public void Distingue_escuchar_en_todas_las_interfaces_de_escuchar_sólo_local()
    {
        var puertos = PuertosParser.Parse(SalidaSs);

        Assert.Equal("0.0.0.0", puertos.First(p => p.Port == 22).Address);
        Assert.Equal("127.0.0.1", puertos.First(p => p.Port == 9000).Address);
    }

    [Fact]
    public void Sin_permiso_el_proceso_queda_sin_nombre_en_vez_de_inventado()
    {
        const string sinProceso =
            "tcp   LISTEN 0      4096         0.0.0.0:22         0.0.0.0:*";

        var puertos = PuertosParser.Parse(sinProceso);

        Assert.Single(puertos);
        Assert.Null(puertos[0].Process);
    }

    [Fact]
    public void Los_puertos_salen_ordenados_por_numero()
    {
        var puertos = PuertosParser.Parse(SalidaSs);

        Assert.Equal(puertos.Select(p => p.Port).Order(), puertos.Select(p => p.Port));
    }

    [Fact]
    public void Se_incluye_UDP_y_no_sólo_TCP()
    {
        var puertos = PuertosParser.Parse(SalidaSs);

        Assert.Contains(puertos, p => p.Protocol.StartsWith("udp", StringComparison.Ordinal));
    }

    [Fact]
    public void Las_cabeceras_no_se_toman_por_puertos()
    {
        var puertos = PuertosParser.Parse(SalidaNetstat);

        Assert.DoesNotContain(puertos, p => p.Protocol.Contains("Proto", StringComparison.Ordinal));
    }

    [Fact]
    public void Una_salida_vacia_no_rompe()
    {
        Assert.Empty(PuertosParser.Parse(string.Empty));
        Assert.Empty(PuertosParser.Parse("\n\n   \n"));
    }

    [Fact]
    public void Una_linea_que_no_entiende_se_saltea_sin_perder_las_demas()
    {
        var mezcla = "Warning: some processes could not be identified\n" + SalidaNetstat;

        var puertos = PuertosParser.Parse(mezcla);

        Assert.Equal(2, puertos.Count);
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var crlfSs = SalidaSs.ReplaceLineEndings("\n").Replace("\n", "\r\n");
        var crlfNetstat = SalidaNetstat.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        Assert.Equal(
            PuertosParser.Parse(SalidaSs).Select(p => (p.Protocol, p.Address, p.Port, p.Process)),
            PuertosParser.Parse(crlfSs).Select(p => (p.Protocol, p.Address, p.Port, p.Process)));

        Assert.Equal(
            PuertosParser.Parse(SalidaNetstat).Select(p => (p.Protocol, p.Address, p.Port, p.Process)),
            PuertosParser.Parse(crlfNetstat).Select(p => (p.Protocol, p.Address, p.Port, p.Process)));
    }

    // FR-165

    [Fact]
    public void Lee_el_pid_del_formato_de_ss()
    {
        var puertos = PuertosParser.Parse(SalidaSs);

        Assert.Equal(812, puertos.First(p => p.Port == 22).Pid);
        Assert.Equal(990, puertos.First(p => p.Port == 80).Pid);
        Assert.Equal(700, puertos.First(p => p.Port == 53).Pid);
    }

    [Fact]
    public void Lee_el_pid_del_formato_de_netstat()
    {
        var puertos = PuertosParser.Parse(SalidaNetstat);

        Assert.Equal(812, puertos.First(p => p.Port == 22).Pid);
        Assert.Equal(1300, puertos.First(p => p.Port == 5432).Pid);
    }

    // FR-164d
    [Fact]
    public void Un_socket_sin_proceso_visible_se_lista_sin_proceso_y_sin_pid()
    {
        const string sinPermisos = """
            tcp   LISTEN 0      4096         0.0.0.0:22         0.0.0.0:*
            tcp   LISTEN 0      511          0.0.0.0:443        0.0.0.0:*
            """;

        var puertos = PuertosParser.Parse(sinPermisos);

        Assert.Equal(2, puertos.Count);
        Assert.All(puertos, p => Assert.Null(p.Process));
        Assert.All(puertos, p => Assert.Null(p.Pid));
        Assert.Contains(puertos, p => p.Port == 22);
        Assert.Contains(puertos, p => p.Port == 443);
    }

    private const string SalidaReal = """
        udp UNCONN 0      0      127.0.0.53%lo:53    0.0.0.0:* users:(("systemd-resolve",pid=4067437,fd=13))
        udp UNCONN 0      0          127.0.0.1:323   0.0.0.0:* users:(("chronyd",pid=921,fd=5))
        tcp LISTEN 0      512          0.0.0.0:6081  0.0.0.0:* users:(("dotnet",pid=3193011,fd=243))
        tcp LISTEN 0      15           0.0.0.0:4118  0.0.0.0:*
        tcp LISTEN 0      511          0.0.0.0:8084  0.0.0.0:* users:(("nginx",pid=1403740,fd=11),("nginx",pid=1403738,fd=11),("nginx",pid=1403737,fd=11),("nginx",pid=1403736,fd=11),("nginx",pid=1403735,fd=11))
        tcp LISTEN 0      4096         0.0.0.0:8089  0.0.0.0:* users:(("docker-proxy",pid=231911,fd=8))
        tcp LISTEN 0      1          127.0.0.1:8002  0.0.0.0:* users:(("java",pid=375727,fd=74))
        """;

    [Fact]
    public void La_direccion_no_arrastra_el_nombre_de_la_interfaz()
    {
        var dns = PuertosParser.Parse(SalidaReal).First(p => p.Port == 53);

        Assert.Equal("127.0.0.53", dns.Address);
        Assert.Equal("systemd-resolve", dns.Process);
        Assert.Equal(4067437, dns.Pid);
    }

    [Fact]
    public void Un_socket_con_varios_procesos_se_muestra_una_sola_vez()
    {
        var puertos = PuertosParser.Parse(SalidaReal).Where(p => p.Port == 8084).ToList();

        Assert.Single(puertos);
        Assert.Equal("nginx", puertos[0].Process);
        Assert.Equal(1403740, puertos[0].Pid);
    }

    [Fact]
    public void La_salida_real_sin_sudo_conserva_las_filas_sin_dueno()
    {
        var puertos = PuertosParser.Parse(SalidaReal);

        Assert.Equal(7, puertos.Count);

        var sinDueno = puertos.First(p => p.Port == 4118);

        Assert.Null(sinDueno.Process);
        Assert.Null(sinDueno.Pid);
    }

    private const string SalidaReal24 = """
        udp UNCONN 0      0                               0.0.0.0:52792 0.0.0.0:* users:(("openvpn",pid=984,fd=5))
        udp UNCONN 0      0      [fe80::2d0:dff:fe00:2b0b]%enp2s0:546      [::]:* users:(("systemd-network",pid=455,fd=22))
        tcp LISTEN 0      4096                            0.0.0.0:22    0.0.0.0:* users:(("sshd",pid=7309,fd=3),("systemd",pid=1,fd=91))
        tcp LISTEN 0      4096                                  *:8082        *:* users:(("lapacho",pid=906,fd=8))
        tcp LISTEN 0      80                              0.0.0.0:3306  0.0.0.0:* users:(("mariadbd",pid=822,fd=26))
        tcp LISTEN 0      4096                               [::]:22       [::]:* users:(("sshd",pid=7309,fd=4),("systemd",pid=1,fd=92))
        """;

    [Fact]
    public void Una_direccion_IPv6_con_interfaz_se_lee_entera()
    {
        var dhcp = PuertosParser.Parse(SalidaReal24).First(p => p.Port == 546);

        Assert.Equal("[fe80::2d0:dff:fe00:2b0b]", dhcp.Address);
        Assert.Equal("systemd-network", dhcp.Process);
        Assert.Equal(455, dhcp.Pid);
    }

    [Fact]
    public void El_comodin_como_direccion_se_lee()
    {
        var lapacho = PuertosParser.Parse(SalidaReal24).First(p => p.Port == 8082);

        Assert.Equal("*", lapacho.Address);
        Assert.Equal("lapacho", lapacho.Process);
    }

    [Fact]
    public void Con_activacion_por_socket_se_muestra_el_servicio_y_no_systemd()
    {
        var ssh = PuertosParser.Parse(SalidaReal24).Where(p => p.Port == 22).ToList();

        Assert.All(ssh, p => Assert.Equal("sshd", p.Process));
        Assert.All(ssh, p => Assert.Equal(7309, p.Pid));
    }

    [Fact]
    public void El_puerto_22_no_aparece_dos_veces()
    {
        Assert.Single(PuertosParser.Parse(SalidaReal24), p => p.Port == 22);
    }

    private const string SalidaRealArm = """
        udp UNCONN 0      0      192.0.2.65%enp0s6:68   0.0.0.0:* users:(("systemd-network",pid=925644,fd=23))
        udp UNCONN 0      0                  [::]:111     [::]:* users:(("rpcbind",pid=749,fd=7),("systemd",pid=1,fd=229))
        tcp LISTEN 0      4096            0.0.0.0:8000 0.0.0.0:* users:(("docker-proxy",pid=2338,fd=7))
        """;

    [Fact]
    public void Una_direccion_IPv4_atada_a_una_interfaz_se_lee_entera()
    {
        var dhcp = PuertosParser.Parse(SalidaRealArm).First(p => p.Port == 68);

        Assert.Equal("192.0.2.65", dhcp.Address);
        Assert.Equal("systemd-network", dhcp.Process);
    }

    [Fact]
    public void El_comodin_IPv6_se_lee()
    {
        var rpc = PuertosParser.Parse(SalidaRealArm).First(p => p.Port == 111);

        Assert.Equal("[::]", rpc.Address);
        Assert.Equal("rpcbind", rpc.Process);
        Assert.Equal(749, rpc.Pid);
    }

    [Fact]
    public void Conviven_las_filas_con_proceso_y_las_que_no_lo_tienen()
    {
        const string mezclada = """
            tcp   LISTEN 0      4096         0.0.0.0:22         0.0.0.0:*
            tcp   LISTEN 0      511          0.0.0.0:80         0.0.0.0:*    users:(("nginx",pid=990,fd=6))
            """;

        var puertos = PuertosParser.Parse(mezclada);

        Assert.Equal(2, puertos.Count);
        Assert.Null(puertos.First(p => p.Port == 22).Pid);
        Assert.Equal(990, puertos.First(p => p.Port == 80).Pid);
    }
}
