using CafManagerConection.Domain.Connections;
using CafManagerConection.Infrastructure.Credentials;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.Infrastructure.Tests.Database;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using CafManagerConection.UseCases.Folders;

namespace CafManagerConection.Infrastructure.Tests;

public class FlujoCompletoTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly FolderService _folders;
    private readonly ConnectionService _connections;
    private readonly List<string> _credencialesCreadas = [];
    private readonly WindowsCredentialStore _credentials = new();

    public FlujoCompletoTests()
    {
        _db.CreateInitializer().InitializeAsync().GetAwaiter().GetResult();

        var folderRepo = new FolderRepository(_db.Factory);
        var connectionRepo = new ConnectionRepository(_db.Factory);

        _folders = new FolderService(folderRepo, connectionRepo, _credentials);
        _connections = new ConnectionService(connectionRepo, folderRepo, _credentials);
    }

    [Fact]
    public async Task Crear_carpeta_y_conexion_y_verlas_en_el_arbol()
    {
        var carpeta = await _folders.CreateAsync("Producción", null);
        Assert.True(carpeta.Success);

        var conexion = new Connection(Guid.NewGuid(), "Linux Web", Protocol.Ssh, "192.0.2.5")
        {
            FolderId = carpeta.Value!.Id,
        };

        var result = await _connections.CreateAsync(
            new ConnectionRecord(conexion, Ssh: new SshSettings { ConnectionId = conexion.Id }));

        Assert.True(result.Success);

        var arbol = await _connections.GetTreeAsync();

        var item = Assert.Single(arbol);
        Assert.Equal("Linux Web", item.Name);
        Assert.Equal(carpeta.Value.Id, item.FolderId);
        Assert.Equal(22, item.EffectivePort);
    }

    [Fact]
    public async Task Veinte_conexiones_heredan_usuario_y_puerto_de_su_carpeta()
    {
        // El escenario de SC-013, ahora contra la base real.
        var carpeta = (await _folders.CreateAsync("Producción", null)).Value!;
        carpeta.Settings.UserName = "root";
        carpeta.Settings.Port = 2222;
        await _folders.UpdateSettingsAsync(carpeta);

        for (var i = 0; i < 20; i++)
        {
            var c = new Connection(Guid.NewGuid(), $"srv-{i:D2}", Protocol.Ssh, $"192.0.2.{i}")
            {
                FolderId = carpeta.Id,
            };
            await _connections.CreateAsync(
                new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));
        }

        var arbol = await _connections.GetTreeAsync();

        Assert.Equal(20, arbol.Count);
        Assert.All(arbol, c =>
        {
            Assert.Equal("root", c.EffectiveUserName);
            Assert.Equal(2222, c.EffectivePort);
        });
    }

    [Fact]
    public async Task La_busqueda_ignora_mayusculas_y_acentos()
    {
        var c = new Connection(Guid.NewGuid(), "Producción Web", Protocol.Ssh, "192.0.2.1");
        await _connections.CreateAsync(
            new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));

        Assert.Single(await _connections.SearchAsync("produccion"));
        Assert.Single(await _connections.SearchAsync("PRODUCCIÓN"));
        Assert.Single(await _connections.SearchAsync("web"));
        Assert.Empty(await _connections.SearchAsync("nada"));
    }

    [Fact]
    public async Task La_busqueda_encuentra_por_host_y_por_usuario_efectivo()
    {
        var carpeta = (await _folders.CreateAsync("Prod", null)).Value!;
        carpeta.Settings.UserName = "administrador";
        await _folders.UpdateSettingsAsync(carpeta);

        var c = new Connection(Guid.NewGuid(), "Servidor", Protocol.Ssh, "192.0.2.20")
        {
            FolderId = carpeta.Id,
        };
        await _connections.CreateAsync(
            new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));

        Assert.Single(await _connections.SearchAsync("192.0.2"));
        // Busca por el usuario heredado, no solo por el propio (FR-122).
        Assert.Single(await _connections.SearchAsync("administrador"));
    }

    [Fact]
    public async Task Mover_una_conexion_a_otra_carpeta_avisa_los_cambios()
    {
        var dev = (await _folders.CreateAsync("Desarrollo", null)).Value!;
        dev.Settings.UserName = "dev";
        dev.Settings.Port = 22;
        await _folders.UpdateSettingsAsync(dev);

        var prod = (await _folders.CreateAsync("Producción", null)).Value!;
        prod.Settings.UserName = "root";
        prod.Settings.Port = 2222;
        await _folders.UpdateSettingsAsync(prod);

        var c = new Connection(Guid.NewGuid(), "S", Protocol.Ssh, "h") { FolderId = dev.Id };
        await _connections.CreateAsync(
            new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));

        var cambios = await _connections.PreviewMoveAsync(c.Id, prod.Id);

        Assert.Contains(cambios, x => x.Contains("Puerto", StringComparison.Ordinal));
        Assert.Contains(cambios, x => x.Contains("Usuario", StringComparison.Ordinal));
    }

    [Fact]
    public async Task No_se_puede_mover_una_carpeta_dentro_de_su_propia_subcarpeta()
    {
        var raiz = (await _folders.CreateAsync("Raíz", null)).Value!;
        var hija = (await _folders.CreateAsync("Hija", raiz.Id)).Value!;

        var result = await _folders.MoveAsync(raiz.Id, hija.Id);

        Assert.False(result.Success);
        Assert.Contains("subcarpeta", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_impacto_de_borrar_una_carpeta_cuenta_todo_su_contenido()
    {
        var raiz = (await _folders.CreateAsync("Producción", null)).Value!;
        var hija = (await _folders.CreateAsync("DMZ", raiz.Id)).Value!;

        foreach (var folderId in new[] { raiz.Id, hija.Id })
        {
            var c = new Connection(Guid.NewGuid(), "S", Protocol.Ssh, "h") { FolderId = folderId };
            await _connections.CreateAsync(
                new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));
        }

        var impacto = await _folders.GetDeletionImpactAsync(raiz.Id);

        Assert.Equal(2, impacto.FolderCount);
        Assert.Equal(2, impacto.ConnectionCount);
    }

    [Fact]
    public async Task Duplicar_una_conexion_copia_sus_parametros()
    {
        var c = new Connection(Guid.NewGuid(), "Original", Protocol.Ssh, "192.0.2.1")
        {
            UserName = "root",
            Notes = "una nota",
        };
        c.SetPort(2222);
        await _connections.CreateAsync(new ConnectionRecord(
            c, Ssh: new SshSettings { ConnectionId = c.Id, KeepAliveSeconds = 120 }));

        var copia = await _connections.DuplicateAsync(c.Id);

        Assert.True(copia.Success);
        var detalle = await _connections.GetDetailAsync(copia.Value);
        Assert.Equal("Original (copia)", detalle!.Connection.Name);
        Assert.Equal(2222, detalle.Connection.Port);
        Assert.Equal("una nota", detalle.Connection.Notes);
        Assert.Equal(120, detalle.Ssh!.KeepAliveSeconds);
    }

    [Fact]
    public async Task Un_nombre_repetido_en_la_misma_carpeta_se_detecta_pero_no_impide_guardar()
    {
        var carpeta = (await _folders.CreateAsync("Prod", null)).Value!;

        var a = new Connection(Guid.NewGuid(), "Servidor", Protocol.Ssh, "h")
        {
            FolderId = carpeta.Id,
        };
        await _connections.CreateAsync(
            new ConnectionRecord(a, Ssh: new SshSettings { ConnectionId = a.Id }));

        Assert.True(await _connections.IsNameDuplicatedAsync(carpeta.Id, "Servidor"));

        var b = new Connection(Guid.NewGuid(), "Servidor", Protocol.Ssh, "h2")
        {
            FolderId = carpeta.Id,
        };
        var result = await _connections.CreateAsync(
            new ConnectionRecord(b, Ssh: new SshSettings { ConnectionId = b.Id }));

        Assert.True(result.Success); // FR-053: se advierte, no se impide
    }

    [Fact]
    public async Task Una_conexion_web_invalida_se_rechaza_con_un_mensaje_claro()
    {
        var c = new Connection(Guid.NewGuid(), "Panel", Protocol.Web, "panel.local");

        var result = await _connections.CreateAsync(new ConnectionRecord(
            c, Web: new WebSettings { ConnectionId = c.Id, Url = "no-es-una-url" }));

        Assert.False(result.Success);
        Assert.Contains("URL", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guardar_una_credencial_la_deja_en_el_almacen_y_no_en_la_base()
    {
        var c = new Connection(Guid.NewGuid(), "Servidor", Protocol.Ssh, "192.0.2.1");
        const string secreto = "MiClaveDePrueba-9871";

        var result = await _connections.CreateAsync(
            new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }),
            new CredentialPromptResult("root", null, secreto, Remember: true));

        Assert.True(result.Success);
        _credencialesCreadas.Add($"cmc:ssh:{c.Id:D}");

        using var guardada = await _credentials.ReadAsync($"cmc:ssh:{c.Id:D}");
        Assert.Equal(secreto, guardada!.RevealSecret());

        var bytes = await File.ReadAllBytesAsync(_db.Paths.DatabasePath);
        var texto = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain(secreto, texto, StringComparison.Ordinal);
        Assert.Contains($"cmc:ssh:{c.Id:D}", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Borrar_una_conexion_borra_tambien_su_credencial()
    {
        var c = new Connection(Guid.NewGuid(), "Servidor", Protocol.Ssh, "192.0.2.1");
        var key = $"cmc:ssh:{c.Id:D}";

        await _connections.CreateAsync(
            new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }),
            new CredentialPromptResult("root", null, "clave", Remember: true));
        _credencialesCreadas.Add(key);

        Assert.True(await _credentials.ExistsAsync(key));

        var result = await _connections.DeleteAsync(c.Id);

        Assert.True(result.Success);
        Assert.False(await _credentials.ExistsAsync(key));
        Assert.Null(await _connections.GetDetailAsync(c.Id));
    }

    public void Dispose()
    {
        foreach (var key in _credencialesCreadas)
        {
            try
            {
                _credentials.DeleteAsync(key).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
            }
        }

        _db.Dispose();
    }
}
