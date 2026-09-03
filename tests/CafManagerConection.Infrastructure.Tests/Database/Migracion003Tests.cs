using CafManagerConection.Infrastructure.Database;
using CafManagerConection.Infrastructure.Database.Migrations;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <remarks>
/// <c>ALTER TABLE … DROP COLUMN</c> de SQLite falla si la columna participa de un índice, una
/// vista o un disparador.
/// </remarks>
public sealed class Migracion003Tests : IDisposable
{
    private readonly string _ruta;

    public Migracion003Tests() =>
        _ruta = Path.Combine(Path.GetTempPath(), $"cmc-mig3-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_ruta);
        }
        catch (IOException)
        {
        }
    }

    private SqliteConnection Abrir()
    {
        var cn = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _ruta, Pooling = false }.ToString());

        cn.Open();
        return cn;
    }

    private static void Correr(SqliteConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static IReadOnlyList<string> Columnas(SqliteConnection cn, string tabla)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT name FROM pragma_table_info('{tabla}')";

        var nombres = new List<string>();
        using var lector = cmd.ExecuteReader();

        while (lector.Read())
        {
            nombres.Add(lector.GetString(0));
        }

        return nombres;
    }

    private SqliteConnection ConLasTresMigraciones()
    {
        var cn = Abrir();

        Correr(cn, Migration001_InitialSchema.Sql);
        Correr(cn, Migration002_ColorJerarquiaCatalogo.Sql);
        Correr(cn, Migration003_EtiquetasConfigurables.Sql);

        return cn;
    }

    [Fact]
    public void Se_aplica_sobre_el_esquema_anterior()
    {
        using var cn = ConLasTresMigraciones();

        Assert.NotEmpty(Columnas(cn, "tags"));
    }

    [Fact]
    public void Quedan_las_cuatro_etiquetas_iniciales()
    {
        using var cn = ConLasTresMigraciones();
        using var cmd = cn.CreateCommand();

        cmd.CommandText = "SELECT code || '|' || name || '|' || color FROM tags ORDER BY sort_order";

        var filas = new List<string>();
        using var lector = cmd.ExecuteReader();

        while (lector.Read())
        {
            filas.Add(lector.GetString(0));
        }

        Assert.Equal(
            [
                "PRD|Producción|rojo",
                "PRE|PreProducción|ambar",
                "CAP|Capacitación|cyan",
                "DEV|Desarrollo|verde",
            ],
            filas);
    }

    [Fact]
    public void Se_van_las_columnas_viejas()
    {
        using var cn = ConLasTresMigraciones();

        Assert.DoesNotContain("environment", Columnas(cn, "connections"));
        Assert.DoesNotContain("tags", Columnas(cn, "connections"));
        Assert.DoesNotContain("environment", Columnas(cn, "folder_settings"));
        Assert.DoesNotContain("tags", Columnas(cn, "connection_folders"));

        Assert.Contains("tag_id", Columnas(cn, "connections"));
        Assert.Contains("tag_id", Columnas(cn, "folder_settings"));
    }

    [Fact]
    public void Las_conexiones_sobreviven()
    {
        using var cn = Abrir();

        Correr(cn, Migration001_InitialSchema.Sql);
        Correr(cn, Migration002_ColorJerarquiaCatalogo.Sql);

        Correr(cn, """
            INSERT INTO connections (id, folder_id, name, protocol, host, sort_order,
                                     created_at, updated_at, environment, tags)
            VALUES ('c1', NULL, 'Servidor uno', 'Ssh', '192.0.2.1', 0,
                    datetime('now'), datetime('now'), 'Produccion', 'web,api');
            """);

        Correr(cn, Migration003_EtiquetasConfigurables.Sql);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT name, host FROM connections WHERE id = 'c1'";

        using var lector = cmd.ExecuteReader();

        Assert.True(lector.Read());
        Assert.Equal("Servidor uno", lector.GetString(0));
        Assert.Equal("192.0.2.1", lector.GetString(1));
    }

    /// <remarks>La etiqueta vive en <c>folder_settings</c>, no en <c>connection_folders</c>; <c>NULL</c> en la conexión significa "la de mi carpeta".</remarks>
    [Fact]
    public void Una_conexion_puede_llevar_su_etiqueta()
    {
        using var cn = ConLasTresMigraciones();

        Correr(cn, """
            INSERT INTO connections (id, folder_id, name, protocol, host, sort_order,
                                     created_at, updated_at, tag_id)
            SELECT 'c1', NULL, 'Servidor', 'Ssh', '192.0.2.1', 0,
                   datetime('now'), datetime('now'), id
            FROM tags WHERE code = 'PRD';
            """);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT t.code FROM connections c JOIN tags t ON t.id = c.tag_id WHERE c.id = 'c1'
            """;

        Assert.Equal("PRD", (string)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void Una_carpeta_puede_llevar_su_etiqueta()
    {
        using var cn = ConLasTresMigraciones();

        Correr(cn, """
            INSERT INTO connection_folders (id, parent_id, name, sort_order, created_at, updated_at)
            VALUES ('f1', NULL, 'Carpeta', 0, datetime('now'), datetime('now'));

            INSERT INTO folder_settings (folder_id, tag_id)
            SELECT 'f1', id FROM tags WHERE code = 'DEV';
            """);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT t.code FROM folder_settings f JOIN tags t ON t.id = f.tag_id
            WHERE f.folder_id = 'f1'
            """;

        Assert.Equal("DEV", (string)cmd.ExecuteScalar()!);
    }

    /// <remarks>Con CASCADE en lugar de SET NULL, borrar una etiqueta se llevaría cada servidor que la usaba.</remarks>
    [Fact]
    public void Borrar_una_etiqueta_no_borra_las_conexiones()
    {
        using var cn = ConLasTresMigraciones();

        Correr(cn, "PRAGMA foreign_keys = ON;");

        Correr(cn, """
            INSERT INTO connections (id, folder_id, name, protocol, host, sort_order,
                                     created_at, updated_at, tag_id)
            SELECT 'c1', NULL, 'Servidor', 'Ssh', '192.0.2.1', 0,
                   datetime('now'), datetime('now'), id
            FROM tags WHERE code = 'PRD';

            DELETE FROM tags WHERE code = 'PRD';
            """);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT name, tag_id FROM connections WHERE id = 'c1'";

        using var lector = cmd.ExecuteReader();

        Assert.True(lector.Read());
        Assert.Equal("Servidor", lector.GetString(0));
        Assert.True(lector.IsDBNull(1));
    }

    [Fact]
    public void No_se_puede_apuntar_a_una_etiqueta_inexistente()
    {
        using var cn = ConLasTresMigraciones();

        Correr(cn, "PRAGMA foreign_keys = ON;");

        Assert.Throws<SqliteException>(() => Correr(cn, """
            INSERT INTO connections (id, folder_id, name, protocol, host, sort_order,
                                     created_at, updated_at, tag_id)
            VALUES ('c1', NULL, 'Servidor', 'Ssh', '192.0.2.1', 0,
                    datetime('now'), datetime('now'), 'no-existe');
            """));
    }

    [Fact]
    public void No_entran_dos_codigos_iguales()
    {
        using var cn = ConLasTresMigraciones();

        var choque = Assert.Throws<SqliteException>(() => Correr(cn, """
            INSERT INTO tags (id, code, name, color, sort_order, created_at, updated_at)
            VALUES ('x', 'prd', 'Otra cosa', 'azul', 9, datetime('now'), datetime('now'));
            """));

        Assert.Contains("UNIQUE", choque.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_entran_dos_nombres_iguales() =>
        Assert.Throws<SqliteException>(() =>
        {
            using var cn = ConLasTresMigraciones();

            Correr(cn, """
                INSERT INTO tags (id, code, name, color, sort_order, created_at, updated_at)
                VALUES ('x', 'XX', 'producción', 'azul', 9, datetime('now'), datetime('now'));
                """);
        });

}
