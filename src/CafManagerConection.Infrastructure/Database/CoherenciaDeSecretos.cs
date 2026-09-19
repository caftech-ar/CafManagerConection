using Dapper;

namespace CafManagerConection.Infrastructure.Database;

/// <summary>Qué encontró la comprobación: <c>Coherente</c> en falso trae el motivo listo para mostrar.</summary>
public sealed record EstadoDeLosSecretos(bool Coherente, string? Motivo)
{
    public static EstadoDeLosSecretos Ok { get; } = new(true, null);
}

/// <summary>Comprueba que el estado de cifrado de los secretos coincida con que haya clave maestra o no.</summary>
public static class CoherenciaDeSecretos
{
    // Un secreto guardado se reconoce cifrado por tener nonce, no por una marca propia. El
    // invariante cruza dos tablas, asi que ningun CHECK de SQLite puede sostenerlo.
    private const string CuentaPorEstado = """
        SELECT
            (SELECT COUNT(*) FROM vault) AS con_clave_maestra,
            (SELECT COUNT(*) FROM connections
              WHERE secreto IS NOT NULL AND secreto_nonce IS NULL)
          + (SELECT COUNT(*) FROM folder_settings
              WHERE (rdp_secreto IS NOT NULL AND rdp_secreto_nonce IS NULL)
                 OR (ssh_secreto IS NOT NULL AND ssh_secreto_nonce IS NULL)
                 OR (web_secreto IS NOT NULL AND web_secreto_nonce IS NULL)) AS en_claro,
            (SELECT COUNT(*) FROM connections WHERE secreto_nonce IS NOT NULL)
          + (SELECT COUNT(*) FROM folder_settings
              WHERE rdp_secreto_nonce IS NOT NULL
                 OR ssh_secreto_nonce IS NOT NULL
                 OR web_secreto_nonce IS NOT NULL) AS cifrados;
        """;

    /// <summary>Recorre los secretos guardados y devuelve si el modo de cifrado es uniforme.</summary>
    public static EstadoDeLosSecretos Comprobar(ISqliteConnectionFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var db = factory.Create();
        var cuenta = db.QuerySingle<Cuenta>(CuentaPorEstado);

        if (cuenta.ConClaveMaestra == 0 && cuenta.Cifrados > 0)
        {
            return new EstadoDeLosSecretos(
                false,
                $"Hay {cuenta.Cifrados} contraseña(s) cifradas pero no hay clave maestra "
                + "configurada: no se van a poder leer.");
        }

        if (cuenta.ConClaveMaestra > 0 && cuenta.EnClaro > 0)
        {
            return new EstadoDeLosSecretos(
                false,
                $"Hay {cuenta.EnClaro} contraseña(s) guardadas sin cifrar aunque hay clave "
                + "maestra configurada.");
        }

        return EstadoDeLosSecretos.Ok;
    }

    private sealed class Cuenta
    {
        public int ConClaveMaestra { get; init; }

        public int EnClaro { get; init; }

        public int Cifrados { get; init; }
    }
}
