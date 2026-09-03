using CafManagerConection.Domain.Connections;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <remarks>
/// Doce columnas se mapean a mano en la fila, los parámetros, el INSERT y el UPDATE; Dapper
/// resuelve por nombre en tiempo de ejecución, así que un nombre mal escrito no rompe la
/// compilación.
/// </remarks>
public sealed class CatalogoIdaYVueltaTests
{
    private static async Task<ConnectionRepository> RepositorioAsync(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();
        return new ConnectionRepository(db.Factory);
    }

    // Las que siembra la migracion 003, con identificadores fijos.
    private static readonly Guid Produccion = Guid.Parse("11111111-0000-4000-8000-000000000001");
    private static readonly Guid Desarrollo = Guid.Parse("11111111-0000-4000-8000-000000000004");

    private static Connection Completa()
    {
        var c = new Connection(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.207")
        {
            Description = "Servidor de aplicaciones de Vialidad",
            TagId = Produccion,
            IsFavorite = true,
            DocumentationUrl = "https://wiki.interno/aplicaciones",
            ClaveDeColor = "azul",
        };

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
        Assert.Equal("https://wiki.interno/aplicaciones", leida.DocumentationUrl);
        Assert.Equal("azul", leida.ClaveDeColor);
        Assert.Equal("Infraestructura", leida.CustomFields["responsable"]);
        Assert.Equal("B-12", leida.CustomFields["rack"]);
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

        await repo.AddAsync(new ConnectionRecord(c, Web: new WebSettings()));
        var leida = (await repo.GetByIdAsync(c.Id))!.Connection;

        Assert.Null(leida.Description);
        Assert.Null(leida.TagId);
        Assert.Null(leida.DocumentationUrl);
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

        await repo.AddAsync(new ConnectionRecord(servicio, Web: new WebSettings()));
        var leida = (await repo.GetByIdAsync(servicio.Id))!.Connection;

        Assert.Equal(servidor.Id, leida.ParentConnectionId);
    }

    [Fact]
    public async Task Un_json_corrupto_en_campos_propios_no_impide_cargar_la_conexion()
    {
        using var db = new TempDatabase();
        var repo = await RepositorioAsync(db);
        var c = new Connection(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.1");

        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings()));

        using (var cn = db.Factory.Create())
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText =
                "UPDATE connections SET custom_fields = '{esto no es json' WHERE id = @id;";
            var p = cmd.CreateParameter();
            p.ParameterName = "@id";
            p.Value = c.Id.ToString("D");
            cmd.Parameters.Add(p);
            cmd.ExecuteNonQuery();
        }

        var leida = (await repo.GetByIdAsync(c.Id))!.Connection;

        Assert.Equal("Aplicaciones", leida.Name);
        Assert.Empty(leida.CustomFields);
    }
}
