using CafManagerConection.Infrastructure.Database;
using CafManagerConection.Infrastructure.Database.Migrations;

namespace CafManagerConection.Infrastructure.Tests.Database;

public class Migracion006Tests
{
    private static void CrearEnVersion5(TempDatabase db)
    {
        Ejecutar(db, Migration001_InitialSchema.Sql);
        Ejecutar(db, Migration002_ColorJerarquiaCatalogo.Sql);
        Ejecutar(db, Migration003_EtiquetasConfigurables.Sql);
        Ejecutar(db, Migration004_CertificadoSsh.Sql);
        Ejecutar(db, Migration005_EtiquetaQA.Sql);
        Ejecutar(db, "PRAGMA user_version = 5;");
    }

    private static void CrearEnVersion1(TempDatabase db)
    {
        Ejecutar(db, Migration001_InitialSchema.Sql);
        Ejecutar(db, "PRAGMA user_version = 1;");
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
        VALUES ('c1', 'f1', 'Aplicaciones', 'Ssh', '192.0.2.207', 'SuperUsuarioSV',
                'una nota que no se debe perder', '2026-01-01', '2026-01-01', 0);
        """);

    [Fact]
    public async Task Una_base_en_la_version_5_pasa_a_la_6()
    {
        using var db = new TempDatabase();
        CrearEnVersion5(db);

        var resultado = await db.CreateInitializer().InitializeAsync();

        Assert.True(resultado.Migrated);
        Assert.Equal(5, resultado.FromVersion);
        Assert.Equal(DatabaseInitializer.LatestVersion, resultado.ToVersion);
        Assert.True(DatabaseInitializer.LatestVersion >= Migration006_Icono.Version);
        Assert.True(Escalar<long>(db, "PRAGMA user_version") >= Migration006_Icono.Version);
    }

    [Fact]
    public async Task Las_dos_tablas_reciben_la_clave_de_icono()
    {
        using var db = new TempDatabase();
        CrearEnVersion5(db);

        await db.CreateInitializer().InitializeAsync();

        Assert.Equal(1, Escalar<long>(db, """
            SELECT COUNT(*) FROM pragma_table_info('connection_folders') WHERE name = 'icon_key'
            """));
        Assert.Equal(1, Escalar<long>(db, """
            SELECT COUNT(*) FROM pragma_table_info('connections') WHERE name = 'icon_key'
            """));
    }

    [Fact]
    public async Task La_clave_de_icono_es_anulable_en_las_dos_tablas()
    {
        using var db = new TempDatabase();
        CrearEnVersion5(db);

        await db.CreateInitializer().InitializeAsync();

        Assert.Equal(0, Escalar<long>(db, """
            SELECT SUM("notnull") FROM pragma_table_info('connection_folders')
             WHERE name = 'icon_key'
            """));
        Assert.Equal(0, Escalar<long>(db, """
            SELECT SUM("notnull") FROM pragma_table_info('connections') WHERE name = 'icon_key'
            """));
    }

    [Fact]
    public async Task Las_filas_que_ya_estaban_quedan_enteras_y_sin_icono_elegido()
    {
        using var db = new TempDatabase();
        CrearEnVersion5(db);
        SembrarDatos(db);

        await db.CreateInitializer().InitializeAsync();

        Assert.Equal("Aplicaciones", Escalar<string>(db, "SELECT name FROM connections WHERE id = 'c1'"));
        Assert.Equal(
            "una nota que no se debe perder",
            Escalar<string>(db, "SELECT notes FROM connections WHERE id = 'c1'"));
        Assert.Equal(
            "Trabajo",
            Escalar<string>(db, "SELECT name FROM connection_folders WHERE id = 'f1'"));

        Assert.Equal(1, Escalar<long>(db, """
            SELECT COUNT(*) FROM connections WHERE id = 'c1' AND icon_key IS NULL
            """));
        Assert.Equal(1, Escalar<long>(db, """
            SELECT COUNT(*) FROM connection_folders WHERE id = 'f1' AND icon_key IS NULL
            """));
    }

    [Fact]
    public async Task El_color_del_icono_sigue_conviviendo_con_la_clave()
    {
        using var db = new TempDatabase();
        CrearEnVersion5(db);
        SembrarDatos(db);
        Ejecutar(db, "UPDATE connections SET icon_color = 'ambar' WHERE id = 'c1';");

        await db.CreateInitializer().InitializeAsync();

        Assert.Equal(
            "ambar",
            Escalar<string>(db, "SELECT icon_color FROM connections WHERE id = 'c1'"));
        Assert.Equal(1, Escalar<long>(db, """
            SELECT COUNT(*) FROM pragma_table_info('connections') WHERE name = 'icon_color'
            """));
    }

    [Fact]
    public async Task Una_carpeta_y_una_conexion_guardan_la_clave_que_eligieron()
    {
        using var db = new TempDatabase();
        CrearEnVersion5(db);
        SembrarDatos(db);

        await db.CreateInitializer().InitializeAsync();

        Ejecutar(db, """
            UPDATE connections        SET icon_key = 'base-de-datos' WHERE id = 'c1';
            UPDATE connection_folders SET icon_key = 'nube'          WHERE id = 'f1';
            """);

        Assert.Equal(
            "base-de-datos",
            Escalar<string>(db, "SELECT icon_key FROM connections WHERE id = 'c1'"));
        Assert.Equal(
            "nube",
            Escalar<string>(db, "SELECT icon_key FROM connection_folders WHERE id = 'f1'"));
    }

    [Fact]
    public async Task Migrar_desde_la_version_1_llega_a_la_6_sin_perder_datos()
    {
        using var db = new TempDatabase();
        CrearEnVersion1(db);
        SembrarDatos(db);

        var resultado = await db.CreateInitializer().InitializeAsync();

        Assert.Equal(1, resultado.FromVersion);
        Assert.True(resultado.ToVersion >= Migration006_Icono.Version);
        Assert.Equal(1, Escalar<long>(db, "SELECT COUNT(*) FROM connections"));
        Assert.Equal(1, Escalar<long>(db, "SELECT COUNT(*) FROM connection_folders"));
        Assert.Equal("Aplicaciones", Escalar<string>(db, "SELECT name FROM connections WHERE id = 'c1'"));
        Assert.Equal(1, Escalar<long>(db, """
            SELECT COUNT(*) FROM connections WHERE id = 'c1' AND icon_key IS NULL
            """));
    }

    [Fact]
    public async Task Migrar_dos_veces_no_reaplica_la_006()
    {
        using var db = new TempDatabase();
        CrearEnVersion5(db);
        SembrarDatos(db);

        await db.CreateInitializer().InitializeAsync();
        var segunda = await db.CreateInitializer().InitializeAsync();

        Assert.False(segunda.Migrated);
        Assert.Equal(DatabaseInitializer.LatestVersion, segunda.FromVersion);
        Assert.Equal(DatabaseInitializer.LatestVersion, segunda.ToVersion);
        Assert.Equal(1, Escalar<long>(db, """
            SELECT COUNT(*) FROM pragma_table_info('connections') WHERE name = 'icon_key'
            """));
        Assert.Equal(1, Escalar<long>(db, "SELECT COUNT(*) FROM connections"));
    }
}
