using CafManagerConection.Domain.Connections;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <remarks>
/// Las columnas de <c>connections</c> se mapean a mano en la fila, los parámetros, el INSERT y el
/// UPDATE; Dapper resuelve por nombre en tiempo de ejecución, así que un nombre mal escrito no
/// rompe la compilación.
/// </remarks>
public sealed class CatalogoIdaYVueltaTests
{
    private static async Task<ConnectionRepository> RepositorioAsync(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();
        return new ConnectionRepository(db.Factory);
    }

    // Identificadores fijos que siembra Migration001_Esquema.cs, iguales en cualquier base.
    private static readonly Guid Produccion = Guid.Parse("11111111-0000-4000-8000-000000000001");
    private static readonly Guid Desarrollo = Guid.Parse("11111111-0000-4000-8000-000000000004");

    private static Connection Completa()
    {
        var c = new Connection(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.207")
        {
            Description = "Servidor de aplicaciones de Vialidad",
            TagId = Produccion,
            IsFavorite = true,
            ClaveDeColor = "azul",
            ClaveDeIcono = "puertos",
            Notes = "Se reinicia los domingos",
            UserName = "operador",
            SortOrder = 7,
        };

        c.SetPort(2022);
        c.SetCustomField("responsable", "Infraestructura");
        c.SetCustomField("rack", "B-12");

        return c;
    }

    [Fact]
    public async Task Todos_los_campos_de_catalogo_sobreviven_a_guardar_y_leer()
    {
        using var db = new TempDatabase();
        var repo = await RepositorioAsync(db);
        var original = Completa();

        await repo.AddAsync(new ConnectionRecord(original, Ssh: new SshSettings()));
        var leida = (await repo.GetByIdAsync(original.Id))!.Connection;

        Assert.Equal("Servidor de aplicaciones de Vialidad", leida.Description);
        Assert.Equal(Produccion, leida.TagId);
        Assert.True(leida.IsFavorite);
        Assert.Equal("azul", leida.ClaveDeColor);
        Assert.Equal("puertos", leida.ClaveDeIcono);
        Assert.Equal("Se reinicia los domingos", leida.Notes);
        Assert.Equal("operador", leida.UserName);
        Assert.Equal(2022, leida.Port);
        Assert.Equal(7, leida.SortOrder);
        Assert.False(leida.EsRapida);
        Assert.Equal("Infraestructura", leida.CustomFields["responsable"]);
        Assert.Equal("B-12", leida.CustomFields["rack"]);
    }

    [Fact]
    public async Task La_marca_de_conexion_rapida_sobrevive_a_guardar_y_leer()
    {
        using var db = new TempDatabase();
        var repo = await RepositorioAsync(db);
        var c = new Connection(Guid.NewGuid(), "Rápida", Protocol.Ssh, "192.0.2.5")
        {
            EsRapida = true,
        };

        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings()));

        Assert.True((await repo.GetByIdAsync(c.Id))!.Connection.EsRapida);
    }

    [Fact]
    public async Task Los_ajustes_de_cada_protocolo_sobreviven_a_guardar_y_leer()
    {
        using var db = new TempDatabase();
        var repo = await RepositorioAsync(db);

        var rdp = new Connection(Guid.NewGuid(), "Escritorio", Protocol.Rdp, "192.0.2.40");
        await repo.AddAsync(new ConnectionRecord(rdp, Rdp: new RdpSettings
        {
            ConnectionId = rdp.Id,
            Domain = "VIALIDAD",
            ClipboardEnabled = false,
            IgnoreCertificateWarnings = true,
            AbreEnVentanaPropia = true,
        }));

        var ssh = new Connection(Guid.NewGuid(), "Consola", Protocol.Ssh, "192.0.2.41");
        await repo.AddAsync(new ConnectionRecord(ssh, Ssh: new SshSettings
        {
            ConnectionId = ssh.Id,
            AuthMethod = SshAuthMethod.PrivateKey,
            PrivateKeyPath = @"C:\claves\id_ed25519",
            CertificatePath = @"C:\claves\id_ed25519-cert.pub",
            KnownHostFingerprint = "SHA256:abc",
            KeepAliveSeconds = 120,
        }));

        var web = new Connection(Guid.NewGuid(), "Panel", Protocol.Web, "panel.local");
        await repo.AddAsync(new ConnectionRecord(web, Web: new WebSettings
        {
            ConnectionId = web.Id,
            Url = "https://panel.local/admin",
            Browser = "firefox",
            PrivateWindow = true,
        }));

        var leidoRdp = (await repo.GetByIdAsync(rdp.Id))!.Rdp!;
        Assert.Equal("VIALIDAD", leidoRdp.Domain);
        Assert.False(leidoRdp.ClipboardEnabled);
        Assert.True(leidoRdp.IgnoreCertificateWarnings);
        Assert.True(leidoRdp.AbreEnVentanaPropia);

        var leidoSsh = (await repo.GetByIdAsync(ssh.Id))!.Ssh!;
        Assert.Equal(SshAuthMethod.PrivateKey, leidoSsh.AuthMethod);
        Assert.Equal(@"C:\claves\id_ed25519", leidoSsh.PrivateKeyPath);
        Assert.Equal(@"C:\claves\id_ed25519-cert.pub", leidoSsh.CertificatePath);
        Assert.Equal("SHA256:abc", leidoSsh.KnownHostFingerprint);
        Assert.Equal(120, leidoSsh.KeepAliveSeconds);

        var leidoWeb = (await repo.GetByIdAsync(web.Id))!.Web!;
        Assert.Equal("https://panel.local/admin", leidoWeb.Url);
        Assert.Equal("firefox", leidoWeb.Browser);
        Assert.True(leidoWeb.PrivateWindow);
    }

    [Fact]
    public async Task Actualizar_conserva_los_campos_de_catalogo()
    {
        using var db = new TempDatabase();
        var repo = await RepositorioAsync(db);
        var c = Completa();

        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings()));

        c.Description = "Otra descripción";
        c.TagId = Desarrollo;
        c.IsFavorite = false;
        c.SetCustomField("responsable", null);

        await repo.UpdateAsync(new ConnectionRecord(c, Ssh: new SshSettings()));
        var leida = (await repo.GetByIdAsync(c.Id))!.Connection;

        Assert.Equal("Otra descripción", leida.Description);
        Assert.Equal(Desarrollo, leida.TagId);
        Assert.False(leida.IsFavorite);
        Assert.False(leida.CustomFields.ContainsKey("responsable"));
        Assert.Equal("B-12", leida.CustomFields["rack"]);
    }

    [Fact]
    public async Task Una_conexion_sin_catalogo_vuelve_con_todo_vacio_y_no_con_cadenas_vacias()
    {
        using var db = new TempDatabase();
        var repo = await RepositorioAsync(db);
        var c = new Connection(Guid.NewGuid(), "Simple", Protocol.Web, "ejemplo.local");

        await repo.AddAsync(new ConnectionRecord(
            c, Web: new WebSettings { Url = "https://ejemplo.local" }));

        var leida = (await repo.GetByIdAsync(c.Id))!.Connection;

        Assert.Null(leida.Description);
        Assert.Null(leida.TagId);
        Assert.Null(leida.ClaveDeColor);
        Assert.False(leida.IsFavorite);
        Assert.Empty(leida.CustomFields);
    }

    [Fact]
    public async Task El_padre_sobrevive_a_guardar_y_leer()
    {
        using var db = new TempDatabase();
        var repo = await RepositorioAsync(db);

        var servidor = new Connection(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.207");
        await repo.AddAsync(new ConnectionRecord(servidor, Ssh: new SshSettings()));

        var servicio = new Connection(Guid.NewGuid(), "Portainer", Protocol.Web, "192.0.2.207")
        {
            ParentConnectionId = servidor.Id,
        };

        await repo.AddAsync(new ConnectionRecord(
            servicio, Web: new WebSettings { Url = "https://192.0.2.207:9443" }));

        var leida = (await repo.GetByIdAsync(servicio.Id))!.Connection;

        Assert.Equal(servidor.Id, leida.ParentConnectionId);
    }

    [Fact]
    public async Task La_base_rechaza_un_json_corrupto_en_campos_propios()
    {
        using var db = new TempDatabase();
        var repo = await RepositorioAsync(db);
        var c = new Connection(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.1");

        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings()));

        using var cn = db.Factory.Create();
        using var cmd = cn.CreateCommand();
        cmd.CommandText =
            "UPDATE connections SET custom_fields = '{esto no es json' WHERE id = @id;";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = c.Id.ToString("D");
        cmd.Parameters.Add(p);

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => cmd.ExecuteNonQuery());
    }
}
