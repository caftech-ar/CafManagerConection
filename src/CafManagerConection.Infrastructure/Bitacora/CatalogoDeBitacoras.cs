using CafManagerConection.Domain.Settings;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Bitacora;

/// <summary>Una bitácora escrita, tal como se la lista.</summary>
public sealed record BitacoraEnDisco(
    string Ruta, string Host, string Conexion, DateTimeOffset Inicio, long Bytes);

/// <summary>Lo que hay escrito en la carpeta de bitácoras. La fuente es el sistema de archivos: el nombre ya lleva host, conexión y momento.</summary>
public static class CatalogoDeBitacoras
{
    /// <summary>Lista las bitácoras de la carpeta, de la más reciente a la más vieja.</summary>
    /// <param name="carpeta">Dónde se guardan.</param>
    /// <param name="logger">Para dejar el motivo de un fallo.</param>
    public static IReadOnlyList<BitacoraEnDisco> Listar(string carpeta, IAppLogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta))
        {
            return [];
        }

        var encontradas = new List<BitacoraEnDisco>();

        try
        {
            foreach (var ruta in Directory.EnumerateFiles(carpeta, $"*{PoliticaDeBitacora.Extension}"))
            {
                if (PoliticaDeBitacora.Leer(Path.GetFileName(ruta)) is not { } datos)
                {
                    continue;
                }

                encontradas.Add(new BitacoraEnDisco(
                    ruta, datos.Host, datos.Conexion, datos.Inicio, Tamano(ruta)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.TechnicalError("listar las bitácoras de sesión", ex);
        }

        return [.. encontradas.OrderByDescending(b => b.Inicio)];
    }

    private static long Tamano(string ruta)
    {
        try
        {
            return new FileInfo(ruta).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
