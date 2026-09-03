using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Tests.Inheritance;

// FR-064a, SC-013: muchas conexiones comparten credencial, y cargarla en la carpeta las cubre todas.
public sealed class HerenciaDeCredencialTests
{
    private static Folder Carpeta(Guid id, string? clave, Guid? padre = null) =>
        new(id, "Carpeta", padre)
        {
            Settings = new FolderSettings { SshCredentialKey = clave },
        };

    private static Connection Ssh(Guid? carpeta) =>
        new(Guid.NewGuid(), "Servidor", Protocol.Ssh, "192.0.2.1") { FolderId = carpeta };

    [Fact]
    public void Veinte_conexiones_que_heredan_cambian_todas_al_cambiar_la_carpeta()
    {
        // SC-013.
        var id = Guid.NewGuid();
        var carpeta = Carpeta(id, "cmc:folder:vieja:ssh");
        var conexiones = Enumerable.Range(0, 20).Select(_ => Ssh(id)).ToList();

        var antes = new SettingsResolver([carpeta]);

        Assert.All(
            conexiones,
            c => Assert.Equal("cmc:folder:vieja:ssh", antes.Resolve(c).CredentialKey.Value));

        carpeta.Settings.SshCredentialKey = "cmc:folder:nueva:ssh";
        var despues = new SettingsResolver([carpeta]);

        Assert.All(
            conexiones,
            c => Assert.Equal("cmc:folder:nueva:ssh", despues.Resolve(c).CredentialKey.Value));
    }

    [Fact]
    public void La_credencial_propia_gana_sobre_la_de_la_carpeta()
    {
        var id = Guid.NewGuid();
        var c = Ssh(id);
        c.CredentialKey = "cmc:ssh:propia";

        var efectivo = new SettingsResolver([Carpeta(id, "cmc:folder:x:ssh")]).Resolve(c);

        Assert.Equal("cmc:ssh:propia", efectivo.CredentialKey.Value);
        Assert.False(efectivo.CredentialKey.IsInherited);
    }

    [Fact]
    public void La_herencia_sube_hasta_encontrar_quien_la_defina()
    {
        var abuela = Guid.NewGuid();
        var madre = Guid.NewGuid();

        var resolver = new SettingsResolver([
            Carpeta(abuela, "cmc:folder:abuela:ssh"),
            Carpeta(madre, clave: null, padre: abuela),
        ]);

        var efectivo = resolver.Resolve(Ssh(madre));

        Assert.Equal("cmc:folder:abuela:ssh", efectivo.CredentialKey.Value);
        Assert.True(efectivo.CredentialKey.IsInherited);
    }

    [Fact]
    public void Gana_la_carpeta_mas_cercana_que_la_defina()
    {
        var abuela = Guid.NewGuid();
        var madre = Guid.NewGuid();

        var resolver = new SettingsResolver([
            Carpeta(abuela, "cmc:folder:abuela:ssh"),
            Carpeta(madre, "cmc:folder:madre:ssh", padre: abuela),
        ]);

        Assert.Equal("cmc:folder:madre:ssh", resolver.Resolve(Ssh(madre)).CredentialKey.Value);
    }

    [Fact]
    public void Sin_nadie_que_la_defina_no_hay_credencial()
    {
        var efectivo = new SettingsResolver([]).Resolve(Ssh(null));

        Assert.False(efectivo.CredentialKey.IsDefined);
    }

    [Fact]
    public void Una_carpeta_define_una_credencial_por_protocolo()
    {
        // FR-064a: la credencial SSH no puede servir para una sesión RDP.
        var id = Guid.NewGuid();

        var carpeta = new Folder(id, "Mixta")
        {
            Settings = new FolderSettings
            {
                SshCredentialKey = "cmc:folder:x:ssh",
                RdpCredentialKey = "cmc:folder:x:rdp",
            },
        };

        var resolver = new SettingsResolver([carpeta]);

        var rdp = new Connection(Guid.NewGuid(), "Pivote", Protocol.Rdp, "192.0.2.5")
        {
            FolderId = id,
        };

        Assert.Equal("cmc:folder:x:ssh", resolver.Resolve(Ssh(id)).CredentialKey.Value);
        Assert.Equal("cmc:folder:x:rdp", resolver.Resolve(rdp).CredentialKey.Value);
    }
}
