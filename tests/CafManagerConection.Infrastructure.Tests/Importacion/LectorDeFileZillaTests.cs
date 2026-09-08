using System.Text;
using CafManagerConection.Domain.Importacion;
using CafManagerConection.Infrastructure.Importacion;

namespace CafManagerConection.Infrastructure.Tests.Importacion;

public sealed class LectorDeFileZillaTests
{
    private static string Documento(string servers) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <FileZilla3 version="3.66.5" platform="windows">
          <Servers>
        {servers}
          </Servers>
        </FileZilla3>
        """;

    private static string UnServidor(string campos) => Documento($"<Server>{campos}</Server>");

    private static string Sftp(string campos) =>
        UnServidor($"<Host>198.51.100.5</Host><Protocol>1</Protocol>{campos}");

    [Fact]
    public void Un_servidor_suelto_y_carpetas_anidadas_conservan_la_jerarquia()
    {
        using var lectura = LectorDeFileZilla.Leer(Documento("""
            <Server>
              <Host>198.51.100.5</Host><Protocol>1</Protocol><Name>Produccion</Name>
            </Server>
            <Folder expanded="1">
              Clientes
              <Server>
                <Host>198.51.100.6</Host><Protocol>1</Protocol><Name>Cliente A</Name>
              </Server>
              <Folder expanded="0">
                Sur
                <Server>
                  <Host>198.51.100.7</Host><Protocol>1</Protocol><Name>Bahia</Name>
                </Server>
              </Folder>
            </Folder>
            """));

        Assert.Equal(
            ["Produccion", "Clientes › Cliente A", "Clientes › Sur › Bahia"],
            lectura.Compatibles.Select(c => c.Ruta));
    }

    [Fact]
    public void Un_servidor_suelto_no_queda_en_ninguna_carpeta()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp("<Name>Produccion</Name>"));

        Assert.Empty(lectura.Compatibles.Single().Carpetas);
    }

    [Fact]
    public void El_nombre_de_la_carpeta_no_se_contamina_con_el_texto_de_los_servidores_de_adentro()
    {
        using var lectura = LectorDeFileZilla.Leer(Documento("""
            <Folder expanded="1">
              Clientes
              <Server>
                <Host>198.51.100.6</Host><Port>2222</Port><Protocol>1</Protocol>
                <User>operador</User><Name>Cliente A</Name>
              </Server>
            </Folder>
            """));

        var conexion = lectura.Compatibles.Single();

        Assert.Equal(["Clientes"], conexion.Carpetas);
        Assert.DoesNotContain("198.51.100.6", conexion.Ruta, StringComparison.Ordinal);
        Assert.DoesNotContain("operador", conexion.Ruta, StringComparison.Ordinal);
        Assert.DoesNotContain("2222", conexion.Ruta, StringComparison.Ordinal);
    }

    [Fact]
    public void El_nombre_de_la_carpeta_no_arrastra_el_de_las_carpetas_de_adentro()
    {
        using var lectura = LectorDeFileZilla.Leer(Documento("""
            <Folder expanded="1">
              Clientes
              <Folder expanded="1">
                Sur
                <Server><Host>198.51.100.7</Host><Protocol>1</Protocol><Name>Bahia</Name></Server>
              </Folder>
            </Folder>
            """));

        Assert.Equal(["Clientes", "Sur"], lectura.Compatibles.Single().Carpetas);
    }

    [Fact]
    public void Una_carpeta_sin_nombre_no_agrega_un_nivel()
    {
        using var lectura = LectorDeFileZilla.Leer(Documento("""
            <Folder expanded="1">
              <Server><Host>198.51.100.7</Host><Protocol>1</Protocol><Name>Suelto</Name></Server>
            </Folder>
            """));

        Assert.Empty(lectura.Compatibles.Single().Carpetas);
    }

    [Fact]
    public void El_protocolo_1_se_importa_como_sftp()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp("<Name>Produccion</Name>"));

        var conexion = lectura.Compatibles.Single();

        Assert.Empty(lectura.Omitidas);
        Assert.Equal("SFTP", conexion.ProtocoloOriginal);
        Assert.Equal("198.51.100.5", conexion.Host);
        Assert.Equal(OrigenDeImportacion.FileZilla, conexion.Origen);
    }

    [Fact]
    public void El_protocolo_0_se_omite_con_un_motivo_que_lo_nombra_ftp()
    {
        using var lectura = LectorDeFileZilla.Leer(
            UnServidor("<Host>198.51.100.5</Host><Protocol>0</Protocol><Name>Viejo</Name>"));

        var omitida = lectura.Omitidas.Single();

        Assert.Empty(lectura.Compatibles);
        Assert.Equal("Viejo", omitida.Nombre);
        Assert.StartsWith("FTP", omitida.Motivo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(9)]
    public void Un_protocolo_desconocido_se_omite_nombrando_el_numero_y_sin_inventarle_nombre(
        int protocolo)
    {
        using var lectura = LectorDeFileZilla.Leer(UnServidor(
            $"<Host>198.51.100.5</Host><Protocol>{protocolo}</Protocol><Name>Raro</Name>"));

        var motivo = lectura.Omitidas.Single().Motivo;

        Assert.Empty(lectura.Compatibles);
        Assert.Contains(
            protocolo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            motivo,
            StringComparison.Ordinal);

        foreach (var inventado in new[] { "FTPS", "WebDAV", "S3", "Dropbox", "OneDrive", "HTTP" })
        {
            Assert.DoesNotContain(inventado, motivo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Un_servidor_sin_protocolo_se_omite_con_motivo()
    {
        using var lectura = LectorDeFileZilla.Leer(
            UnServidor("<Host>198.51.100.5</Host><Name>Sin protocolo</Name>"));

        Assert.Empty(lectura.Compatibles);
        Assert.Contains("protocolo", lectura.Omitidas.Single().Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_protocolo_no_numerico_se_omite_sin_lanzar()
    {
        using var lectura = LectorDeFileZilla.Leer(
            UnServidor("<Host>198.51.100.5</Host><Protocol>sftp</Protocol><Name>Raro</Name>"));

        Assert.Empty(lectura.Compatibles);
        Assert.Single(lectura.Omitidas);
    }

    [Fact]
    public void Un_servidor_sin_host_se_omite_con_motivo()
    {
        using var lectura = LectorDeFileZilla.Leer(
            UnServidor("<Protocol>1</Protocol><Name>Sin host</Name>"));

        var omitida = lectura.Omitidas.Single();

        Assert.Empty(lectura.Compatibles);
        Assert.Equal("Sin host", omitida.Nombre);
        Assert.Contains("Host", omitida.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_servidor_con_host_vacio_se_omite()
    {
        using var lectura = LectorDeFileZilla.Leer(
            UnServidor("<Host></Host><Protocol>1</Protocol><Name>Sin host</Name>"));

        Assert.Empty(lectura.Compatibles);
        Assert.Single(lectura.Omitidas);
    }

    [Fact]
    public void La_contrasena_en_base64_se_decodifica()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            """<User>operador</User><Pass encoding="base64">c2VjcmV0bw==</Pass>"""
            + "<Logontype>1</Logontype><Name>Produccion</Name>"));

        var conexion = lectura.Compatibles.Single();

        Assert.True(conexion.TieneContrasena);
        Assert.Equal(1, lectura.ConContrasena);
        Assert.Equal("secreto", conexion.Credencial!.RevealSecret());
        Assert.Equal("operador", conexion.Credencial.UserName);
    }

    [Fact]
    public void La_contrasena_en_base64_se_decodifica_como_utf8()
    {
        var codificada = Convert.ToBase64String(Encoding.UTF8.GetBytes("añonuñez€"));

        using var lectura = LectorDeFileZilla.Leer(Sftp(
            $"""<User>operador</User><Pass encoding="base64">{codificada}</Pass>"""
            + "<Logontype>1</Logontype>"));

        Assert.Equal("añonuñez€", lectura.Compatibles.Single().Credencial!.RevealSecret());
    }

    [Fact]
    public void La_contrasena_cifrada_con_la_maestra_importa_la_conexion_sin_credencial()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            """<User>operador</User><Pass encoding="crypt">AAECAwQFBg==</Pass>"""
            + "<Logontype>1</Logontype><Name>Produccion</Name>"));

        var conexion = lectura.Compatibles.Single();

        Assert.Equal("Produccion", conexion.Nombre);
        Assert.Null(conexion.Credencial);
        Assert.False(conexion.TieneContrasena);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void La_contrasena_cifrada_con_la_maestra_deja_dicho_por_que()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            """<User>operador</User><Pass encoding="crypt">AAECAwQFBg==</Pass>"""
            + "<Logontype>1</Logontype><Name>Produccion</Name>"));

        Assert.Equal(
            LectorDeFileZilla.AvisoContrasenaConMaestra,
            Assert.Single(lectura.Compatibles.Single().AdvertenciasOVacio));
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Un_base64_invalido_deja_la_conexion_sin_credencial_y_no_lanza()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            """<User>operador</User><Pass encoding="base64">%%%no-es-base64%%%</Pass>"""
            + "<Logontype>1</Logontype><Name>Produccion</Name>"));

        var conexion = lectura.Compatibles.Single();

        Assert.Null(conexion.Credencial);
        Assert.Equal(
            LectorDeFileZilla.AvisoContrasenaEnBase64Invalido,
            Assert.Single(conexion.AdvertenciasOVacio));
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Ninguna_advertencia_lleva_la_contrasena_adentro()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            """<User>operador</User><Pass encoding="base64">no-es-base64-secreto</Pass>"""
            + "<Logontype>1</Logontype><Name>Produccion</Name>"));

        Assert.DoesNotContain(
            "no-es-base64-secreto",
            Assert.Single(lectura.Compatibles.Single().AdvertenciasOVacio),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Una_contrasena_que_se_decodifica_no_deja_advertencias()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            """<User>operador</User><Pass encoding="base64">c2VjcmV0bw==</Pass>"""
            + "<Logontype>1</Logontype><Name>Produccion</Name>"));

        Assert.Empty(lectura.Compatibles.Single().AdvertenciasOVacio);
    }

    [Fact]
    public void Una_contrasena_sin_atributo_de_codificacion_es_texto_plano()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            "<User>operador</User><Pass>en-claro</Pass><Logontype>1</Logontype>"));

        Assert.Equal("en-claro", lectura.Compatibles.Single().Credencial!.RevealSecret());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void Los_logontype_sin_contrasena_guardada_no_dejan_credencial_ni_motivo(int logontype)
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            """<User>anonymous</User><Pass encoding="base64">c2VjcmV0bw==</Pass>"""
            + $"<Logontype>{logontype}</Logontype><Name>Publico</Name>"));

        Assert.Null(lectura.Compatibles.Single().Credencial);
        Assert.Empty(lectura.Omitidas);
        Assert.Equal(0, lectura.ConContrasena);
    }

    [Fact]
    public void Un_servidor_sin_pass_no_deja_motivo()
    {
        using var lectura = LectorDeFileZilla.Leer(
            Sftp("<User>operador</User><Logontype>1</Logontype>"));

        Assert.Null(lectura.Compatibles.Single().Credencial);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Desechar_la_lectura_borra_las_contrasenas()
    {
        var lectura = LectorDeFileZilla.Leer(Sftp(
            """<User>operador</User><Pass encoding="base64">c2VjcmV0bw==</Pass>"""
            + "<Logontype>1</Logontype>"));

        var credencial = lectura.Compatibles.Single().Credencial!;

        lectura.Dispose();

        Assert.False(credencial.HasSecret);
    }

    [Fact]
    public void El_nombre_ausente_cae_al_host()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(string.Empty));

        Assert.Equal("198.51.100.5", lectura.Compatibles.Single().Nombre);
    }

    [Fact]
    public void El_nombre_vacio_cae_al_host()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp("<Name>   </Name>"));

        Assert.Equal("198.51.100.5", lectura.Compatibles.Single().Nombre);
    }

    [Fact]
    public void El_puerto_ausente_queda_en_null_y_no_en_22()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp("<Name>Produccion</Name>"));

        Assert.Null(lectura.Compatibles.Single().Puerto);
    }

    [Theory]
    [InlineData("no-es-un-numero")]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("99999")]
    public void Un_puerto_inutilizable_queda_en_null(string puerto)
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp($"<Port>{puerto}</Port>"));

        Assert.Null(lectura.Compatibles.Single().Puerto);
    }

    [Fact]
    public void El_puerto_guardado_se_conserva()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp("<Port>2222</Port>"));

        Assert.Equal(2222, lectura.Compatibles.Single().Puerto);
    }

    [Fact]
    public void El_keyfile_llega_a_la_ruta_de_clave_privada()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp(
            @"<Logontype>5</Logontype><Keyfile>C:\claves\id_rsa.ppk</Keyfile>"));

        var conexion = lectura.Compatibles.Single();

        Assert.Equal(@"C:\claves\id_rsa.ppk", conexion.RutaDeClavePrivada);
        Assert.Null(conexion.Credencial);
    }

    [Fact]
    public void Sin_keyfile_la_ruta_de_clave_privada_queda_en_null()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp("<Name>Produccion</Name>"));

        Assert.Null(lectura.Compatibles.Single().RutaDeClavePrivada);
    }

    [Fact]
    public void El_usuario_ausente_queda_en_null()
    {
        using var lectura = LectorDeFileZilla.Leer(Sftp("<User></User>"));

        Assert.Null(lectura.Compatibles.Single().Usuario);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_xml_vacio_no_lanza(string xml)
    {
        using var lectura = LectorDeFileZilla.Leer(xml);

        Assert.Empty(lectura.Compatibles);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Un_xml_mal_formado_no_lanza_y_deja_dicho_que_no_se_pudo_leer()
    {
        using var lectura = LectorDeFileZilla.Leer(
            """<FileZilla3><Servers><Server><Host>198.51.100.5</Host>""");

        Assert.Empty(lectura.Compatibles);
        Assert.Contains("sitemanager", lectura.Omitidas.Single().Nombre, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_xml_que_no_es_xml_no_lanza()
    {
        using var lectura = LectorDeFileZilla.Leer("esto no es un archivo de FileZilla");

        Assert.Empty(lectura.Compatibles);
        Assert.Single(lectura.Omitidas);
    }

    [Fact]
    public void Servers_vacio_no_lanza()
    {
        using var lectura = LectorDeFileZilla.Leer(Documento(string.Empty));

        Assert.Empty(lectura.Compatibles);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Un_xml_sin_servers_no_lanza()
    {
        using var lectura = LectorDeFileZilla.Leer(
            """<?xml version="1.0"?><FileZilla3 version="3.66.5"><Settings /></FileZilla3>""");

        Assert.Empty(lectura.Compatibles);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void Los_elementos_desconocidos_dentro_de_servers_se_ignoran()
    {
        using var lectura = LectorDeFileZilla.Leer(Documento("""
            <Bookmark><Name>Marcador</Name></Bookmark>
            <Server><Host>198.51.100.5</Host><Protocol>1</Protocol><Name>Produccion</Name></Server>
            """));

        Assert.Equal("Produccion", lectura.Compatibles.Single().Nombre);
        Assert.Empty(lectura.Omitidas);
    }

    [Fact]
    public void RutaHabitual_apunta_al_sitemanager_de_filezilla_en_appdata()
    {
        var ruta = LectorDeFileZilla.RutaHabitual();

        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileZilla",
                "sitemanager.xml"),
            ruta);
    }
}
