using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Tests.Inheritance;

// FR-058 a FR-064, SC-013.
public class SettingsResolverTests
{
    private static Connection Conexion(Guid? folderId = null, Protocol protocol = Protocol.Ssh)
        => new(Guid.NewGuid(), "Servidor", protocol, "192.0.2.1") { FolderId = folderId };

    [Fact]
    public void Sin_carpeta_y_sin_valor_propio_el_campo_queda_sin_definir()
    {
        var resolver = new SettingsResolver([]);

        var efectivo = resolver.Resolve(Conexion());

        Assert.False(efectivo.Port.IsDefined);
        Assert.Equal(ValueSource.Undefined, efectivo.UserName.Source);
    }

    [Fact]
    public void Sin_puerto_definido_se_usa_el_predeterminado_del_protocolo()
    {
        var resolver = new SettingsResolver([]);

        Assert.Equal(22, resolver.Resolve(Conexion(protocol: Protocol.Ssh)).ResolvedPort);
        Assert.Equal(3389, resolver.Resolve(Conexion(protocol: Protocol.Rdp)).ResolvedPort);
    }

    [Fact]
    public void El_valor_propio_gana_sobre_el_heredado()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { Port = 2222, UserName = "root" },
        };
        var conexion = Conexion(carpeta.Id);
        conexion.SetPort(2022);

        var efectivo = new SettingsResolver([carpeta]).Resolve(conexion);

        Assert.Equal(2022, efectivo.ResolvedPort);
        Assert.Equal(ValueSource.Own, efectivo.Port.Source);
    }

    [Fact]
    public void Un_campo_sin_valor_propio_se_hereda_de_la_carpeta()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { Port = 2222, UserName = "root" },
        };

        var efectivo = new SettingsResolver([carpeta]).Resolve(Conexion(carpeta.Id));

        Assert.Equal(2222, efectivo.ResolvedPort);
        Assert.Equal("root", efectivo.UserName.Value);
        Assert.True(efectivo.UserName.IsInherited);
        Assert.Equal(carpeta.Id, efectivo.UserName.SourceFolderId);
    }

    [Fact]
    public void La_herencia_sube_por_toda_la_cadena_hasta_la_raiz()
    {
        var raiz = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { UserName = "root", Port = 2222 },
        };
        var media = new Folder(Guid.NewGuid(), "DMZ", raiz.Id);
        var hoja = new Folder(Guid.NewGuid(), "Web", media.Id);

        var efectivo = new SettingsResolver([raiz, media, hoja]).Resolve(Conexion(hoja.Id));

        Assert.Equal("root", efectivo.UserName.Value);
        Assert.Equal(raiz.Id, efectivo.UserName.SourceFolderId);
    }

    [Fact]
    public void La_carpeta_mas_cercana_gana_sobre_la_mas_lejana()
    {
        var raiz = new Folder(Guid.NewGuid(), "Producción") { Settings = { Port = 22 } };
        var hoja = new Folder(Guid.NewGuid(), "DMZ", raiz.Id) { Settings = { Port = 2222 } };

        var efectivo = new SettingsResolver([raiz, hoja]).Resolve(Conexion(hoja.Id));

        Assert.Equal(2222, efectivo.ResolvedPort);
        Assert.Equal(hoja.Id, efectivo.Port.SourceFolderId);
    }

    [Fact]
    public void El_certificado_ssh_se_hereda_igual_que_la_clave_privada()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings =
            {
                SshPrivateKeyPath = @"C:\claves\id_ed25519",
                SshCertificatePath = @"C:\claves\id_ed25519-cert.pub",
            },
        };

        var efectivo = new SettingsResolver([carpeta]).Resolve(Conexion(carpeta.Id));

        Assert.Equal(@"C:\claves\id_ed25519-cert.pub", efectivo.CertificatePath.Value);
        Assert.True(efectivo.CertificatePath.IsInherited);
        Assert.Equal(carpeta.Id, efectivo.CertificatePath.SourceFolderId);
    }

    [Fact]
    public void El_certificado_ssh_puede_venir_de_una_carpeta_distinta_a_la_de_la_clave()
    {
        var raiz = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { SshPrivateKeyPath = @"C:\claves\id_ed25519" },
        };
        var hoja = new Folder(Guid.NewGuid(), "DMZ", raiz.Id)
        {
            Settings = { SshCertificatePath = @"C:\claves\id_ed25519-cert.pub" },
        };

        var efectivo = new SettingsResolver([raiz, hoja]).Resolve(Conexion(hoja.Id));

        Assert.Equal(@"C:\claves\id_ed25519", efectivo.PrivateKeyPath.Value);
        Assert.Equal(raiz.Id, efectivo.PrivateKeyPath.SourceFolderId);
        Assert.Equal(@"C:\claves\id_ed25519-cert.pub", efectivo.CertificatePath.Value);
        Assert.Equal(hoja.Id, efectivo.CertificatePath.SourceFolderId);
    }

    [Fact]
    public void Cada_protocolo_hereda_la_credencial_de_su_propio_tipo()
    {
        // FR-064a.
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings =
            {
                RdpCredentialKey = "cmc:folder:x:rdp",
                SshCredentialKey = "cmc:folder:x:ssh",
            },
        };
        var resolver = new SettingsResolver([carpeta]);

        var rdp = resolver.Resolve(Conexion(carpeta.Id, Protocol.Rdp));
        var ssh = resolver.Resolve(Conexion(carpeta.Id, Protocol.Ssh));

        Assert.Equal("cmc:folder:x:rdp", rdp.CredentialKey.Value);
        Assert.Equal("cmc:folder:x:ssh", ssh.CredentialKey.Value);
    }

    [Fact]
    public void Una_conexion_web_no_hereda_la_credencial_ssh_de_su_carpeta()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { SshCredentialKey = "cmc:folder:x:ssh" },
        };

        var efectivo = new SettingsResolver([carpeta])
            .Resolve(Conexion(carpeta.Id, Protocol.Web));

        Assert.False(efectivo.CredentialKey.IsDefined);
    }

    [Fact]
    public void Veinte_conexiones_heredan_la_credencial_de_su_carpeta()
    {
        // Es el escenario de SC-013, tal cual lo planteó el usuario.
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { SshCredentialKey = "cmc:folder:prod:ssh", UserName = "admin", Port = 22 },
        };
        var resolver = new SettingsResolver([carpeta]);
        var conexiones = Enumerable.Range(0, 20).Select(_ => Conexion(carpeta.Id)).ToArray();

        var efectivas = conexiones.Select(c => resolver.Resolve(c)).ToArray();

        Assert.All(efectivas, e =>
        {
            Assert.Equal("cmc:folder:prod:ssh", e.CredentialKey.Value);
            Assert.Equal("admin", e.UserName.Value);
            Assert.Equal(22, e.ResolvedPort);
        });
    }

    [Fact]
    public void Los_predeterminados_se_aplican_cuando_nadie_define_el_valor()
    {
        var efectivo = new SettingsResolver([]).Resolve(Conexion(protocol: Protocol.Rdp));

        Assert.True(efectivo.ResolvedClipboardEnabled);
        Assert.True(efectivo.ResolvedFitToTab);
        Assert.False(efectivo.ResolvedIgnoreCertificateWarnings);
        Assert.Equal(SshAuthMethod.Password, efectivo.ResolvedAuthMethod);
        Assert.Equal(60, efectivo.ResolvedKeepAliveSeconds);
    }

    [Fact]
    public void Un_valor_booleano_propio_en_false_no_se_confunde_con_heredar()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { RdpClipboardEnabled = true },
        };
        var conexion = Conexion(carpeta.Id, Protocol.Rdp);
        var rdp = new RdpSettings { ConnectionId = conexion.Id, ClipboardEnabled = false };

        var efectivo = new SettingsResolver([carpeta]).Resolve(conexion, rdp);

        Assert.False(efectivo.ResolvedClipboardEnabled);
        Assert.Equal(ValueSource.Own, efectivo.ClipboardEnabled.Source);
    }

    [Fact]
    public void Una_cadena_vacia_se_trata_como_no_definida()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción") { Settings = { UserName = "root" } };
        var conexion = Conexion(carpeta.Id);
        conexion.UserName = "";

        var efectivo = new SettingsResolver([carpeta]).Resolve(conexion);

        Assert.Equal("root", efectivo.UserName.Value);
    }

    [Fact]
    public void Un_ciclo_de_carpetas_no_cuelga_la_resolucion()
    {
        var a = new Folder(Guid.NewGuid(), "A");
        var b = new Folder(Guid.NewGuid(), "B", a.Id);
        a.MoveTo(b.Id);

        var ancestros = new SettingsResolver([a, b]).AncestryOf(a.Id);

        Assert.Equal(2, ancestros.Count);
    }

    [Fact]
    public void Mover_a_una_carpeta_con_otra_credencial_se_detecta_como_cambio()
    {
        var origen = new Folder(Guid.NewGuid(), "Desarrollo")
        {
            Settings = { SshCredentialKey = "cmc:folder:dev:ssh", UserName = "dev", Port = 22 },
        };
        var destino = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { SshCredentialKey = "cmc:folder:prod:ssh", UserName = "root", Port = 2222 },
        };
        var conexion = Conexion(origen.Id);
        var resolver = new SettingsResolver([origen, destino]);

        var cambios = resolver.DiffOnMove(conexion, destino.Id);

        Assert.Equal(3, cambios.Count);
        Assert.Contains(cambios, c => c.Contains("credencial", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(origen.Id, conexion.FolderId);
    }

    [Fact]
    public void Mover_entre_carpetas_equivalentes_no_reporta_cambios()
    {
        var origen = new Folder(Guid.NewGuid(), "A") { Settings = { UserName = "root" } };
        var destino = new Folder(Guid.NewGuid(), "B") { Settings = { UserName = "root" } };
        var conexion = Conexion(origen.Id);

        var cambios = new SettingsResolver([origen, destino]).DiffOnMove(conexion, destino.Id);

        Assert.Empty(cambios);
    }

    [Fact]
    public void Con_clave_privada_y_sin_metodo_elegido_se_autentica_por_clave()
    {
        var conexion = Conexion();
        var ssh = new SshSettings
        {
            ConnectionId = conexion.Id,
            PrivateKeyPath = @"C:\Users\alguien\.ssh\caftech",
        };

        var efectivo = new SettingsResolver([]).Resolve(conexion, null, ssh);

        Assert.Equal(SshAuthMethod.PrivateKey, efectivo.ResolvedAuthMethod);
    }

    [Fact]
    public void Sin_clave_privada_y_sin_metodo_elegido_se_autentica_por_contrasena()
    {
        var efectivo = new SettingsResolver([]).Resolve(Conexion());

        Assert.Equal(SshAuthMethod.Password, efectivo.ResolvedAuthMethod);
    }

    [Fact]
    public void La_clave_heredada_de_la_carpeta_tambien_decide_el_metodo()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        carpeta.Settings.SshPrivateKeyPath = @"C:\claves\comun";

        var efectivo = new SettingsResolver([carpeta])
            .Resolve(Conexion(folderId: carpeta.Id));

        Assert.Equal(SshAuthMethod.PrivateKey, efectivo.ResolvedAuthMethod);
    }

    [Fact]
    public void Un_metodo_elegido_a_mano_le_gana_a_la_deduccion()
    {
        var conexion = Conexion();
        var ssh = new SshSettings
        {
            ConnectionId = conexion.Id,
            PrivateKeyPath = @"C:\claves\una",
            AuthMethod = SshAuthMethod.Password,
        };

        var efectivo = new SettingsResolver([]).Resolve(conexion, null, ssh);

        Assert.Equal(SshAuthMethod.Password, efectivo.ResolvedAuthMethod);
    }
}
