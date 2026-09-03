using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Database;

public interface ISqliteConnectionFactory
{
    SqliteConnection Create();

    string DatabasePath { get; }
}

// Los PRAGMA se aplican en cada conexión: foreign_keys viene apagado por omisión en SQLite y es por conexión.
public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    public SqliteConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public SqliteConnection Create()
    {
        // Sin caché compartida: mantiene abierto el archivo aunque se cierre la conexión, y eso impide apartar una base corrupta para preservarla.
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString());

        try
        {
            connection.Open();

            // SQLite abre de forma perezosa: con un archivo que no es una base, Open() pasa sin error y el fallo recién aparece en el primer comando.
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
            pragma.ExecuteNonQuery();

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
