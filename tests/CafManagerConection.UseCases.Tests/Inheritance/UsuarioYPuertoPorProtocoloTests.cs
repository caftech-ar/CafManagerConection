using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Tests.Inheritance;

/// <summary>El usuario y el puerto de carpeta se heredan por protocolo, con el compartido de reserva.</summary>
public class UsuarioYPuertoPorProtocoloTests
{
    private static Connection Conexion(Guid carpeta, Protocol protocol) =>
        new(Guid.NewGuid(), "Servidor", protocol, "192.0.2.1") { FolderId = carpeta };

    [Fact]
    public void Rdp_y_ssh_en_la_misma_carpeta_heredan_usuarios_distintos()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { RdpUserName = "admin", SshUserName = "root" },
        };
        var resolver = new SettingsResolver([carpeta]);

        Assert.Equal("admin", resolver.Resolve(Conexion(carpeta.Id, Protocol.Rdp)).UserName.Value);
        Assert.Equal("root", resolver.Resolve(Conexion(carpeta.Id, Protocol.Ssh)).UserName.Value);
    }

    [Fact]
    public void El_puerto_es_por_protocolo()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { SshPort = 2222, RdpPort = 3390 },
        };
        var resolver = new SettingsResolver([carpeta]);

        Assert.Equal(2222, resolver.Resolve(Conexion(carpeta.Id, Protocol.Ssh)).ResolvedPort);
        Assert.Equal(3390, resolver.Resolve(Conexion(carpeta.Id, Protocol.Rdp)).ResolvedPort);
    }

    [Fact]
    public void El_compartido_queda_de_reserva_cuando_el_protocolo_no_lo_define()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { UserName = "operador", SshUserName = "root" },
        };
        var resolver = new SettingsResolver([carpeta]);

        // SSH tiene el suyo; RDP no, así que cae en el compartido.
        Assert.Equal("root", resolver.Resolve(Conexion(carpeta.Id, Protocol.Ssh)).UserName.Value);
        Assert.Equal("operador", resolver.Resolve(Conexion(carpeta.Id, Protocol.Rdp)).UserName.Value);
    }

    [Fact]
    public void El_valor_por_protocolo_sube_por_la_cadena()
    {
        var abuela = new Folder(Guid.NewGuid(), "Raíz") { Settings = { SshPort = 2222 } };
        var madre = new Folder(Guid.NewGuid(), "Hija", abuela.Id);
        var resolver = new SettingsResolver([abuela, madre]);

        Assert.Equal(2222, resolver.Resolve(Conexion(madre.Id, Protocol.Ssh)).ResolvedPort);
    }
}
