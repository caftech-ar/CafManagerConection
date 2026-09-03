using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class ParserDeProcesosTests
{
    // Campos de /proc/<pid>/stat: 1 pid, 2 comm, 3 estado, 4 ppid, 14 utime, 15 stime, 20 hilos, 22 arranque, 24 rss en páginas.
    private const string Systemd =
        "1 (systemd) S 0 1 1 0 -1 4194560 43296 1176538 96 2185 194 431 3465 1885 20 0 1 0 34 "
        + "170491904 3237 18446744073709551615 1 1 0 0 0 0 671173123 4096 1260 0 0 0 17 3 0 0 24 0";

    private const string SdPam =
        "2848 ((sd-pam)) S 2847 2847 2847 0 -1 1077936448 46 0 0 0 0 0 0 0 20 0 1 0 3132 "
        + "173953024 594 18446744073709551615 0 0 0 0 0 0 0 4096 0 0 0 0 17 2 0 0 0 0";

    private const string NombreConEspaciosYParentesis =
        "4242 ((mi cosa) rara) R 1 4242 4242 0 -1 4194304 512 0 0 0 700 300 0 0 20 0 3 0 90210 "
        + "9999999 1024 18446744073709551615 0 0 0 0 0 0 0 0 0 0 0 0 17 1 0 0 0 0";

    private const string IoDeVerdad = """
        rchar: 3273983
        wchar: 1244851
        syscr: 6961
        syscw: 4225
        read_bytes: 27889664
        write_bytes: 331776
        cancelled_write_bytes: 45056
        """;

    private const string Passwd = "root:0\nmalva:1000";

    private const string Duenos = "0 /proc/1\n1000 /proc/2848\n1000 /proc/4242";

    private static string Encabezado(
        string tics = "100", string pagina = "4096", string uptime = "9000.00 8000.00") =>
        tics + Tramo() + pagina + Tramo() + Passwd + Tramo() + uptime + Tramo() + Duenos;

    private static string Tramo() => "\n" + ParserDeProcesos.Marca + "\n";

    private static string Salida(params string[] bloques) =>
        Encabezado() + string.Concat(bloques.Select(b => Tramo() + b + "\n"));

    [Fact]
    public void Lee_el_pid_el_padre_el_estado_y_los_hilos()
    {
        var p = ParserDeProcesos.ParsearStat(Systemd);

        Assert.NotNull(p);
        Assert.Equal(1, p.Pid);
        Assert.Equal(0, p.PidPadre);
        Assert.Equal("systemd", p.Nombre);
        Assert.Equal("S", p.Estado);
        Assert.Equal(1, p.Hilos);
    }

    [Fact]
    public void El_nombre_con_espacios_y_parentesis_no_corre_los_campos()
    {
        var p = ParserDeProcesos.ParsearStat(NombreConEspaciosYParentesis);

        Assert.NotNull(p);
        Assert.Equal("(mi cosa) rara", p.Nombre);
        Assert.Equal("R", p.Estado);
        Assert.Equal(1, p.PidPadre);
        Assert.Equal(3, p.Hilos);
        Assert.Equal(700, p.TicsDeUsuario);
        Assert.Equal(300, p.TicsDeSistema);
    }

    [Fact]
    public void El_nombre_de_sd_pam_conserva_sus_parentesis()
    {
        var p = ParserDeProcesos.ParsearStat(SdPam);

        Assert.NotNull(p);
        Assert.Equal("(sd-pam)", p.Nombre);
        Assert.Equal(2847, p.PidPadre);
        Assert.Equal(3132, p.TicDeArranque);
    }

    [Fact]
    public void Los_tics_de_cpu_son_utime_mas_stime()
    {
        var p = ParserDeProcesos.ParsearStat(Systemd);

        Assert.NotNull(p);
        Assert.Equal(194, p.TicsDeUsuario);
        Assert.Equal(431, p.TicsDeSistema);
        Assert.Equal(625, p.TicsDeCpu);
    }

    [Fact]
    public void La_memoria_residente_sale_en_bytes_multiplicando_por_el_tamano_de_pagina()
    {
        var p = ParserDeProcesos.ParsearStat(Systemd, bytesPorPagina: 4096);

        Assert.NotNull(p);
        Assert.Equal(3237L * 4096, p.BytesResidentes);
    }

    [Fact]
    public void Una_linea_sin_parentesis_de_cierre_no_devuelve_proceso()
    {
        Assert.Null(ParserDeProcesos.ParsearStat("1 (systemd S 0 1 1"));
    }

    [Fact]
    public void Una_linea_truncada_no_devuelve_proceso()
    {
        Assert.Null(ParserDeProcesos.ParsearStat("4242 ((mi cosa) rara) R 1"));
    }

    [Fact]
    public void Una_linea_vacia_no_devuelve_proceso()
    {
        Assert.Null(ParserDeProcesos.ParsearStat(string.Empty));
    }

    [Fact]
    public void Parse_arma_la_muestra_con_todos_los_bloques()
    {
        var muestra = ParserDeProcesos.Parse(
            Salida(Systemd, SdPam, NombreConEspaciosYParentesis), DateTimeOffset.UnixEpoch);

        Assert.Equal(3, muestra.Procesos.Count);
        Assert.Equal(new[] { 1, 2848, 4242 }, muestra.Procesos.Select(p => p.Pid).ToArray());
        Assert.Equal(DateTimeOffset.UnixEpoch, muestra.Instante);
    }

    [Fact]
    public void Un_proceso_que_desaparecio_deja_un_bloque_vacio_y_no_es_un_error()
    {
        var muestra = ParserDeProcesos.Parse(
            Salida(Systemd, string.Empty, SdPam), DateTimeOffset.UnixEpoch);

        Assert.Equal(2, muestra.Procesos.Count);
    }

    [Fact]
    public void La_entrada_y_salida_del_bloque_se_pega_a_su_proceso()
    {
        var muestra = ParserDeProcesos.Parse(
            Salida(Systemd + "\n" + IoDeVerdad), DateTimeOffset.UnixEpoch);

        var p = Assert.Single(muestra.Procesos);
        Assert.Equal(27889664, p.BytesLeidos);
        Assert.Equal(331776, p.BytesEscritos);
    }

    [Fact]
    public void Un_proceso_ajeno_sin_permiso_para_leer_su_io_sale_igual_sin_entrada_ni_salida()
    {
        var muestra = ParserDeProcesos.Parse(Salida(Systemd), DateTimeOffset.UnixEpoch);

        var p = Assert.Single(muestra.Procesos);
        Assert.Null(p.BytesLeidos);
        Assert.Null(p.BytesEscritos);
    }

    [Fact]
    public void Los_tics_por_segundo_salen_del_getconf_del_servidor()
    {
        var salida = Encabezado(tics: "250") + Tramo() + Systemd;

        var muestra = ParserDeProcesos.Parse(salida, DateTimeOffset.UnixEpoch);

        Assert.Equal(250, muestra.TicsPorSegundo);
    }

    [Fact]
    public void Sin_getconf_los_tics_por_segundo_valen_cien_y_la_pagina_cuatro_kilobytes()
    {
        var salida = Encabezado(tics: string.Empty, pagina: string.Empty) + Tramo() + Systemd;

        var muestra = ParserDeProcesos.Parse(salida, DateTimeOffset.UnixEpoch);

        Assert.Equal(MuestraDeProcesos.TicsPorSegundoPorOmision, muestra.TicsPorSegundo);
        Assert.Equal(3237L * 4096, muestra.Procesos[0].BytesResidentes);
    }

    [Fact]
    public void El_tamano_de_pagina_del_servidor_manda_sobre_el_de_omision()
    {
        var salida = Encabezado(pagina: "16384") + Tramo() + Systemd;

        var muestra = ParserDeProcesos.Parse(salida, DateTimeOffset.UnixEpoch);

        Assert.Equal(3237L * 16384, muestra.Procesos[0].BytesResidentes);
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var lf = Salida(Systemd + "\n" + IoDeVerdad, SdPam);
        var crlf = lf.Replace("\n", "\r\n");

        var conLf = ParserDeProcesos.Parse(lf, DateTimeOffset.UnixEpoch);
        var conCrlf = ParserDeProcesos.Parse(crlf, DateTimeOffset.UnixEpoch);

        Assert.Equal(conLf.TicsPorSegundo, conCrlf.TicsPorSegundo);
        Assert.Equal(conLf.Procesos, conCrlf.Procesos);
    }

    [Fact]
    public void La_marca_separadora_va_entre_comillas_para_no_ser_un_comentario()
    {
        Assert.Contains($"echo '{ParserDeProcesos.Marca}'", ParserDeProcesos.ComandoDeLectura);
        Assert.DoesNotContain($"echo {ParserDeProcesos.Marca}", ParserDeProcesos.ComandoDeLectura);
    }

    [Fact]
    public void El_comando_lee_stat_e_io_de_todos_los_procesos_de_una_vez()
    {
        var comando = ParserDeProcesos.ComandoDeLectura;

        Assert.Contains("/proc/[0-9]*", comando);
        Assert.Contains("/stat", comando);
        Assert.Contains("/io", comando);
        Assert.Contains("getconf CLK_TCK", comando);
        Assert.Contains("getconf PAGESIZE", comando);
        Assert.DoesNotContain("\n", comando);
    }

    [Fact]
    public void El_comando_calla_los_errores_de_los_procesos_que_no_puede_leer()
    {
        var callados = ParserDeProcesos.ComandoDeLectura.Split("2>/dev/null").Length - 1;

        Assert.True(callados >= 2, ParserDeProcesos.ComandoDeLectura);
    }

    [Fact]
    public void El_dueno_de_cada_proceso_sale_del_stat_de_su_directorio()
    {
        var duenos = ParserDeProcesos.Duenos("0 /proc/1\n1000 /proc/4242");

        Assert.Equal(0, duenos[1]);
        Assert.Equal(1000, duenos[4242]);
    }

    [Fact]
    public void Una_linea_del_stat_sin_pid_no_ensucia_los_duenos()
    {
        var duenos = ParserDeProcesos.Duenos("stat: no se pudo hacer statx\n\n1000 /proc/7");

        Assert.Equal(new[] { 7 }, duenos.Keys.ToArray());
    }

    [Fact]
    public void El_uid_se_traduce_al_nombre_del_passwd()
    {
        var filas = ParserDeProcesos
            .Parse(Salida(Systemd, SdPam), DateTimeOffset.UnixEpoch)
            .SinMedir();

        Assert.Equal("root", filas[0].Usuario);
        Assert.Equal("malva", filas[1].Usuario);
    }

    [Fact]
    public void Un_uid_que_no_esta_en_el_passwd_se_muestra_como_numero()
    {
        var salida = "100" + Tramo() + "4096" + Tramo() + "root:0" + Tramo()
            + "9000.00 8000.00" + Tramo() + "4242 /proc/2848" + Tramo() + SdPam;

        var fila = Assert.Single(
            ParserDeProcesos.Parse(salida, DateTimeOffset.UnixEpoch).SinMedir());

        Assert.Equal("4242", fila.Usuario);
    }

    // El campo 22 de stat es el tic de arranque: el tiempo corriendo es el encendido del servidor menos eso, y no un dato más que haya que pedirle.
    [Fact]
    public void El_tiempo_corriendo_sale_del_arranque_contra_el_encendido_del_servidor()
    {
        var fila = Assert.Single(
            ParserDeProcesos
                .Parse(Encabezado() + Tramo() + SdPam, DateTimeOffset.UnixEpoch)
                .SinMedir());

        Assert.Equal(9000 - 31.32, fila.TiempoCorriendo!.Value.TotalSeconds, precision: 2);
    }

    [Fact]
    public void Sin_uptime_el_tiempo_corriendo_no_se_inventa()
    {
        var fila = Assert.Single(
            ParserDeProcesos
                .Parse(Encabezado(uptime: string.Empty) + Tramo() + SdPam, DateTimeOffset.UnixEpoch)
                .SinMedir());

        Assert.Null(fila.TiempoCorriendo);
    }

    [Fact]
    public void Sin_el_stat_de_los_duenos_las_filas_salen_igual_sin_usuario()
    {
        var salida = "100" + Tramo() + "4096" + Tramo() + "root:0" + Tramo()
            + "9000.00 8000.00" + Tramo() + string.Empty + Tramo() + Systemd;

        var fila = Assert.Single(
            ParserDeProcesos.Parse(salida, DateTimeOffset.UnixEpoch).SinMedir());

        Assert.Equal(string.Empty, fila.Usuario);
        Assert.Equal(1, fila.Pid);
    }

    [Fact]
    public void El_comando_pide_el_dueno_el_encendido_y_el_passwd_en_una_sola_vuelta()
    {
        var comando = ParserDeProcesos.ComandoDeLectura;

        Assert.Contains("stat -c '%u %n' /proc/[0-9]*", comando);
        Assert.Contains("cat /proc/uptime", comando);
        Assert.Contains("cut -d: -f1,3 /etc/passwd", comando);

        Assert.Equal(
            ParserDeProcesos.Encabezados - 1,
            comando.Split($"; echo '{ParserDeProcesos.Marca}'; ").Length - 1);
    }
}
