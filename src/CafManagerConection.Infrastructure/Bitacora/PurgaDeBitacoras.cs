using CafManagerConection.Domain.Settings;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Bitacora;

/// <summary>Borra las bitácoras que pasaron el plazo de retención.</summary>
public static class PurgaDeBitacoras
{
    /// <summary>Borra lo vencido de la carpeta y devuelve cuántos archivos se fueron.</summary>
    /// <param name="carpeta">Dónde se guardan las bitácoras.</param>
    /// <param name="dias">Días que se guardan; cero no borra nada.</param>
    /// <param name="ahora">Momento de referencia.</param>
    /// <param name="logger">Para dejar el motivo de un fallo.</param>
    public static int Purgar(
        string carpeta, int dias, DateTimeOffset ahora, IAppLogger? logger = null)
    {
        if (dias <= 0 || string.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta))
        {
            return 0;
        }

        var borrados = 0;

        foreach (var archivo in Archivos(carpeta, logger))
        {
            try
            {
                if (!PoliticaDeBitacora.EsBitacora(Path.GetFileName(archivo))
                    || !PoliticaDeBitacora.HayQueBorrar(
                        File.GetLastWriteTimeUtc(archivo), ahora, dias))
                {
                    continue;
                }

                File.Delete(archivo);
                borrados++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger?.TechnicalError("borrar una bitácora vencida", ex);
            }
        }

        return borrados;
    }

    private static IEnumerable<string> Archivos(string carpeta, IAppLogger? logger)
    {
        try
        {
            return Directory.EnumerateFiles(carpeta, $"*{PoliticaDeBitacora.Extension}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.TechnicalError("listar las bitácoras para purgarlas", ex);
            return [];
        }
    }
}
