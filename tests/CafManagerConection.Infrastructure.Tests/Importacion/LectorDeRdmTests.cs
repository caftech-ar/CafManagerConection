using CafManagerConection.Domain.Importacion;
using CafManagerConection.Infrastructure.Importacion;
using Xunit;

namespace CafManagerConection.Infrastructure.Tests.Importacion;

public sealed class LectorDeRdmTests
{
    private const string Export = """
        <ArrayOfConnection>
          <Connection>
            <ConnectionType>Group</ConnectionType>
            <Name>Produccion</Name>
            <Group>Produccion</Group>
          </Connection>
          <Connection>
            <ConnectionType>SSHShell</ConnectionType>
            <Name>web-01</Name>
            <Group>Produccion\Linux</Group>
            <Terminal>
              <Host>web-01.example.com</Host>
              <HostPort>2222</HostPort>
              <Username>admin</Username>
            </Terminal>
          </Connection>
          <Connection>
            <ConnectionType>Putty</ConnectionType>
            <Name>legado</Name>
            <Group></Group>
            <Putty>
              <Host>operador@legado.example.com</Host>
              <Port>22</Port>
            </Putty>
          </Connection>
          <Connection>
            <ConnectionType>RDPConfigured</ConnectionType>
            <Name>escritorio</Name>
            <Group>Produccion</Group>
            <Url>escritorio.example.com</Url>
            <RDP>
              <UserName>soporte</UserName>
              <Domain>EJEMPLO</Domain>
            </RDP>
          </Connection>
          <Connection>
            <ConnectionType>WebBrowser</ConnectionType>
            <Name>panel</Name>
            <Group></Group>
            <WebBrowserUrl>https://panel.example.com/admin</WebBrowserUrl>
          </Connection>
          <Connection>
            <ConnectionType>Ftp</ConnectionType>
            <Name>archivos</Name>
          </Connection>
          <Connection>
            <ConnectionType>SSHShell</ConnectionType>
            <Name>sin host</Name>
            <Terminal><Username>admin</Username></Terminal>
          </Connection>
        </ArrayOfConnection>
        """;

    private static ConexionImportada Una(string nombre) =>
        LectorDeRdm.Leer(Export).Compatibles.Single(c => c.Nombre == nombre);

    [Fact]
    public void Trae_las_cuatro_conexiones_convertibles()
    {
        var lectura = LectorDeRdm.Leer(Export);

        Assert.Equal(
            ["web-01", "legado", "escritorio", "panel"],
            lectura.Compatibles.Select(c => c.Nombre));
    }

    [Fact]
    public void Un_grupo_no_es_una_conexion()
    {
        var lectura = LectorDeRdm.Leer(Export);

        Assert.DoesNotContain(lectura.Compatibles, c => c.Nombre == "Produccion");
        Assert.DoesNotContain(lectura.Omitidas, o => o.Nombre == "Produccion");
    }

    [Fact]
    public void Una_sesion_ssh_trae_host_puerto_y_usuario()
    {
        var web = Una("web-01");

        Assert.Equal(ProtocoloImportado.Ssh, web.Protocolo);
        Assert.Equal("web-01.example.com", web.Host);
        Assert.Equal(2222, web.Puerto);
        Assert.Equal("admin", web.Usuario);
        Assert.Equal(["Produccion", "Linux"], web.Carpetas);
    }

    [Fact]
    public void El_usuario_embebido_de_putty_se_separa_del_host()
    {
        var legado = Una("legado");

        Assert.Equal("legado.example.com", legado.Host);
        Assert.Equal("operador", legado.Usuario);
    }

    [Fact]
    public void Una_conexion_rdp_toma_el_host_de_Url_y_trae_el_dominio()
    {
        var escritorio = Una("escritorio");

        Assert.Equal(ProtocoloImportado.Rdp, escritorio.Protocolo);
        Assert.Equal("escritorio.example.com", escritorio.Host);
        Assert.Equal("soporte", escritorio.Usuario);
        Assert.Equal("EJEMPLO", escritorio.Dominio);
    }

    [Fact]
    public void Una_entrada_web_conserva_la_url_entera_y_deduce_el_host()
    {
        var panel = Una("panel");

        Assert.Equal(ProtocoloImportado.Web, panel.Protocolo);
        Assert.Equal("panel.example.com", panel.Host);
        Assert.Equal("https://panel.example.com/admin", panel.Url);
    }

    [Fact]
    public void Lo_que_no_se_convierte_se_informa_con_su_motivo()
    {
        var omitidas = LectorDeRdm.Leer(Export).Omitidas;

        Assert.Contains(omitidas, o => o.Nombre == "archivos" && o.Motivo.Contains("FTP", StringComparison.Ordinal));
        Assert.Contains(omitidas, o => o.Nombre == "sin host" && o.Motivo.Contains("host", StringComparison.Ordinal));
        Assert.All(omitidas, o => Assert.NotEmpty(o.Motivo));
    }

    [Fact]
    public void Un_archivo_que_no_es_xml_se_informa_en_lugar_de_reventar()
    {
        var lectura = LectorDeRdm.Leer("esto no es xml");

        Assert.Empty(lectura.Compatibles);
        Assert.Single(lectura.Omitidas);
    }

    [Fact]
    public void Ninguna_conexion_trae_contrasena()
    {
        // RDM guarda las contrasenas cifradas con su propia clave: no se pueden traer.
        Assert.Equal(0, LectorDeRdm.Leer(Export).ConContrasena);
    }
}
