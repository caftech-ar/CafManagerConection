using CafManagerConection.Domain.Importacion;
using CafManagerConection.Infrastructure.Importacion;

namespace CafManagerConection.Infrastructure.Tests.Importacion;

public sealed class LectorDePuttyTests
{
    private const string NombreReal = "Servidor%20Produccion";

    private static LectorDePutty.ResultadoDeSesion Convertir(
        string nombreCrudo = NombreReal,
        string? hostName = "198.51.100.31",
        int? portNumber = 22,
        string? protocol = "ssh",
        string? userName = null,
        string? publicKeyFile = null) =>
        LectorDePutty.DesdeValores(
            nombreCrudo, hostName, portNumber, protocol, userName, publicKeyFile);

    private static ConexionImportada Conexion(
        string nombreCrudo = NombreReal,
        string? hostName = "198.51.100.31",
        int? portNumber = 22,
        string? protocol = "ssh",
        string? userName = null,
        string? publicKeyFile = null)
    {
        var resultado = Convertir(
            nombreCrudo, hostName, portNumber, protocol, userName, publicKeyFile);

        Assert.Null(resultado.Omitida);

        return Assert.IsType<ConexionImportada>(resultado.Conexion);
    }

    private static ImportacionOmitida Omitida(
        string nombreCrudo = NombreReal,
        string? hostName = "198.51.100.31",
        int? portNumber = 22,
        string? protocol = "ssh",
        string? userName = null,
        string? publicKeyFile = null)
    {
        var resultado = Convertir(
            nombreCrudo, hostName, portNumber, protocol, userName, publicKeyFile);

        Assert.Null(resultado.Conexion);

        return Assert.IsType<ImportacionOmitida>(resultado.Omitida);
    }

    [Theory]
    [InlineData("Servidor%20Produccion", "Servidor Produccion")]
    [InlineData("xxxxx", "xxxxx")]
    [InlineData("Default%20Settings", "Default Settings")]
    public void Decodifica_los_nombres_tal_como_los_guarda_PuTTY(string crudo, string esperado) =>
        Assert.Equal(esperado, LectorDePutty.DecodificarNombre(crudo));

    [Theory]
    [InlineData("Ruta%5Ccon%5Cbarras", @"Ruta\con\barras")]
    [InlineData("Que%3F", "Que?")]
    [InlineData("Cien%25", "Cien%")]
    [InlineData("%2Eoculta", ".oculta")]
    public void Decodifica_los_caracteres_que_PuTTY_escapa(string crudo, string esperado) =>
        Assert.Equal(esperado, LectorDePutty.DecodificarNombre(crudo));

    [Fact]
    public void Los_escapes_consecutivos_forman_un_caracter_de_varios_bytes() =>
        Assert.Equal("Producción", LectorDePutty.DecodificarNombre("Producci%C3%B3n"));

    [Fact]
    public void Un_porcentaje_que_no_arranca_un_escape_valido_queda_literal() =>
        Assert.Equal("100%ok y 50%", LectorDePutty.DecodificarNombre("100%ok y 50%"));

    [Fact]
    public void Decodificar_un_nombre_nulo_no_se_deja_pasar() =>
        Assert.Throws<ArgumentNullException>(() => LectorDePutty.DecodificarNombre(null!));

    [Fact]
    public void El_nombre_decodificado_llega_a_la_conexion() =>
        Assert.Equal("Servidor Produccion", Conexion().Nombre);

    [Fact]
    public void La_barra_es_parte_del_nombre_porque_PuTTY_no_tiene_carpetas()
    {
        var conexion = Conexion("Prod/Backup");

        Assert.Equal("Prod/Backup", conexion.Nombre);
        Assert.Empty(conexion.Carpetas);
    }

    [Theory]
    [InlineData("xxxxx")]
    [InlineData("Servidor%20Produccion")]
    [InlineData("Clientes/Norte/Base")]
    [InlineData("Ruta%5Ccon%5Cbarras")]
    public void Las_carpetas_van_siempre_vacias(string nombreCrudo)
    {
        var conexion = Conexion(nombreCrudo);

        Assert.Empty(conexion.Carpetas);
        Assert.Equal(conexion.Nombre, conexion.Ruta);
    }

    [Fact]
    public void La_sesion_Default_Settings_no_es_compatible_ni_omitida()
    {
        var resultado = Convertir("Default%20Settings", hostName: null, protocol: null);

        Assert.True(resultado.EsIgnorada);
        Assert.Null(resultado.Conexion);
        Assert.Null(resultado.Omitida);
    }

    [Theory]
    [InlineData("WinSCP%20temporary%20session")]
    [InlineData("WinSCP temporary session")]
    public void La_sesion_temporal_que_escribe_WinSCP_no_es_compatible_ni_omitida(string crudo)
    {
        var resultado = Convertir(crudo);

        Assert.True(resultado.EsIgnorada);
        Assert.Null(resultado.Conexion);
        Assert.Null(resultado.Omitida);
    }

    [Theory]
    [InlineData("ssh")]
    [InlineData("SSH")]
    [InlineData("ssh-connection")]
    public void Las_sesiones_SSH_se_importan(string protocol) =>
        Assert.Equal(LectorDePutty.ProtocoloSsh, Conexion(protocol: protocol).ProtocoloOriginal);

    [Theory]
    [InlineData("telnet")]
    [InlineData("serial")]
    [InlineData("raw")]
    [InlineData("rlogin")]
    public void Los_protocolos_que_no_son_SSH_se_omiten_con_su_nombre_en_el_motivo(string protocol)
    {
        var omitida = Omitida(protocol: protocol);

        Assert.Equal(OrigenDeImportacion.Putty, omitida.Origen);
        Assert.Equal("Servidor Produccion", omitida.Nombre);
        Assert.Contains(protocol, omitida.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sin_protocolo_guardado_no_se_supone_SSH_y_se_omite_con_motivo() =>
        Assert.Contains("protocolo", Omitida(protocol: null).Motivo, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void El_usuario_pegado_al_HostName_se_separa_del_servidor()
    {
        var conexion = Conexion(hostName: "operador@198.51.100.31");

        Assert.Equal("198.51.100.31", conexion.Host);
        Assert.Equal("operador", conexion.Usuario);
    }

    [Fact]
    public void UserName_le_gana_al_usuario_pegado_al_HostName()
    {
        var conexion = Conexion(hostName: "viejo@198.51.100.31", userName: "nuevo");

        Assert.Equal("198.51.100.31", conexion.Host);
        Assert.Equal("nuevo", conexion.Usuario);
    }

    [Fact]
    public void El_usuario_se_separa_por_el_ultimo_arroba()
    {
        var conexion = Conexion(hostName: "alguien@dominio.com@servidor");

        Assert.Equal("servidor", conexion.Host);
        Assert.Equal("alguien@dominio.com", conexion.Usuario);
    }

    [Fact]
    public void Un_arroba_sin_usuario_delante_deja_el_usuario_en_null()
    {
        var conexion = Conexion(hostName: "@servidor");

        Assert.Equal("servidor", conexion.Host);
        Assert.Null(conexion.Usuario);
    }

    [Fact]
    public void Sin_UserName_ni_arroba_el_usuario_queda_en_null() =>
        Assert.Null(Conexion().Usuario);

    [Fact]
    public void PortNumber_ausente_deja_el_puerto_en_null_para_que_herede_del_arbol() =>
        Assert.Null(Conexion(portNumber: null).Puerto);

    [Theory]
    [InlineData(22)]
    [InlineData(2222)]
    [InlineData(65535)]
    public void PortNumber_guardado_se_respeta(int portNumber) =>
        Assert.Equal(portNumber, Conexion(portNumber: portNumber).Puerto);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void Un_PortNumber_que_no_es_un_puerto_se_trata_como_ausente(int portNumber) =>
        Assert.Null(Conexion(portNumber: portNumber).Puerto);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Una_sesion_sin_HostName_se_omite_con_motivo(string? hostName)
    {
        var omitida = Omitida(hostName: hostName);

        Assert.Contains("servidor", omitida.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_ruta_del_ppk_llega_a_RutaDeClavePrivada() =>
        Assert.Equal(
            @"C:\Users\yo\claves\produccion.ppk",
            Conexion(publicKeyFile: @"C:\Users\yo\claves\produccion.ppk").RutaDeClavePrivada);

    [Fact]
    public void Una_sesion_con_clave_ppk_avisa_que_puede_necesitar_conversion()
    {
        var conexion = Conexion(publicKeyFile: @"C:\claves\produccion.ppk");

        Assert.Equal(LectorDePutty.ProtocoloSsh, conexion.ProtocoloOriginal);
        Assert.Equal([LectorDePutty.AvisoDeClavePutty], conexion.AdvertenciasOVacio);
    }

    [Fact]
    public void Sin_clave_privada_no_hay_ninguna_advertencia() =>
        Assert.Empty(Conexion().AdvertenciasOVacio);

    [Fact]
    public void Sin_clave_privada_la_ruta_queda_en_null() =>
        Assert.Null(Conexion().RutaDeClavePrivada);

    [Theory]
    [InlineData(null)]
    [InlineData(@"C:\claves\produccion.ppk")]
    public void PuTTY_no_guarda_contrasenas_asi_que_la_credencial_va_siempre_en_null(
        string? publicKeyFile)
    {
        var conexion = Conexion(publicKeyFile: publicKeyFile);

        Assert.Null(conexion.Credencial);
        Assert.False(conexion.TieneContrasena);
    }

    [Fact]
    public void El_origen_declarado_es_PuTTY() =>
        Assert.Equal(OrigenDeImportacion.Putty, Conexion().Origen);

    [Fact]
    public void Convertir_un_nombre_nulo_no_se_deja_pasar() =>
        Assert.Throws<ArgumentNullException>(() => Convertir(null!));

    [Fact]
    public void Leer_el_registro_no_lista_la_plantilla_ni_deja_credenciales()
    {
        using var lectura = LectorDePutty.LeerRegistro();

        foreach (var ajena in new[] { "Default Settings", "WinSCP temporary session" })
        {
            Assert.DoesNotContain(
                lectura.Compatibles,
                c => string.Equals(c.Nombre, ajena, StringComparison.Ordinal));
            Assert.DoesNotContain(
                lectura.Omitidas,
                o => string.Equals(o.Nombre, ajena, StringComparison.Ordinal));
        }

        Assert.Equal(0, lectura.ConContrasena);
        Assert.All(lectura.Compatibles, c => Assert.Empty(c.Carpetas));
    }
}
