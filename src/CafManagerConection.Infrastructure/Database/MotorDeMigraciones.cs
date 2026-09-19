using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Database;

/// <summary>Aplica migraciones sobre una conexión abierta, en una sola transacción y sin que recrear una tabla se lleve sus filas hijas.</summary>
public static class MotorDeMigraciones
{
    /// <summary>Corre las migraciones en orden y deja la base en la versión indicada, o no cambia nada.</summary>
    /// <param name="connection">Conexión abierta y fuera de toda transacción.</param>
    /// <param name="migraciones">Las sentencias a correr, en el orden en que se aplican.</param>
    /// <param name="versionFinal">Lo que queda en <c>user_version</c> al confirmar.</param>
    /// <exception cref="InvalidOperationException">Una migración dejó filas sin su fila padre.</exception>
    public static void Aplicar(
        SqliteConnection connection,
        IReadOnlyList<string> migraciones,
        int versionFinal)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(migraciones);

        // Con las claves foraneas activas, el DROP TABLE del procedimiento para recrear una tabla
        // dispara las cascadas y se lleva las filas hijas. El pragma es por conexion y dentro de
        // una transaccion no hace nada, asi que va antes de abrirla.
        Ejecutar(connection, "PRAGMA foreign_keys = OFF;");

        try
        {
            using var tx = connection.BeginTransaction();

            foreach (var sql in migraciones)
            {
                Ejecutar(connection, sql, tx);
            }

            ExigirIntegridadReferencial(connection, tx);
            Ejecutar(connection, $"PRAGMA user_version = {versionFinal};", tx);

            tx.Commit();
        }
        finally
        {
            Ejecutar(connection, "PRAGMA foreign_keys = ON;");
        }
    }

    private static void Ejecutar(
        SqliteConnection connection,
        string sql,
        SqliteTransaction? tx = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Comprueba que ninguna fila quedó apuntando a un padre inexistente.</summary>
    /// <exception cref="InvalidOperationException">Alguna fila quedó huérfana; la transacción se revierte sin confirmar.</exception>
    private static void ExigirIntegridadReferencial(
        SqliteConnection connection,
        SqliteTransaction tx)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "PRAGMA foreign_key_check;";

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            throw new InvalidOperationException(
                $"La migración dejó filas sin su fila padre en «{reader.GetString(0)}». "
                + "La base no se tocó: sigue donde estaba.");
        }
    }
}
