using CafManagerConection.Infrastructure.Database;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

public class MotorDeMigracionesTests
{
    private const string EsquemaConCascada = """
        CREATE TABLE padres (
            id   TEXT PRIMARY KEY NOT NULL,
            name TEXT NOT NULL
        );

        CREATE TABLE hijas (
            id       TEXT PRIMARY KEY NOT NULL,
            padre_id TEXT NOT NULL REFERENCES padres(id) ON DELETE CASCADE,
            dato     TEXT NOT NULL
        );
        CREATE INDEX ix_hijas_padre ON hijas(padre_id);

        INSERT INTO padres (id, name) VALUES ('p1', 'Uno'), ('p2', 'Dos');
        INSERT INTO hijas (id, padre_id, dato) VALUES ('h1', 'p1', 'a'), ('h2', 'p2', 'b');
        """;

    // El procedimiento que exige SQLite para cambiar un CHECK: crear, copiar, bajar, renombrar y
    // recrear los indices.
    private const string RecrearPadres = """
        CREATE TABLE padres_nueva (
            id   TEXT PRIMARY KEY NOT NULL,
            name TEXT NOT NULL CHECK (TRIM(name) <> '')
        );

        INSERT INTO padres_nueva (id, name) SELECT id, name FROM padres;
        DROP TABLE padres;
        ALTER TABLE padres_nueva RENAME TO padres;
        """;

    [Fact]
    public void Recrear_una_tabla_no_se_lleva_sus_filas_hijas()
    {
        using var db = new TempDatabase();
        using var connection = db.Factory.Create();
        MotorDeMigraciones.Aplicar(connection, [EsquemaConCascada], 1);

        MotorDeMigraciones.Aplicar(connection, [RecrearPadres], 2);

        Assert.Equal(2, Contar(connection, "padres"));
        Assert.Equal(2, Contar(connection, "hijas"));
        Assert.Equal(2, Version(connection));
    }

    [Fact]
    public void Las_claves_foraneas_quedan_activas_despues_de_migrar()
    {
        using var db = new TempDatabase();
        using var connection = db.Factory.Create();

        MotorDeMigraciones.Aplicar(connection, [EsquemaConCascada], 1);

        Assert.Equal(1, Pragma(connection, "foreign_keys"));
    }

    [Fact]
    public void Las_claves_foraneas_quedan_activas_aunque_la_migracion_falle()
    {
        using var db = new TempDatabase();
        using var connection = db.Factory.Create();

        Assert.Throws<SqliteException>(
            () => MotorDeMigraciones.Aplicar(connection, ["DROP TABLE no_existe;"], 1));

        Assert.Equal(1, Pragma(connection, "foreign_keys"));
    }

    [Fact]
    public void Una_migracion_que_deja_huerfanas_no_se_aplica()
    {
        using var db = new TempDatabase();
        using var connection = db.Factory.Create();
        MotorDeMigraciones.Aplicar(connection, [EsquemaConCascada], 1);

        const string CopiaIncompleta = """
            CREATE TABLE padres_nueva (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL);
            INSERT INTO padres_nueva (id, name) SELECT id, name FROM padres WHERE id = 'p1';
            DROP TABLE padres;
            ALTER TABLE padres_nueva RENAME TO padres;
            """;

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => MotorDeMigraciones.Aplicar(connection, [CopiaIncompleta], 2));

        Assert.Contains("hijas", excepcion.Message, StringComparison.Ordinal);
        Assert.Equal(2, Contar(connection, "padres"));
        Assert.Equal(1, Version(connection));
    }

    [Fact]
    public void Una_migracion_que_falla_no_deja_la_version_avanzada()
    {
        using var db = new TempDatabase();
        using var connection = db.Factory.Create();
        MotorDeMigraciones.Aplicar(connection, [EsquemaConCascada], 1);

        Assert.Throws<SqliteException>(
            () => MotorDeMigraciones.Aplicar(connection, ["DROP TABLE no_existe;"], 2));

        Assert.Equal(1, Version(connection));
        Assert.Equal(2, Contar(connection, "padres"));
    }

    private static int Contar(SqliteConnection connection, string tabla)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {tabla};";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static int Version(SqliteConnection connection) => Pragma(connection, "user_version");

    private static int Pragma(SqliteConnection connection, string nombre)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA {nombre};";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
