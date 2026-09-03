using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.Infrastructure.Database.Migrations;
using CafManagerConection.UseCases.Abstractions;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Database;

/// <summary>Abre o crea la base y aplica las migraciones pendientes; la versión vive en el pragma <c>user_version</c>.</summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    // El orden tiene que ser ascendente: Migrate() aplica en secuencia las que superan la versión actual y LatestVersion toma la última.
    private static readonly (int Version, string Sql)[] Migrations =
    [
        (Migration001_InitialSchema.Version, Migration001_InitialSchema.Sql),
        (Migration002_ColorJerarquiaCatalogo.Version, Migration002_ColorJerarquiaCatalogo.Sql),
        (Migration003_EtiquetasConfigurables.Version, Migration003_EtiquetasConfigurables.Sql),
        (Migration004_CertificadoSsh.Version, Migration004_CertificadoSsh.Sql),
        (Migration005_EtiquetaQA.Version, Migration005_EtiquetaQA.Sql),
        (Migration006_Icono.Version, Migration006_Icono.Sql),
        (Migration007_Vault.Version, Migration007_Vault.Sql),
    ];

    private readonly ISqliteConnectionFactory _factory;
    private readonly AppPaths _paths;
    private readonly IAppLogger? _logger;
    private readonly TimeProvider _time;

    public DatabaseInitializer(
        ISqliteConnectionFactory factory,
        AppPaths paths,
        IAppLogger? logger = null,
        TimeProvider? time = null)
    {
        _factory = factory;
        _paths = paths;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public static int LatestVersion => Migrations[^1].Version;

    public Task<DatabaseStartupResult> InitializeAsync(CancellationToken ct = default)
    {
        _paths.EnsureCreated();

        try
        {
            return Task.FromResult(Migrate());
        }
        catch (SqliteException ex)
        {
            var preserved = PreserveCorrupted(ex);

            try
            {
                var result = Migrate() with { RecoveredFromCorruptionPath = preserved };

                if (preserved is not null)
                {
                    _logger?.DatabaseCorruptionRecovered(preserved);
                }

                return Task.FromResult(result);
            }
            catch (SqliteException ex2)
            {
                _logger?.TechnicalError("migrar la base tras intentar preservar la corrupta", ex2);
                return Task.FromResult(new DatabaseStartupResult(false, 0, 0));
            }
        }
    }

    private DatabaseStartupResult Migrate()
    {
        using var connection = _factory.Create();
        var from = GetUserVersion(connection);

        // Sin esto, el Where de abajo no devuelve nada y la aplicacion vieja abre la base nueva
        // como si nada: no ve las tablas del vault, cree que ninguna conexion tiene credencial y
        // ofrece guardarlas de nuevo en el Administrador de credenciales.
        if (from > LatestVersion)
        {
            throw new InvalidOperationException(
                $"La base es de una versión más nueva que esta aplicación: la base está en "
                + $"{from} y esta versión conoce hasta {LatestVersion}. Actualizá CMC antes de "
                + "abrirla, o vas a perder de vista las credenciales que ya tenés guardadas.");
        }

        var applied = false;

        foreach (var (version, sql) in Migrations.Where(m => m.Version > from))
        {
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $"PRAGMA user_version = {version};";
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            applied = true;
        }

        var to = GetUserVersion(connection);

        if (applied)
        {
            _logger?.DatabaseMigrated(from, to);
        }

        return new DatabaseStartupResult(applied, from, to);
    }

    /// <summary>Aparta la base ilegible sin destruirla. Devuelve <c>null</c> si no se movió nada, para no informar una ruta que no existe (FR-052).</summary>
    private string? PreserveCorrupted(Exception cause)
    {
        var preserved = _paths.CorruptedDatabasePath(_time.GetUtcNow());

        if (!File.Exists(_paths.DatabasePath))
        {
            return null;
        }

        SqliteConnection.ClearAllPools();

        for (var intento = 0; intento < 3; intento++)
        {
            try
            {
                File.Move(_paths.DatabasePath, preserved, overwrite: false);
                return preserved;
            }
            catch (IOException) when (intento < 2)
            {
                Thread.Sleep(50);
            }
            catch (IOException ex)
            {
                _logger?.TechnicalError("preservar la base corrupta", ex);
                _logger?.TechnicalError("causa original", cause);
                return null;
            }
        }

        return null;
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }
}
