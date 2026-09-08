using System.Globalization;
using System.Text;
using CafManagerConection.Domain.Importacion;
using CafManagerConection.Infrastructure.Importacion;

namespace CafManagerConection.Infrastructure.Tests.Importacion;

public sealed class LectorDeWinScpTests
{
    private const string IniConLosCincoNombresReales = """
        [Configuration\Interface]
        Theme=Dark

        [Sessions\carpeta%201/ssss/Servidor%20Produccion]
        HostName=198.51.100.31
        PortNumber=2222
        UserName=operador

        [Sessions\carpeta%201/xxxxx]
        HostName=198.51.100.32

        [Sessions\Default%20Settings]
        FSProtocol=2

        [Sessions\Servidor%20Produccion%20(1)]
        HostName=198.51.100.33

        [Sessions\xxxxx]
        HostName=198.51.100.34
        """;

    [Fact]
    public void Los_cinco_nombres_reales_se_leen_con_sus_carpetas_y_su_nombre_decodificados()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniConLosCincoNombresReales);

        Assert.Equal(
            new[]
            {
                "carpeta 1 › ssss › Servidor Produccion",
                "carpeta 1 › xxxxx",
                "Servidor Produccion (1)",
                "xxxxx",
            },
            lectura.Compatibles.Select(c => c.Ruta).ToArray());
    }

    [Fact]
    public void La_sesion_anidada_conserva_el_orden_de_las_carpetas()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniConLosCincoNombresReales);

        var anidada = lectura.Compatibles[0];

        Assert.Equal(new[] { "carpeta 1", "ssss" }, anidada.Carpetas.ToArray());
        Assert.Equal("Servidor Produccion", anidada.Nombre);
    }

    [Fact]
    public void Las_sesiones_del_ini_se_marcan_con_el_origen_del_ini()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniConLosCincoNombresReales);

        Assert.All(
            lectura.Compatibles,
            c => Assert.Equal(OrigenDeImportacion.WinScpIni, c.Origen));
    }

    [Fact]
    public void Default_Settings_no_aparece_ni_entre_las_compatibles_ni_entre_las_omitidas()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniConLosCincoNombresReales);

        Assert.DoesNotContain("Default Settings", lectura.Compatibles.Select(c => c.Nombre));
        Assert.DoesNotContain("Default Settings", lectura.Omitidas.Select(o => o.Nombre));
    }

    [Fact]
    public void Una_sesion_llamada_Default_Settings_dentro_de_una_carpeta_si_se_importa()
    {
        using var lectura = LectorDeWinScp.LeerIni(
            "[Sessions\\carpeta%201/Default%20Settings]\nHostName=198.51.100.31");

        Assert.Equal("carpeta 1 › Default Settings", Assert.Single(lectura.Compatibles).Ruta);
    }

    [Fact]
    public void Una_barra_codificada_no_separa_carpetas()
    {
        var ruta = LectorDeWinScp.SepararRuta("carpeta%201/produccion%2Frespaldo");

        Assert.Equal(new[] { "carpeta 1" }, ruta.Carpetas.ToArray());
        Assert.Equal("produccion/respaldo", ruta.Nombre);
    }

    [Fact]
    public void Un_nombre_suelto_no_tiene_carpetas()
    {
        var ruta = LectorDeWinScp.SepararRuta("xxxxx");

        Assert.Empty(ruta.Carpetas);
        Assert.Equal("xxxxx", ruta.Nombre);
    }

    [Fact]
    public void El_signo_mas_no_se_decodifica_como_espacio()
    {
        Assert.Equal("a+b", LectorDeWinScp.DecodificarNombre("a+b"));
    }

    [Fact]
    public void Un_escapado_invalido_se_deja_tal_cual()
    {
        Assert.Equal("100%zz", LectorDeWinScp.DecodificarNombre("100%zz"));
    }

    [Theory]
    [InlineData(0, "SCP")]
    [InlineData(1, "SFTP (con respaldo SCP)")]
    [InlineData(2, "SFTP")]
    public void Los_protocolos_que_van_sobre_ssh_se_importan(int fsProtocol, string esperado)
    {
        using var lectura = LectorDeWinScp.LeerIni(IniDeUnaSesion($"FSProtocol={fsProtocol}"));

        Assert.Equal(esperado, Assert.Single(lectura.Compatibles).ProtocoloOriginal);
        Assert.Empty(lectura.Omitidas);
    }

    [Theory]
    [InlineData(3, "FTP")]
    [InlineData(4, "WebDAV")]
    [InlineData(5, "S3")]
    [InlineData(99, "99")]
    public void Los_protocolos_que_no_van_sobre_ssh_se_omiten_nombrandolos(
        int fsProtocol, string nombrado)
    {
        using var lectura = LectorDeWinScp.LeerIni(IniDeUnaSesion($"FSProtocol={fsProtocol}"));

        Assert.Empty(lectura.Compatibles);
        Assert.Contains(nombrado, Assert.Single(lectura.Omitidas).Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_FSProtocol_que_no_es_numero_se_omite()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniDeUnaSesion("FSProtocol=sftp"));

        Assert.Empty(lectura.Compatibles);
        Assert.Contains("sftp", Assert.Single(lectura.Omitidas).Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_FSProtocol_la_sesion_se_importa_como_SFTP()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniDeUnaSesion());

        Assert.Equal("SFTP", Assert.Single(lectura.Compatibles).ProtocoloOriginal);
    }

    [Fact]
    public void Sin_PortNumber_el_puerto_queda_en_null()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniDeUnaSesion());

        Assert.Null(Assert.Single(lectura.Compatibles).Puerto);
    }

    [Fact]
    public void Con_PortNumber_se_usa_el_puerto_guardado()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniDeUnaSesion("PortNumber=2222"));

        Assert.Equal(2222, Assert.Single(lectura.Compatibles).Puerto);
    }

    [Fact]
    public void Un_PortNumber_fuera_de_rango_queda_en_null()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniDeUnaSesion("PortNumber=99999"));

        Assert.Null(Assert.Single(lectura.Compatibles).Puerto);
    }

    [Fact]
    public void Una_sesion_sin_HostName_se_omite_nombrando_el_valor_que_falta()
    {
        using var lectura = LectorDeWinScp.LeerIni("[Sessions\\xxxxx]\nUserName=operador");

        Assert.Empty(lectura.Compatibles);

        var omitida = Assert.Single(lectura.Omitidas);

        Assert.Equal("xxxxx", omitida.Nombre);
        Assert.Contains("HostName", omitida.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void La_ruta_de_la_clave_privada_sale_de_PublicKeyFile()
    {
        using var lectura = LectorDeWinScp.LeerIni(
            IniDeUnaSesion(@"PublicKeyFile=C:\Users\operador\.ssh\id.ppk"));

        Assert.Equal(
            @"C:\Users\operador\.ssh\id.ppk",
            Assert.Single(lectura.Compatibles).RutaDeClavePrivada);
    }

    [Fact]
    public void Las_secciones_que_no_son_de_sesiones_se_ignoran()
    {
        using var lectura = LectorDeWinScp.LeerIni("""
            [Configuration\Interface]
            HostName=198.51.100.99

            [Logging]
            HostName=198.51.100.98
            """);

        Assert.Empty(lectura.Compatibles);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Los_comentarios_las_lineas_vacias_y_los_espacios_no_rompen_la_lectura()
    {
        using var lectura = LectorDeWinScp.LeerIni("""
            ; esto es un comentario

            [ Sessions\xxxxx ]
              HostName = 198.51.100.31
            ; PortNumber=22
              UserName=operador

            """);

        var conexion = Assert.Single(lectura.Compatibles);

        Assert.Equal("198.51.100.31", conexion.Host);
        Assert.Equal("operador", conexion.Usuario);
        Assert.Null(conexion.Puerto);
    }

    [Fact]
    public void Un_ini_con_finales_de_linea_de_windows_se_lee_igual()
    {
        using var lectura = LectorDeWinScp.LeerIni(
            "[Sessions\\xxxxx]\r\nHostName=198.51.100.31\r\nPortNumber=2222\r\n");

        Assert.Equal(2222, Assert.Single(lectura.Compatibles).Puerto);
    }

    [Fact]
    public void Un_ini_vacio_no_devuelve_nada()
    {
        using var lectura = LectorDeWinScp.LeerIni(string.Empty);

        Assert.Empty(lectura.Compatibles);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void La_contrasena_ofuscada_se_recupera_entera()
    {
        var hex = Ofuscar("operador198.51.100.31" + "tr3s.Tigres");

        Assert.Equal("tr3s.Tigres", LectorDeWinScp.DecodificarContrasena(hex, "operador", "198.51.100.31"));
    }

    [Fact]
    public void La_contrasena_con_largo_corto_tambien_se_recupera()
    {
        var hex = Ofuscar("operador198.51.100.31" + "tr3s.Tigres", largoExtendido: false);

        Assert.Equal("tr3s.Tigres", LectorDeWinScp.DecodificarContrasena(hex, "operador", "198.51.100.31"));
    }

    [Fact]
    public void La_contrasena_sin_bytes_de_relleno_tambien_se_recupera()
    {
        var hex = Ofuscar("operador198.51.100.31" + "tr3s.Tigres", salto: 0);

        Assert.Equal("tr3s.Tigres", LectorDeWinScp.DecodificarContrasena(hex, "operador", "198.51.100.31"));
    }

    [Fact]
    public void Con_el_usuario_equivocado_no_se_devuelve_contrasena()
    {
        var hex = Ofuscar("operador198.51.100.31" + "tr3s.Tigres");

        Assert.Null(LectorDeWinScp.DecodificarContrasena(hex, "otro", "198.51.100.31"));
    }

    [Fact]
    public void Con_el_host_equivocado_no_se_devuelve_contrasena()
    {
        var hex = Ofuscar("operador198.51.100.31" + "tr3s.Tigres");

        Assert.Null(LectorDeWinScp.DecodificarContrasena(hex, "operador", "198.51.100.32"));
    }

    [Fact]
    public void Sin_usuario_ni_host_no_hay_nada_que_verificar_y_no_se_devuelve_contrasena()
    {
        var hex = Ofuscar("tr3s.Tigres");

        Assert.Null(LectorDeWinScp.DecodificarContrasena(hex, string.Empty, string.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("ABCDE")]
    [InlineData("ZZZZZZ")]
    [InlineData("12 34")]
    [InlineData("FF")]
    [InlineData("A3")]
    public void Un_hex_invalido_o_truncado_no_devuelve_contrasena_ni_lanza(string hex)
    {
        Assert.Null(LectorDeWinScp.DecodificarContrasena(hex, "operador", "198.51.100.31"));
    }

    [Fact]
    public void La_contrasena_del_ini_llega_como_credencial_de_la_conexion()
    {
        using var lectura = LectorDeWinScp.LeerIni(
            IniDeUnaSesion("UserName=operador", "Password=" + Ofuscar("operador198.51.100.31tr3s.Tigres")));

        var conexion = Assert.Single(lectura.Compatibles);

        var credencial = conexion.Credencial!;

        Assert.True(conexion.TieneContrasena);
        Assert.Equal("tr3s.Tigres", credencial.RevealSecret());
        Assert.Equal("operador", credencial.UserName);
        Assert.Equal(1, lectura.ConContrasena);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Una_contrasena_que_no_se_verifica_importa_la_conexion_sin_credencial_y_lo_advierte()
    {
        using var lectura = LectorDeWinScp.LeerIni(
            IniDeUnaSesion("UserName=operador", "Password=" + Ofuscar("otro198.51.100.31tr3s.Tigres")));

        var conexion = Assert.Single(lectura.Compatibles);

        Assert.Null(conexion.Credencial);
        Assert.False(conexion.TieneContrasena);
        Assert.Equal(
            LectorDeWinScp.AvisoContrasenaSinVerificar,
            Assert.Single(conexion.AdvertenciasOVacio));
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void La_advertencia_de_la_contrasena_que_no_verifica_no_lleva_la_contrasena_adentro()
    {
        using var lectura = LectorDeWinScp.LeerIni(
            IniDeUnaSesion("UserName=operador", "Password=" + Ofuscar("otro198.51.100.31tr3s.Tigres")));

        Assert.DoesNotContain(
            "tr3s.Tigres",
            Assert.Single(Assert.Single(lectura.Compatibles).AdvertenciasOVacio),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Una_contrasena_que_verifica_no_deja_advertencias()
    {
        using var lectura = LectorDeWinScp.LeerIni(
            IniDeUnaSesion("UserName=operador", "Password=" + Ofuscar("operador198.51.100.31tr3s.Tigres")));

        Assert.Empty(Assert.Single(lectura.Compatibles).AdvertenciasOVacio);
    }

    [Fact]
    public void Sin_Password_la_conexion_no_trae_credencial_ni_se_informa_nada()
    {
        using var lectura = LectorDeWinScp.LeerIni(IniDeUnaSesion("UserName=operador"));

        Assert.Null(Assert.Single(lectura.Compatibles).Credencial);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Desechar_la_lectura_borra_el_secreto_de_la_memoria()
    {
        var lectura = LectorDeWinScp.LeerIni(
            IniDeUnaSesion("UserName=operador", "Password=" + Ofuscar("operador198.51.100.31tr3s.Tigres")));

        var credencial = Assert.Single(lectura.Compatibles).Credencial!;

        lectura.Dispose();

        Assert.False(credencial.HasSecret);
    }

    [Fact]
    public void Las_rutas_habituales_del_ini_son_absolutas_distintas_y_apuntan_al_archivo()
    {
        var rutas = LectorDeWinScp.RutasHabitualesDelIni().ToArray();

        Assert.NotEmpty(rutas);
        Assert.Equal(rutas.Length, rutas.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(rutas, ruta => Assert.True(Path.IsPathFullyQualified(ruta)));
        Assert.All(rutas, ruta => Assert.Equal("WinSCP.ini", Path.GetFileName(ruta)));
    }

    [Fact]
    public void Leer_el_registro_devuelve_una_lectura_aunque_no_haya_sesiones()
    {
        using var lectura = LectorDeWinScp.LeerRegistro();

        Assert.DoesNotContain("Default Settings", lectura.Compatibles.Select(c => c.Nombre));
        Assert.All(lectura.Compatibles, c => Assert.NotEmpty(c.Host));
        Assert.All(
            lectura.Compatibles,
            c => Assert.Equal(OrigenDeImportacion.WinScpRegistro, c.Origen));
    }

    private static string IniDeUnaSesion(params string[] valores) =>
        string.Join('\n', ["[Sessions\\xxxxx]", "HostName=198.51.100.31", .. valores]);

    private static string Ofuscar(string claveMasContrasena, bool largoExtendido = true, int salto = 3)
    {
        var utiles = Encoding.UTF8.GetBytes(claveMasContrasena);
        var crudos = new List<byte>();

        if (largoExtendido)
        {
            crudos.Add(0xFF);
            crudos.Add(0x00);
        }

        crudos.Add((byte)utiles.Length);
        crudos.Add((byte)salto);
        crudos.AddRange(Enumerable.Repeat((byte)0x5A, salto));
        crudos.AddRange(utiles);

        return string.Concat(crudos.Select(
            b => (((~b) & 0xFF) ^ 0xA3).ToString("X2", CultureInfo.InvariantCulture)));
    }
}
