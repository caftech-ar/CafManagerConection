using Dapper;

namespace CafManagerConection.Infrastructure.Database;

/// <summary>Empareja las columnas <c>con_guion_bajo</c> con las propiedades <c>PascalCase</c> de los DTOs.</summary>
public static class MapeoDeDapper
{
    /// <summary>Deja el mapeo listo. Es idempotente y lo llama la fábrica de conexiones, que es por donde pasan todos los repositorios.</summary>
    public static void Configurar() => DefaultTypeMap.MatchNamesWithUnderscores = true;
}
