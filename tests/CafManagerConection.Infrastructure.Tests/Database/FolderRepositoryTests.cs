using CafManagerConection.Domain.Connections;
using CafManagerConection.Infrastructure.Database;

namespace CafManagerConection.Infrastructure.Tests.Database;

public class FolderRepositoryTests
{
    private static async Task<(TempDatabase Db, FolderRepository Repo)> CreateAsync()
    {
        var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();
        return (db, new FolderRepository(db.Factory));
    }

    [Fact]
    public async Task Guarda_y_recupera_una_carpeta()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var folder = new Folder(Guid.NewGuid(), "Producción");

        await repo.AddAsync(folder);
        var recuperada = await repo.GetByIdAsync(folder.Id);

        Assert.NotNull(recuperada);
        Assert.Equal("Producción", recuperada.Name);
        Assert.Null(recuperada.ParentId);
    }

    [Fact]
    public async Task Persiste_la_configuracion_heredable()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var folder = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings =
            {
                UserName = "root",
                Port = 2222,
                SshCredentialKey = "cmc:folder:x:ssh",
                RdpCredentialKey = "cmc:folder:x:rdp",
                SshAuthMethod = SshAuthMethod.PrivateKey,
                RdpClipboardEnabled = false,
                SshKeepAliveSeconds = 120,
            },
        };

        await repo.AddAsync(folder);
        var r = await repo.GetByIdAsync(folder.Id);

        Assert.NotNull(r);
        Assert.Equal("root", r.Settings.UserName);
        Assert.Equal(2222, r.Settings.Port);
        Assert.Equal("cmc:folder:x:ssh", r.Settings.SshCredentialKey);
        Assert.Equal("cmc:folder:x:rdp", r.Settings.RdpCredentialKey);
        Assert.Equal(SshAuthMethod.PrivateKey, r.Settings.SshAuthMethod);
        Assert.False(r.Settings.RdpClipboardEnabled);
        Assert.Equal(120, r.Settings.SshKeepAliveSeconds);
    }

    /// <remarks>El certificado y la clave privada conviven en la misma fila de <c>folder_settings</c>.</remarks>
    [Fact]
    public async Task Guarda_y_recupera_el_certificado_ssh_heredado()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var folder = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings =
            {
                SshAuthMethod = SshAuthMethod.PrivateKey,
                SshPrivateKeyPath = @"C:\claves\id_ed25519",
                SshCertificatePath = @"C:\claves\id_ed25519-cert.pub",
            },
        };

        await repo.AddAsync(folder);
        var r = await repo.GetByIdAsync(folder.Id);

        Assert.Equal(@"C:\claves\id_ed25519", r!.Settings.SshPrivateKeyPath);
        Assert.Equal(@"C:\claves\id_ed25519-cert.pub", r.Settings.SshCertificatePath);
    }

    /// <remarks>Una columna que falte en el <c>SELECT</c> explícito de <see cref="FolderRepository.GetAllAsync"/> se pierde al reescribir la fila.</remarks>
    [Fact]
    public async Task Renombrar_una_carpeta_no_le_borra_el_certificado_ssh()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings =
            {
                SshPrivateKeyPath = @"C:\claves\id_ed25519",
                SshCertificatePath = @"C:\claves\id_ed25519-cert.pub",
            },
        };
        await repo.AddAsync(carpeta);

        var leida = await repo.GetByIdAsync(carpeta.Id);
        Assert.NotNull(leida);
        leida.Rename("Producción CABA");
        await repo.UpdateAsync(leida);

        var despues = await repo.GetByIdAsync(carpeta.Id);

        Assert.NotNull(despues);
        Assert.Equal("Producción CABA", despues.Name);
        Assert.Equal(@"C:\claves\id_ed25519", despues.Settings.SshPrivateKeyPath);
        Assert.Equal(@"C:\claves\id_ed25519-cert.pub", despues.Settings.SshCertificatePath);
    }

    [Fact]
    public async Task Un_booleano_en_false_se_distingue_de_no_definido()
    {
        // El caso que rompe un mapeo descuidado de bool? a INTEGER.
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var folder = new Folder(Guid.NewGuid(), "F")
        {
            Settings = { RdpClipboardEnabled = false, RdpFitToTab = null },
        };

        await repo.AddAsync(folder);
        var r = await repo.GetByIdAsync(folder.Id);

        Assert.False(r!.Settings.RdpClipboardEnabled);
        Assert.Null(r.Settings.RdpFitToTab);
    }

    [Fact]
    public async Task Una_carpeta_sin_configuracion_no_escribe_fila_de_settings()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var folder = new Folder(Guid.NewGuid(), "Simple");

        await repo.AddAsync(folder);
        var r = await repo.GetByIdAsync(folder.Id);

        Assert.True(r!.Settings.IsEmpty);
    }

    [Fact]
    public async Task Update_reemplaza_la_configuracion_heredable()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var folder = new Folder(Guid.NewGuid(), "F") { Settings = { UserName = "antes" } };
        await repo.AddAsync(folder);

        var actualizada = new Folder(folder.Id, "F") { Settings = { UserName = "después" } };
        await repo.UpdateAsync(actualizada);

        var r = await repo.GetByIdAsync(folder.Id);
        Assert.Equal("después", r!.Settings.UserName);
    }

    [Fact]
    public async Task Borrar_una_carpeta_arrastra_sus_subcarpetas()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var raiz = new Folder(Guid.NewGuid(), "Producción");
        var hija = new Folder(Guid.NewGuid(), "DMZ", raiz.Id);
        var nieta = new Folder(Guid.NewGuid(), "Web", hija.Id);
        await repo.AddAsync(raiz);
        await repo.AddAsync(hija);
        await repo.AddAsync(nieta);

        var result = await repo.DeleteAsync(raiz.Id);

        Assert.Equal(3, result.DeletedFolderIds.Count);
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task Borrar_informa_las_conexiones_eliminadas_para_borrar_sus_credenciales()
    {
        // SQLite no puede alcanzar el Credential Manager: el repositorio devuelve los ids
        // para que la capa de aplicacion se ocupe.
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        await repo.AddAsync(carpeta);

        var conexionId = Guid.NewGuid();
        using (var conn = db.Factory.Create())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO connections (id, folder_id, name, protocol, host, created_at, updated_at)
                VALUES (@id, @folder, 'S', 'Ssh', 'h', '2026-08-24', '2026-08-24');
                """;
            cmd.Parameters.AddWithValue("@id", conexionId.ToString("D"));
            cmd.Parameters.AddWithValue("@folder", carpeta.Id.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        var result = await repo.DeleteAsync(carpeta.Id);

        Assert.Contains(conexionId, result.DeletedConnectionIds);
    }

    [Fact]
    public async Task GetAll_devuelve_las_carpetas_ordenadas()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        await repo.AddAsync(new Folder(Guid.NewGuid(), "Segunda", null, 2));
        await repo.AddAsync(new Folder(Guid.NewGuid(), "Primera", null, 1));

        var todas = await repo.GetAllAsync();

        Assert.Equal("Primera", todas[0].Name);
        Assert.Equal("Segunda", todas[1].Name);
    }

    /// <remarks>El color, la descripción y la etiqueta faltaban en la lista de columnas del SELECT.</remarks>
    [Fact]
    public async Task Guarda_y_recupera_la_clave_de_color_la_descripcion_y_la_etiqueta()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var etiqueta = (await new TagRepository(db.Factory).GetAllAsync())
            .First(e => e.Codigo == "PRD");

        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            ClaveDeColor = "rojo",
            Description = "Servidores de producción",
        };
        carpeta.Settings.TagId = etiqueta.Id;

        await repo.AddAsync(carpeta);
        var recuperada = await repo.GetByIdAsync(carpeta.Id);

        Assert.NotNull(recuperada);
        Assert.Equal("rojo", recuperada.ClaveDeColor);
        Assert.Equal("Servidores de producción", recuperada.Description);
        Assert.Equal(etiqueta.Id, recuperada.Settings.TagId);
    }

    /// <remarks>Con una columna afuera del SELECT, cualquier cambio la devolvía a la base como <c>null</c>.</remarks>
    [Fact]
    public async Task Renombrar_una_carpeta_no_le_borra_la_clave_de_color_la_descripcion_ni_la_etiqueta()
    {
        var (db, repo) = await CreateAsync();
        using var _ = db;
        var etiqueta = (await new TagRepository(db.Factory).GetAllAsync())
            .First(e => e.Codigo == "PRD");

        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            ClaveDeColor = "rojo",
            Description = "Servidores de producción",
        };
        carpeta.Settings.TagId = etiqueta.Id;
        await repo.AddAsync(carpeta);

        var leida = await repo.GetByIdAsync(carpeta.Id);
        Assert.NotNull(leida);
        leida.Rename("Producción CABA");
        await repo.UpdateAsync(leida);

        var despues = await repo.GetByIdAsync(carpeta.Id);

        Assert.NotNull(despues);
        Assert.Equal("Producción CABA", despues.Name);
        Assert.Equal("rojo", despues.ClaveDeColor);
        Assert.Equal("Servidores de producción", despues.Description);
        Assert.Equal(etiqueta.Id, despues.Settings.TagId);
    }
}
