using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>
/// Base temporal por prueba, creada y destruida por ella. Nunca se toca una base real:
/// lo exige la regla de no operar sobre datos del usuario.
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
    public async Task Crea_las_tablas_del_esquema_y_ninguna_mas()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        Assert.Equal(
            [
                "application_settings",
                "connection_folders",
                "connection_history",
                "connections",
                "folder_settings",
                "rdp_settings",
                "ssh_settings",
                "ssh_tunnels",
                "tags",
                "vault",
                "web_settings",
            ],
            Tablas(db));
    }

    [Fact]
    public async Task No_queda_rastro_del_formato_anterior_del_vault()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        Assert.DoesNotContain("vault_007", Tablas(db));
        Assert.DoesNotContain("vault_credenciales", Tablas(db));
        Assert.DoesNotContain("clave_dpapi", Columnas(db, "vault"));
        Assert.DoesNotContain("clave_envuelta", Columnas(db, "vault"));
    }

    [Fact]
    public async Task Las_columnas_que_la_secuencia_anterior_retiraba_no_se_crean()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        Assert.DoesNotContain("environment", Columnas(db, "connections"));
        Assert.DoesNotContain("tags", Columnas(db, "connections"));
        Assert.DoesNotContain("environment", Columnas(db, "folder_settings"));
        Assert.DoesNotContain("tags", Columnas(db, "connection_folders"));
    }

    [Fact]
    public async Task La_clave_logica_de_credencial_se_retiro_de_las_dos_tablas()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        Assert.DoesNotContain("credential_key", Columnas(db, "connections"));
        Assert.Contains("secreto", Columnas(db, "connections"));
        Assert.Contains("secreto_nonce", Columnas(db, "connections"));

        Assert.DoesNotContain("rdp_credential_key", Columnas(db, "folder_settings"));
        Assert.DoesNotContain("ssh_credential_key", Columnas(db, "folder_settings"));
        Assert.DoesNotContain("web_credential_key", Columnas(db, "folder_settings"));
        Assert.Contains("ssh_secreto", Columnas(db, "folder_settings"));
        Assert.Contains("ssh_secreto_nonce", Columnas(db, "folder_settings"));
    }

    [Fact]
    public async Task Las_etiquetas_semilla_llegan_con_su_codigo_y_su_orden_finales()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT code FROM tags ORDER BY sort_order;";
        using var reader = cmd.ExecuteReader();

        var codigos = new List<string>();
        while (reader.Read())
        {
            codigos.Add(reader.GetString(0));
        }

        Assert.Equal(["PRD", "PRE", "QA", "CAPA", "DESA"], codigos);
    }

    [Fact]
    public async Task Un_nonce_sin_secreto_se_rechaza()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO connections (id, name, protocol, host, secreto_nonce, created_at, updated_at)
            VALUES ('n', 'Servidor', 'Ssh', 'h', X'0102', '2026-08-24', '2026-08-24');
            """;

        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }

    [Fact]
    public async Task Una_base_de_otro_esquema_no_se_toca()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        using (var connection = db.Factory.Create())
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA user_version = 8;";
            cmd.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
        var antes = await File.ReadAllBytesAsync(db.Paths.DatabasePath);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.CreateInitializer().InitializeAsync());

        Assert.Contains("esquema 8", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("versión anterior", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("más nueva", excepcion.Message, StringComparison.Ordinal);

        SqliteConnection.ClearAllPools();
        Assert.Equal(antes, await File.ReadAllBytesAsync(db.Paths.DatabasePath));
    }

    private static List<string> Tablas(TempDatabase db)
    {
        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT name FROM sqlite_master WHERE type='table' "
            + "AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        using var reader = cmd.ExecuteReader();

        var tablas = new List<string>();
        while (reader.Read())
        {
            tablas.Add(reader.GetString(0));
        }

        return tablas;
    }

    private static List<string> Columnas(TempDatabase db, string tabla)
    {
        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tabla});";
        using var reader = cmd.ExecuteReader();

        var columnas = new List<string>();
        while (reader.Read())
        {
            columnas.Add(reader.GetString(1));
        }

        return columnas;
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

    /// <remarks>Si tras tres reintentos no se puede mover el archivo, el segundo <c>Migrate()</c> no debe reventar.</remarks>
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
        // No poder abrir la base no debe impedir arrancar la aplicacion.
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
