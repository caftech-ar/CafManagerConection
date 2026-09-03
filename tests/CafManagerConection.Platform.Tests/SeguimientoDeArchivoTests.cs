using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class ComandoDeSeguimientoTests
{
    [Fact]
    public void Sigue_por_nombre_para_sobrevivir_a_la_rotacion()
    {
        var comando = SeguimientoDeArchivo.Comando("/var/log/api.log");

        Assert.Contains(" -F ", comando);
        Assert.DoesNotContain(" -f ", comando);
    }

    [Fact]
    public void Pide_en_ingles_para_poder_interpretar_lo_que_diga_tail()
    {
        Assert.StartsWith("LC_ALL=C ", SeguimientoDeArchivo.Comando("/var/log/api.log"));
    }

    [Fact]
    public void Trae_las_ultimas_lineas_y_junta_el_canal_de_error()
    {
        var comando = SeguimientoDeArchivo.Comando("/var/log/api.log", lineas: 50);

        Assert.Contains("-n 50", comando);
        Assert.EndsWith("2>&1", comando);
    }

    [Fact]
    public void La_ruta_va_entre_comillas_simples_y_despues_del_doble_guion()
    {
        Assert.Contains("-- '/var/log/api.log'", SeguimientoDeArchivo.Comando("/var/log/api.log"));
    }

    [Fact]
    public void Una_comilla_en_la_ruta_no_puede_cerrar_la_cita()
    {
        var comando = SeguimientoDeArchivo.Comando("/var/log/de'todo.log");

        Assert.Contains(@"'/var/log/de'\''todo.log'", comando);
    }

    [Theory]
    [InlineData("/var/log/api.log", true)]
    [InlineData("/var/log/con espacio.log", true)]
    [InlineData("relativa.log", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("/var/log/api.log\nrm -rf /", false)]
    [InlineData("/var/log/api\u0007.log", false)]
    public void Solo_se_acepta_una_ruta_absoluta_y_sin_control(string? ruta, bool esperado)
    {
        Assert.Equal(esperado, SeguimientoDeArchivo.RutaAceptable(ruta));
    }

    [Fact]
    public void Una_ruta_inaceptable_no_arma_comando()
    {
        Assert.Throws<ArgumentException>(() => SeguimientoDeArchivo.Comando("relativa.log"));
    }
}

public sealed class DiagnosticoDeSeguimientoTests
{
    [Fact]
    public void Una_linea_normal_no_es_un_aviso()
    {
        Assert.Null(SeguimientoDeArchivo.Diagnostico("2026-09-03 10:00:00 arranco el servicio"));
    }

    [Fact]
    public void Una_linea_del_registro_que_habla_de_tail_no_es_un_aviso()
    {
        Assert.Null(SeguimientoDeArchivo.Diagnostico("el proceso corre tail: cannot open el log"));
    }

    [Fact]
    public void El_archivo_borrado_deja_de_poder_leerse()
    {
        var aviso = SeguimientoDeArchivo.Diagnostico(
            "tail: '/var/log/api.log' has become inaccessible: No such file or directory");

        Assert.NotNull(aviso);
        Assert.Equal(ClaseDeAviso.Inaccesible, aviso.Clase);
    }

    [Fact]
    public void El_archivo_que_no_existe_deja_de_poder_leerse()
    {
        var aviso = SeguimientoDeArchivo.Diagnostico(
            "tail: cannot open '/var/log/api.log' for reading: No such file or directory");

        Assert.NotNull(aviso);
        Assert.Equal(ClaseDeAviso.Inaccesible, aviso.Clase);
    }

    [Fact]
    public void El_permiso_denegado_deja_de_poder_leerse_y_lo_dice()
    {
        var aviso = SeguimientoDeArchivo.Diagnostico(
            "tail: cannot open '/var/log/api.log' for reading: Permission denied");

        Assert.NotNull(aviso);
        Assert.Equal(ClaseDeAviso.Inaccesible, aviso.Clase);
        Assert.Contains("permiso", aviso.Texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_rotacion_no_es_un_fallo()
    {
        var aviso = SeguimientoDeArchivo.Diagnostico(
            "tail: '/var/log/api.log' has been replaced;  following new file");

        Assert.NotNull(aviso);
        Assert.Equal(ClaseDeAviso.Rotacion, aviso.Clase);
    }

    [Fact]
    public void El_archivo_que_volvio_a_aparecer_tampoco_es_un_fallo()
    {
        var aviso = SeguimientoDeArchivo.Diagnostico(
            "tail: '/var/log/api.log' has appeared;  following new file");

        Assert.NotNull(aviso);
        Assert.Equal(ClaseDeAviso.Rotacion, aviso.Clase);
    }

    [Fact]
    public void La_advertencia_de_retry_no_alarma_a_nadie()
    {
        Assert.Null(SeguimientoDeArchivo.Diagnostico(
            "tail: warning: --retry only effective for the initial open"));
    }

    [Fact]
    public void El_error_de_lectura_deja_de_poder_leerse()
    {
        var aviso = SeguimientoDeArchivo.Diagnostico(
            "tail: error reading '/var/log/api.log': Input/output error");

        Assert.NotNull(aviso);
        Assert.Equal(ClaseDeAviso.Inaccesible, aviso.Clase);
    }
}

public sealed class FechasDeArchivoSeguidoTests
{
    [Fact]
    public void La_consulta_pide_la_fecha_y_el_nombre_de_cada_ruta()
    {
        var comando = SeguimientoDeArchivo.ComandoDeFechas(["/var/log/a.log", "/var/log/b.log"]);

        Assert.Contains("stat", comando);
        Assert.Contains("'/var/log/a.log'", comando);
        Assert.Contains("'/var/log/b.log'", comando);
    }

    [Fact]
    public void Sin_rutas_no_hay_comando()
    {
        Assert.Equal(string.Empty, SeguimientoDeArchivo.ComandoDeFechas([]));
    }

    [Fact]
    public void La_fecha_que_contesta_el_servidor_es_la_del_archivo()
    {
        var leidos = SeguimientoDeArchivo.LeerFechas(
            ["/var/log/a.log"], "1772539200|/var/log/a.log");

        Assert.Single(leidos);
        Assert.Equal("/var/log/a.log", leidos[0].Ruta);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1772539200), leidos[0].Cambiado);
        Assert.Null(leidos[0].Falla);
    }

    [Fact]
    public void Se_conserva_el_orden_en_el_que_se_pidieron()
    {
        var leidos = SeguimientoDeArchivo.LeerFechas(
            ["/var/log/a.log", "/var/log/b.log"],
            "1772539201|/var/log/b.log\n1772539200|/var/log/a.log");

        Assert.Equal("/var/log/a.log", leidos[0].Ruta);
        Assert.Equal("/var/log/b.log", leidos[1].Ruta);
    }

    [Fact]
    public void El_archivo_que_stat_no_encontro_queda_con_su_falla_y_no_con_una_fecha()
    {
        var leidos = SeguimientoDeArchivo.LeerFechas(
            ["/var/log/a.log"],
            "stat: cannot stat '/var/log/a.log': No such file or directory");

        Assert.Single(leidos);
        Assert.Null(leidos[0].Cambiado);
        Assert.NotNull(leidos[0].Falla);
    }

    [Fact]
    public void El_archivo_del_que_el_servidor_no_dijo_nada_no_inventa_fecha()
    {
        var leidos = SeguimientoDeArchivo.LeerFechas(["/var/log/a.log"], string.Empty);

        Assert.Single(leidos);
        Assert.Null(leidos[0].Cambiado);
        Assert.NotNull(leidos[0].Falla);
    }

    [Fact]
    public void Una_ruta_con_barra_vertical_se_lee_completa()
    {
        var leidos = SeguimientoDeArchivo.LeerFechas(
            ["/var/log/a|b.log"], "1772539200|/var/log/a|b.log");

        Assert.Equal("/var/log/a|b.log", leidos[0].Ruta);
        Assert.NotNull(leidos[0].Cambiado);
    }
}

public sealed class CambioDeArchivoSeguidoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(3, "hace 3 s")]
    [InlineData(90, "hace 1 min")]
    [InlineData(7200, "hace 2 h")]
    public void El_cambio_reciente_se_cuenta_en_lo_que_paso(int segundos, string esperado)
    {
        var archivo = new ArchivoSeguido("/var/log/a.log", Ahora.AddSeconds(-segundos));

        Assert.Equal(esperado, archivo.Cambio(Ahora));
    }

    [Fact]
    public void El_cambio_viejo_se_dice_con_su_fecha()
    {
        var archivo = new ArchivoSeguido("/var/log/a.log", Ahora.AddDays(-3));

        Assert.Contains("31/08", archivo.Cambio(Ahora));
    }

    [Fact]
    public void Sin_fecha_se_dice_la_falla_y_no_un_guion()
    {
        var archivo = new ArchivoSeguido("/var/log/a.log", null, "no existe");

        Assert.Equal("no existe", archivo.Cambio(Ahora));
    }

    [Fact]
    public void Sin_fecha_ni_falla_no_se_afirma_nada()
    {
        Assert.Equal("sin fecha", new ArchivoSeguido("/var/log/a.log", null).Cambio(Ahora));
    }
}

public sealed class RegistrosAbiertosDeUnProcesoTests
{
    [Theory]
    [InlineData("pid 1234, uptime 0:12:34", 1234)]
    [InlineData("RUNNING   pid 9, uptime 1 day, 2:03:04", 9)]
    [InlineData("Not started", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("exited too quickly (pid 4242)", 4242)]
    public void El_pid_sale_del_detalle_que_da_supervisorctl(string? detalle, int? esperado)
    {
        Assert.Equal(esperado, SeguimientoDeArchivo.PidDeSupervisor(detalle));
    }

    [Fact]
    public void Se_preguntan_las_dos_salidas_del_proceso()
    {
        var comando = SeguimientoDeArchivo.ComandoDeRegistrosAbiertos(1234);

        Assert.Contains("/proc/1234/fd/1", comando);
        Assert.Contains("/proc/1234/fd/2", comando);
    }

    [Fact]
    public void Un_pid_imposible_no_arma_comando()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SeguimientoDeArchivo.ComandoDeRegistrosAbiertos(0));
    }

    [Fact]
    public void Las_dos_salidas_al_mismo_archivo_se_declaran_una_vez()
    {
        var rutas = SeguimientoDeArchivo.LeerRegistrosAbiertos(
            "/var/log/api.log\n/var/log/api.log");

        Assert.Equal(["/var/log/api.log"], rutas);
    }

    [Fact]
    public void Lo_que_no_es_un_archivo_no_se_sigue()
    {
        var rutas = SeguimientoDeArchivo.LeerRegistrosAbiertos(
            "/dev/null\npipe:[12345]\nsocket:[9]\n/dev/pts/3\n/var/log/api.log");

        Assert.Equal(["/var/log/api.log"], rutas);
    }

    [Fact]
    public void Una_ruta_borrada_no_se_sigue()
    {
        var rutas = SeguimientoDeArchivo.LeerRegistrosAbiertos(
            "/var/log/viejo.log (deleted)\n/var/log/api.log");

        Assert.Equal(["/var/log/api.log"], rutas);
    }

    [Fact]
    public void Sin_salida_no_hay_nada_que_seguir()
    {
        Assert.Empty(SeguimientoDeArchivo.LeerRegistrosAbiertos(string.Empty));
    }
}
