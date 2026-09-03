using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>
/// Base temporal por prueba, creada y destruida por ella. Nunca se toca una base real:
/// lo exige el Principio III y la regla de no operar sobre datos del usuario.
/// </summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        Root = Path.Combine(Path.GetTempPath(), "cmc-tests", Guid.NewGuid().ToString("N"));
        Paths = new AppPaths(Root);
        Paths.EnsureCreated();
        Factory = new SqliteConnectionFactory(Paths.DatabasePath);
    }

    public string Root { get; }

    public AppPaths Paths { get; }

    public SqliteConnectionFactory Factory { get; }

    public DatabaseInitializer CreateInitializer() => new(Factory, Paths);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

public class DatabaseInitializerTests
{
    [Fact]
    public async Task Una_base_nueva_queda_en_la_ultima_version()
    {
        using var db = new TempDatabase();

        var result = await db.CreateInitializer().InitializeAsync();

        Assert.True(result.Migrated);
        Assert.Equal(0, result.FromVersion);
        Assert.Equal(DatabaseInitializer.LatestVersion, result.ToVersion);
        Assert.Null(result.RecoveredFromCorruptionPath);
    }

    [Fact]
    public async Task Una_base_ya_migrada_no_se_vuelve_a_migrar()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        var segunda = await db.CreateInitializer().InitializeAsync();

        Assert.False(segunda.Migrated);
        Assert.Equal(DatabaseInitializer.LatestVersion, segunda.FromVersion);
    }

    [Fact]
    public async Task Crea_las_ocho_tablas_del_esquema()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader = cmd.ExecuteReader();

        var tablas = new List<string>();
        while (reader.Read())
        {
            tablas.Add(reader.GetString(0));
        }

        Assert.Contains("connection_folders", tablas);
        Assert.Contains("folder_settings", tablas);
        Assert.Contains("connections", tablas);
        Assert.Contains("rdp_settings", tablas);
        Assert.Contains("ssh_settings", tablas);
        Assert.Contains("web_settings", tablas);
        Assert.Contains("ssh_tunnels", tablas);
        Assert.Contains("connection_history", tablas);
        Assert.Contains("application_settings", tablas);
    }

    [Fact]
    public async Task Las_claves_foraneas_quedan_activadas_en_cada_conexion()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";

        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
    }

    [Fact]
    public async Task Una_base_corrupta_se_preserva_y_se_crea_una_nueva()
    {
        using var db = new TempDatabase();
        await File.WriteAllTextAsync(db.Paths.DatabasePath, "esto no es una base de datos");

        var result = await db.CreateInitializer().InitializeAsync();

        Assert.NotNull(result.RecoveredFromCorruptionPath);
        Assert.True(File.Exists(result.RecoveredFromCorruptionPath));
        Assert.Equal(DatabaseInitializer.LatestVersion, result.ToVersion);

        var preservado = await File.ReadAllTextAsync(result.RecoveredFromCorruptionPath!);
        Assert.Equal("esto no es una base de datos", preservado);
    }

    /// <remarks>Cubre FR-052: si tras tres reintentos no se puede mover el archivo, el segundo <c>Migrate()</c> no debe reventar.</remarks>
    [Fact]
    public async Task Si_no_se_puede_apartar_la_base_corrupta_el_arranque_no_revienta()
    {
        using var db = new TempDatabase();
        await File.WriteAllTextAsync(db.Paths.DatabasePath, "esto no es una base de datos");

        using var bloqueo = new FileStream(
            db.Paths.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.None);

        DatabaseStartupResult? result = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            result = await db.CreateInitializer().InitializeAsync();
        });

        Assert.Null(exception);
        Assert.NotNull(result);

        Assert.Null(result!.RecoveredFromCorruptionPath);
    }

    [Fact]
    public async Task Ante_una_base_corrupta_no_lanza_excepcion()
    {
        // No poder abrir la base no debe impedir arrancar la aplicacion (FR-052).
        using var db = new TempDatabase();
        await File.WriteAllTextAsync(db.Paths.DatabasePath, "basura");

        var exception = await Record.ExceptionAsync(
            () => db.CreateInitializer().InitializeAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task El_protocolo_Web_es_un_valor_valido_en_el_esquema()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO connections (id, name, protocol, host, created_at, updated_at)
            VALUES ('x', 'Panel', 'Web', 'https://panel.local', '2026-08-24', '2026-08-24');
            """;

        var filas = cmd.ExecuteNonQuery();

        Assert.Equal(1, filas);
    }

    [Fact]
    public async Task Un_protocolo_desconocido_se_rechaza()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO connections (id, name, protocol, host, created_at, updated_at)
            VALUES ('y', 'X', 'Telnet', 'h', '2026-08-24', '2026-08-24');
            """;

        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }

    [Fact]
    public async Task Un_puerto_nulo_se_acepta_porque_significa_heredar()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO connections (id, name, protocol, host, port, created_at, updated_at)
            VALUES ('z', 'Servidor', 'Ssh', 'h', NULL, '2026-08-24', '2026-08-24');
            """;

        Assert.Equal(1, cmd.ExecuteNonQuery());
    }

    [Fact]
    public async Task Un_puerto_fuera_de_rango_se_rechaza()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO connections (id, name, protocol, host, port, created_at, updated_at)
            VALUES ('w', 'Servidor', 'Ssh', 'h', 70000, '2026-08-24', '2026-08-24');
            """;

        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }
}
