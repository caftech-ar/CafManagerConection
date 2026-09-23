using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.Infrastructure.Database.Migrations;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>La 005 corrida sobre una base en la versión 4 con las claves del juego de iconos anterior.</summary>
public class Migration005Tests
{
    private const string Fecha = "2026-08-24T00:00:00.0000000Z";

    // Las 16 que el juego anterior sabía escribir, con la del catálogo que les corresponde.
    public static TheoryData<string, string> Traducciones() => new()
    {
        { "carpeta", "folder" },
        { "escritorio", "device-desktop-share" },
        { "terminal", "terminal-2" },
        { "web", "world-www" },
        { "base-de-datos", "database" },
        { "correo", "mail" },
        { "archivos", "folder-open" },
        { "respaldo", "archive" },
        { "contenedor", "brand-docker" },
        { "cortafuegos", "wall" },
        { "monitoreo", "activity" },
        { "proxy", "nginx" },
        { "servicios", "settings-automation" },
        { "red", "route" },
        { "puertos", "plug" },
        { "aplicacion", "box" },
    };

    [Theory]
    [MemberData(nameof(Traducciones))]
    public async Task Una_conexion_conserva_su_icono_con_la_clave_nueva(string vieja, string nueva)
    {
        using var db = await BaseEnLaVersion4Async();

        Assert.Equal(nueva, Escalar(db, $"SELECT icon_key FROM connections WHERE id = 'c-{vieja}';"));
    }

    [Theory]
    [MemberData(nameof(Traducciones))]
    public async Task Una_carpeta_conserva_su_icono_con_la_clave_nueva(string vieja, string nueva)
    {
        using var db = await BaseEnLaVersion4Async();

        Assert.Equal(
            nueva,
            Escalar(db, $"SELECT icon_key FROM connection_folders WHERE id = 'f-{vieja}';"));
    }

    [Theory]
    [MemberData(nameof(Traducciones))]
    public void Toda_clave_traducida_existe_en_el_catalogo(string vieja, string nueva)
    {
        Assert.True(CatalogoDeIconos.EsValido(nueva), $"«{vieja}» apunta a «{nueva}», que no está.");
    }

    [Fact]
    public async Task Una_conexion_sin_icono_elegido_sigue_sin_icono()
    {
        using var db = await BaseEnLaVersion4Async();

        Assert.Null(Escalar(db, "SELECT icon_key FROM connections WHERE id = 'c-sin-icono';"));
    }

    [Fact]
    public async Task Una_clave_que_no_es_del_juego_anterior_no_se_toca()
    {
        using var db = await BaseEnLaVersion4Async();

        Assert.Equal("server", Escalar(db, "SELECT icon_key FROM connections WHERE id = 'c-ya-nueva';"));
        Assert.Equal("cualquiera", Escalar(db, "SELECT icon_key FROM connections WHERE id = 'c-rara';"));
    }

    [Fact]
    public async Task La_base_queda_en_la_version_cinco()
    {
        using var db = await BaseEnLaVersion4Async();

        Assert.Equal("5", Escalar(db, "PRAGMA user_version;"));
        Assert.Equal(5, DatabaseInitializer.LatestVersion);
    }

    [Fact]
    public async Task Correrla_dos_veces_no_cambia_nada()
    {
        using var db = await BaseEnLaVersion4Async();

        var antes = Escalar(db, "SELECT icon_key FROM connections WHERE id = 'c-carpeta';");

        using (var connection = db.Factory.Create())
        {
            Ejecutar(connection, Migration005_ClavesDeIconoDelCatalogo.Sql);
        }

        Assert.Equal(antes, Escalar(db, "SELECT icon_key FROM connections WHERE id = 'c-carpeta';"));
    }

    /// <summary>Arma una base en la versión 4 con una fila por cada clave vieja y la deja ya migrada.</summary>
    private static async Task<TempDatabase> BaseEnLaVersion4Async()
    {
        var db = new TempDatabase();

        try
        {
            using (var connection = db.Factory.Create())
            {
                Ejecutar(connection, Migration001_Esquema.Sql);
                Ejecutar(connection, Migration002_CamposDeCarpeta.Sql);
                Ejecutar(connection, Migration003_UsuarioYPuertoPorProtocolo.Sql);
                Ejecutar(connection, Migration004_Saneamiento.Sql);
                Ejecutar(connection, Datos());
                Ejecutar(connection, "PRAGMA user_version = 4;");
            }

            await db.CreateInitializer().InitializeAsync();
            return db;
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }

    private static string Datos()
    {
        var filas = new List<string>();

        foreach (var caso in Traducciones())
        {
            var vieja = (string)caso[0];

            filas.Add($"INSERT INTO connection_folders (id, name, created_at, updated_at, icon_key) "
                      + $"VALUES ('f-{vieja}', 'Carpeta {vieja}', '{Fecha}', '{Fecha}', '{vieja}');");

            filas.Add(Conexion($"c-{vieja}", $"'{vieja}'"));
        }

        filas.Add(Conexion("c-sin-icono", "NULL"));
        filas.Add(Conexion("c-ya-nueva", "'server'"));
        filas.Add(Conexion("c-rara", "'cualquiera'"));

        return string.Join("\n", filas);
    }

    private static string Conexion(string id, string iconKey) =>
        "INSERT INTO connections (id, name, protocol, host, created_at, updated_at, icon_key) "
        + $"VALUES ('{id}', '{id}', 'Ssh', '192.0.2.1', '{Fecha}', '{Fecha}', {iconKey});";

    private static void Ejecutar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string? Escalar(TempDatabase db, string sql)
    {
        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        return cmd.ExecuteScalar() is { } valor and not DBNull
            ? valor.ToString()
            : null;
    }
}
