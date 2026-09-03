using CafManagerConection.Infrastructure.Database;
using CafManagerConection.Infrastructure.Database.Migrations;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>
/// Migración a <c>user_version = 2</c>: color de icono, conexiones hijas y catálogo.
/// </summary>
/// <remarks>Corre sobre una base ya poblada a mano, no sobre una recién creada.</remarks>
public class Migracion2Tests
{
    /// <summary>Deja la base en <c>user_version = 1</c>, como si nunca hubiera migrado.</summary>
    private static void CrearEnVersion1(TempDatabase db)
    {
        using var cn = db.Factory.Create();

        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = Migration001_InitialSchema.Sql;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA user_version = 1;";
            cmd.ExecuteNonQuery();
        }
    }

    private static void Ejecutar(TempDatabase db, string sql)
    {
        using var cn = db.Factory.Create();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static T Escalar<T>(TempDatabase db, string sql)
    {
        using var cn = db.Factory.Create();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;

        var valor = cmd.ExecuteScalar();

        return valor is null or DBNull ? default! : (T)Convert.ChangeType(valor, typeof(T));
    }

    private static void SembrarDatos(TempDatabase db) => Ejecutar(db, """
        INSERT INTO connection_folders (id, name, sort_order, created_at, updated_at)
        VALUES ('f1', 'Trabajo', 0, '2026-01-01', '2026-01-01');

        INSERT INTO connections (id, folder_id, name, protocol, host, username,
                                 notes, created_at, updated_at, sort_order)
        VALUES ('c1', 'f1', 'Aplicaciones', 'Ssh', '192.0.2.207', 'SuperUsuario',
                'una nota que no se debe perder', '2026-01-01', '2026-01-01', 0);
        """);

    [Fact]
    public async Task Migrar_desde_la_version_1_conserva_los_datos()
    {
        using var db = new TempDatabase();
        CrearEnVersion1(db);
        SembrarDatos(db);

        var resultado = await db.CreateInitializer().InitializeAsync();

        Assert.True(resultado.Migrated);
        Assert.Equal(1, resultado.FromVersion);
        Assert.Equal(DatabaseInitializer.LatestVersion, resultado.ToVersion);

        Assert.Equal(1, Escalar<long>(db, "SELECT COUNT(*) FROM connection_folders"));
        Assert.Equal(1, Escalar<long>(db, "SELECT COUNT(*) FROM connections"));
        Assert.Equal("Aplicaciones", Escalar<string>(db, "SELECT name FROM connections WHERE id = 'c1'"));
        Assert.Equal(
            "una nota que no se debe perder",
            Escalar<string>(db, "SELECT notes FROM connections WHERE id = 'c1'"));
    }

    [Fact]
    public async Task Las_columnas_nuevas_nacen_nulas_en_las_filas_existentes()
    {
        using var db = new TempDatabase();
        CrearEnVersion1(db);
        SembrarDatos(db);

        await db.CreateInitializer().InitializeAsync();

        var nulas = Escalar<long>(db, """
            SELECT COUNT(*) FROM connections
            WHERE id = 'c1'
              AND icon_color           IS NULL
              AND parent_connection_id IS NULL
              AND description          IS NULL
              AND documentation_url    IS NULL
              AND custom_fields        IS NULL
            """);

        Assert.Equal(1, nulas);
        Assert.Equal(1, Escalar<long>(db, """
            SELECT COUNT(*) FROM connection_folders
            WHERE id = 'f1' AND icon_color IS NULL AND description IS NULL
            """));
    }

    [Fact]
    public async Task Una_favorita_no_marcada_vale_cero_y_no_nulo()
    {
        using var db = new TempDatabase();
        CrearEnVersion1(db);
        SembrarDatos(db);

        await db.CreateInitializer().InitializeAsync();

        Assert.Equal(0, Escalar<long>(db, "SELECT is_favorite FROM connections WHERE id = 'c1'"));
    }

    [Fact]
    public async Task Migrar_dos_veces_no_falla_ni_repite_trabajo()
    {
        // `ALTER TABLE ADD COLUMN` falla si la columna ya existe; el ejecutor solo aplica
        // las migraciones posteriores a la versión actual.
        using var db = new TempDatabase();
        CrearEnVersion1(db);
        SembrarDatos(db);

        await db.CreateInitializer().InitializeAsync();
        var segunda = await db.CreateInitializer().InitializeAsync();

        Assert.False(segunda.Migrated);
        Assert.Equal(DatabaseInitializer.LatestVersion, segunda.FromVersion);
        Assert.Equal(DatabaseInitializer.LatestVersion, segunda.ToVersion);
        Assert.Equal(1, Escalar<long>(db, "SELECT COUNT(*) FROM connections"));
    }

    [Fact]
    public async Task Una_base_nueva_llega_directo_a_la_version_2()
    {
        using var db = new TempDatabase();

        var resultado = await db.CreateInitializer().InitializeAsync();

        Assert.Equal(0, resultado.FromVersion);
        Assert.Equal(DatabaseInitializer.LatestVersion, resultado.ToVersion);
        Assert.True(DatabaseInitializer.LatestVersion >= 2);
    }

    [Fact]
    public async Task Borrar_una_conexion_arrastra_a_sus_hijas()
    {
        // FR-128.
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        Ejecutar(db, """
            PRAGMA foreign_keys = ON;

            INSERT INTO connections (id, name, protocol, host, created_at, updated_at, sort_order)
            VALUES ('padre', 'Aplicaciones', 'Ssh', '192.0.2.207', '2026-01-01', '2026-01-01', 0);

            INSERT INTO connections (id, name, protocol, host, parent_connection_id,
                                     port, created_at, updated_at, sort_order)
            VALUES ('hija', 'Portainer', 'Web', '192.0.2.207', 'padre',
                    9000, '2026-01-01', '2026-01-01', 0);
            """);

        Assert.Equal(2, Escalar<long>(db, "SELECT COUNT(*) FROM connections"));

        Ejecutar(db, "PRAGMA foreign_keys = ON; DELETE FROM connections WHERE id = 'padre';");

        Assert.Equal(0, Escalar<long>(db, "SELECT COUNT(*) FROM connections"));
    }

    [Fact]
    public async Task Los_indices_nuevos_existen()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        var indices = Escalar<long>(db, """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'index'
              AND name IN ('ix_connections_parent', 'ix_connections_favorite')
            """);

        Assert.Equal(2, indices);
    }
}
