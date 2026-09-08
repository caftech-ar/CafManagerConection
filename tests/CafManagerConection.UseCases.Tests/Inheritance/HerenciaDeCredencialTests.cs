using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Tests.Inheritance;

// Muchas conexiones heredan la contraseña de la carpeta, y cargarla ahí las cubre todas.
public sealed class HerenciaDeCredencialTests
{
    private static Folder Carpeta(Guid id, bool conSecreto, Guid? padre = null) =>
        new(id, "Carpeta", padre)
        {
            Settings = new FolderSettings { SshTieneSecreto = conSecreto },
        };

    private static Connection Ssh(Guid? carpeta) =>
        new(Guid.NewGuid(), "Servidor", Protocol.Ssh, "192.0.2.1") { FolderId = carpeta };

    [Fact]
    public void Veinte_conexiones_que_heredan_apuntan_todas_a_la_carpeta()
    {
        var id = Guid.NewGuid();
        var carpeta = Carpeta(id, conSecreto: true);
        var conexiones = Enumerable.Range(0, 20).Select(_ => Ssh(id)).ToList();

        var resolver = new SettingsResolver([carpeta]);
        var esperada = ReferenciaDeSecreto.DeCarpeta(id, Protocol.Ssh);

        Assert.All(conexiones, c => Assert.Equal(esperada, resolver.Resolve(c).Secreto.Value));
    }

    [Fact]
    public void Al_quitar_la_contrasena_de_la_carpeta_ninguna_hereda_nada()
    {
        var id = Guid.NewGuid();
        var carpeta = Carpeta(id, conSecreto: true);
        var conexion = Ssh(id);

        Assert.True(new SettingsResolver([carpeta]).Resolve(conexion).Secreto.IsDefined);

        carpeta.Settings.SshTieneSecreto = false;

        Assert.False(new SettingsResolver([carpeta]).Resolve(conexion).Secreto.IsDefined);
    }

    [Fact]
    public void La_contrasena_propia_gana_sobre_la_de_la_carpeta()
    {
        var id = Guid.NewGuid();
        var c = Ssh(id);
        c.TieneSecreto = true;

        var efectivo = new SettingsResolver([Carpeta(id, conSecreto: true)]).Resolve(c);

        Assert.Equal(ReferenciaDeSecreto.DeConexion(c.Id, Protocol.Ssh), efectivo.Secreto.Value);
        Assert.False(efectivo.Secreto.IsInherited);
    }

    [Fact]
    public void La_herencia_sube_hasta_encontrar_quien_la_defina()
    {
        var abuela = Guid.NewGuid();
        var madre = Guid.NewGuid();

        var resolver = new SettingsResolver([
            Carpeta(abuela, conSecreto: true),
            Carpeta(madre, conSecreto: false, padre: abuela),
        ]);

        var efectivo = resolver.Resolve(Ssh(madre));

        Assert.Equal(ReferenciaDeSecreto.DeCarpeta(abuela, Protocol.Ssh), efectivo.Secreto.Value);
        Assert.True(efectivo.Secreto.IsInherited);
    }

    [Fact]
    public void Gana_la_carpeta_mas_cercana_que_la_defina()
    {
        var abuela = Guid.NewGuid();
        var madre = Guid.NewGuid();

        var resolver = new SettingsResolver([
            Carpeta(abuela, conSecreto: true),
            Carpeta(madre, conSecreto: true, padre: abuela),
        ]);

        Assert.Equal(
            ReferenciaDeSecreto.DeCarpeta(madre, Protocol.Ssh),
            resolver.Resolve(Ssh(madre)).Secreto.Value);
    }

    [Fact]
    public void Sin_nadie_que_la_defina_no_hay_contrasena()
    {
        var efectivo = new SettingsResolver([]).Resolve(Ssh(null));

        Assert.False(efectivo.Secreto.IsDefined);
    }

    [Fact]
    public void Una_carpeta_define_una_contrasena_por_protocolo()
    {
        // La contraseña SSH no puede servir para una sesión RDP.
        var id = Guid.NewGuid();

        var carpeta = new Folder(id, "Mixta")
        {
            Settings = new FolderSettings { SshTieneSecreto = true, RdpTieneSecreto = true },
        };

        var resolver = new SettingsResolver([carpeta]);

        var rdp = new Connection(Guid.NewGuid(), "Pivote", Protocol.Rdp, "192.0.2.5")
        {
            FolderId = id,
        };

        Assert.Equal(
            ReferenciaDeSecreto.DeCarpeta(id, Protocol.Ssh),
            resolver.Resolve(Ssh(id)).Secreto.Value);

        Assert.Equal(
            ReferenciaDeSecreto.DeCarpeta(id, Protocol.Rdp),
            resolver.Resolve(rdp).Secreto.Value);
    }

    [Fact]
    public void Una_carpeta_sin_la_del_protocolo_pedido_no_presta_la_del_otro()
    {
        var id = Guid.NewGuid();

        var carpeta = new Folder(id, "Sólo SSH")
        {
            Settings = new FolderSettings { SshTieneSecreto = true },
        };

        var rdp = new Connection(Guid.NewGuid(), "Pivote", Protocol.Rdp, "192.0.2.5")
        {
            FolderId = id,
        };

        Assert.False(new SettingsResolver([carpeta]).Resolve(rdp).Secreto.IsDefined);
    }
}
