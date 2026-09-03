using CafManagerConection.Domain.Connections;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Tests.Database;

public class ConnectionRepositoryTests
{
    private static async Task<(TempDatabase Db, ConnectionRepository Repo)> CreateAsync()
    {
        var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();
        return (db, new ConnectionRepository(db.Factory));
    }

    [Fact]
    public async Task Guarda_una_conexion_ssh_con_su_configuracion()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "Linux Web", Protocol.Ssh, "192.0.2.1")
        {
            UserName = "root",
        };
        c.SetPort(2222);
        var ssh = new SshSettings
        {
            ConnectionId = c.Id,
            AuthMethod = SshAuthMethod.PrivateKey,
            PrivateKeyPath = @"C:\claves\id_ed25519",
            KnownHostFingerprint = "SHA256:abc123",
            KeepAliveSeconds = 90,
        };

        await repo.AddAsync(new ConnectionRecord(c, Ssh: ssh));
        var r = await repo.GetByIdAsync(c.Id);

        Assert.NotNull(r);
        Assert.Equal("Linux Web", r.Connection.Name);
        Assert.Equal(2222, r.Connection.Port);
        Assert.Equal(SshAuthMethod.PrivateKey, r.Ssh!.AuthMethod);
        Assert.Equal("SHA256:abc123", r.Ssh.KnownHostFingerprint);
        Assert.Equal(90, r.Ssh.KeepAliveSeconds);
    }

    /// <remarks><c>ReadSsh</c> usa <c>SELECT *</c>; el riesgo es olvidar la columna en el <c>INSERT</c>.</remarks>
    [Fact]
    public async Task Guarda_y_recupera_el_certificado_de_una_conexion_ssh()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "Linux Web", Protocol.Ssh, "192.0.2.1");
        var ssh = new SshSettings
        {
            ConnectionId = c.Id,
            AuthMethod = SshAuthMethod.PrivateKey,
            PrivateKeyPath = @"C:\claves\id_ed25519",
            CertificatePath = @"C:\claves\id_ed25519-cert.pub",
        };

        await repo.AddAsync(new ConnectionRecord(c, Ssh: ssh));
        var r = await repo.GetByIdAsync(c.Id);

        Assert.Equal(@"C:\claves\id_ed25519-cert.pub", r!.Ssh!.CertificatePath);
    }

    /// <remarks><c>UpdateAsync</c> borra la fila de <c>ssh_settings</c> y la reescribe entera desde <c>record.Ssh</c>.</remarks>
    [Fact]
    public async Task Editar_una_conexion_sin_tocar_el_certificado_no_lo_borra()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "Linux Web", Protocol.Ssh, "192.0.2.1");
        var ssh = new SshSettings
        {
            ConnectionId = c.Id,
            AuthMethod = SshAuthMethod.PrivateKey,
            PrivateKeyPath = @"C:\claves\id_ed25519",
            CertificatePath = @"C:\claves\id_ed25519-cert.pub",
        };
        await repo.AddAsync(new ConnectionRecord(c, Ssh: ssh));

        var leida = await repo.GetByIdAsync(c.Id);
        Assert.NotNull(leida);
        leida.Connection.Rename("Linux Web (renombrado)");
        await repo.UpdateAsync(leida);

        var despues = await repo.GetByIdAsync(c.Id);

        Assert.NotNull(despues);
        Assert.Equal("Linux Web (renombrado)", despues.Connection.Name);
        Assert.Equal(@"C:\claves\id_ed25519-cert.pub", despues.Ssh!.CertificatePath);
    }

    [Fact]
    public async Task Guarda_una_conexion_web()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "Panel", Protocol.Web, "panel.local");
        var web = new WebSettings
        {
            ConnectionId = c.Id,
            Url = "https://panel.local/admin",
            Browser = @"C:\Program Files\Firefox\firefox.exe",
            PrivateWindow = true,
        };

        await repo.AddAsync(new ConnectionRecord(c, Web: web));
        var r = await repo.GetByIdAsync(c.Id);

        Assert.Equal("https://panel.local/admin", r!.Web!.Url);
        Assert.True(r.Web.PrivateWindow);
        Assert.Contains("firefox", r.Web.Browser!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_navegador_nulo_significa_el_predeterminado_del_sistema()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "Panel", Protocol.Web, "panel.local");

        await repo.AddAsync(new ConnectionRecord(
            c, Web: new WebSettings { ConnectionId = c.Id, Url = "https://x" }));
        var r = await repo.GetByIdAsync(c.Id);

        Assert.Null(r!.Web!.Browser);
        Assert.False(r.Web.PrivateWindow);
    }

    [Fact]
    public async Task Los_campos_heredables_se_guardan_como_nulos()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "Servidor", Protocol.Ssh, "h");

        await repo.AddAsync(new ConnectionRecord(
            c, Ssh: new SshSettings { ConnectionId = c.Id }));
        var r = await repo.GetByIdAsync(c.Id);

        Assert.Null(r!.Connection.Port);
        Assert.Null(r.Connection.UserName);
        Assert.Null(r.Connection.CredentialKey);
        Assert.Null(r.Ssh!.AuthMethod);
        Assert.Null(r.Ssh.KeepAliveSeconds);
    }

    [Fact]
    public async Task La_credencial_guardada_es_solo_una_referencia()
    {
        // Principio II: en la base solo puede haber la clave, jamas el secreto.
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "S", Protocol.Ssh, "h")
        {
            CredentialKey = "cmc:ssh:11111111-1111-1111-1111-111111111111",
        };
        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));

        var contenido = await File.ReadAllBytesAsync(db.Paths.DatabasePath);
        var texto = System.Text.Encoding.UTF8.GetString(contenido);

        Assert.Contains("cmc:ssh:", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("contraseña", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_reemplaza_la_configuracion_especifica()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "S", Protocol.Ssh, "h");
        await repo.AddAsync(new ConnectionRecord(
            c, Ssh: new SshSettings { ConnectionId = c.Id, KeepAliveSeconds = 60 }));

        await repo.UpdateAsync(new ConnectionRecord(
            c, Ssh: new SshSettings { ConnectionId = c.Id, KeepAliveSeconds = 0 }));

        var r = await repo.GetByIdAsync(c.Id);
        Assert.Equal(0, r!.Ssh!.KeepAliveSeconds);
    }

    [Fact]
    public async Task Borrar_una_conexion_arrastra_su_configuracion()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "S", Protocol.Ssh, "h");
        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));

        await repo.DeleteAsync(c.Id);

        Assert.Null(await repo.GetByIdAsync(c.Id));

        using var conn = db.Factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ssh_settings;";
        Assert.Equal(0L, Convert.ToInt64(cmd.ExecuteScalar()));
    }

    [Fact]
    public async Task Reorder_asigna_el_orden_pedido()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var a = new Connection(Guid.NewGuid(), "A", Protocol.Ssh, "h");
        var b = new Connection(Guid.NewGuid(), "B", Protocol.Ssh, "h");
        await repo.AddAsync(new ConnectionRecord(a, Ssh: new SshSettings { ConnectionId = a.Id }));
        await repo.AddAsync(new ConnectionRecord(b, Ssh: new SshSettings { ConnectionId = b.Id }));

        await repo.ReorderAsync(null, [b.Id, a.Id]);

        var todas = await repo.GetAllAsync();
        Assert.Equal("B", todas[0].Name);
        Assert.Equal("A", todas[1].Name);
    }

    [Fact]
    public async Task SetLastConnected_registra_la_fecha()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var c = new Connection(Guid.NewGuid(), "S", Protocol.Ssh, "h");
        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));
        var cuando = new DateTimeOffset(2026, 8, 24, 10, 30, 0, TimeSpan.Zero);

        await repo.SetLastConnectedAsync(c.Id, cuando);

        var r = await repo.GetByIdAsync(c.Id);
        Assert.Equal(cuando, r!.Connection.LastConnectedAt);
    }

    [Fact]
    public async Task Borrar_la_carpeta_arrastra_sus_conexiones()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var folderRepo = new FolderRepository(db.Factory);
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        await folderRepo.AddAsync(carpeta);

        var c = new Connection(Guid.NewGuid(), "S", Protocol.Ssh, "h") { FolderId = carpeta.Id };
        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));

        await folderRepo.DeleteAsync(carpeta.Id);

        Assert.Null(await repo.GetByIdAsync(c.Id));
    }
}
