using System.Linq;
using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.Infrastructure.Database.Migrations;
using CafManagerConection.UseCases.Abstractions;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Database;

/// <summary>Abre la base, o la crea entera en la <c>user_version</c> 1. Lanza <see cref="InvalidOperationException"/> con cualquier otra versión, sin escribir nada.</summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
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

    public static int LatestVersion => Migration003_UsuarioYPuertoPorProtocolo.Version;

    // En orden de versión: una base en 0 las corre todas; una en 1 corre de la 2 en adelante. La 1
    // es el esquema entero, la 2 en adelante son cambios incrementales sobre él.
    private static readonly (int Version, string Sql)[] Migraciones =
    [
        (Migration001_Esquema.Version, Migration001_Esquema.Sql),
        (Migration002_CamposDeCarpeta.Version, Migration002_CamposDeCarpeta.Sql),
        (Migration003_UsuarioYPuertoPorProtocolo.Version,
            Migration003_UsuarioYPuertoPorProtocolo.Sql),
    ];

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

        // Una version que no sea 0 y que ninguna migracion conozca queda afuera: la numeracion
        // vieja llegaba a 8, asi que un numero desconocido tanto puede ser una base vieja como una
        // de una version futura. Sin esto, una base vieja se abriria sin sus tablas y la
        // aplicacion creeria que ninguna conexion tiene credencial.
        if (from is not 0 && from != LatestVersion && Array.TrueForAll(Migraciones, m => m.Version != from))
        {
            throw new InvalidOperationException(
                $"Esta base declara el esquema {from} y esta versión sólo abre hasta el "
                + $"{LatestVersion}. Si viene de una versión anterior de CMC, ya no se puede "
                + "abrir ni convertir; si viene de una más nueva, actualizá CMC. La base no se "
                + "tocó: sigue donde estaba.");
        }

        var pendientes = Migraciones.Where(m => m.Version > from).OrderBy(m => m.Version).ToArray();
        var applied = pendientes.Length > 0;

        if (applied)
        {
            using var tx = connection.BeginTransaction();

            foreach (var migracion in pendientes)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = migracion.Sql;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $"PRAGMA user_version = {LatestVersion};";
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        var to = GetUserVersion(connection);

        if (applied)
        {
            _logger?.DatabaseMigrated(from, to);
        }

        return new DatabaseStartupResult(applied, from, to);
    }

    /// <summary>Aparta la base ilegible sin destruirla. Devuelve <c>null</c> si no se movió nada, para no informar una ruta que no existe.</summary>
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
