using System.Text;

namespace CafManagerConection.Infrastructure.Bitacora;

/// <summary>Lee una bitácora desde el final, de a partes: una sesión larga puede pesar cientos de megas.</summary>
public static class LectorDeBitacora
{
    /// <summary>Cuánto se trae en cada lectura.</summary>
    public const int BytesPorParte = 256 * 1024;

    /// <summary>Lee el tramo que termina en <paramref name="hasta"/>, sin cortar una línea por la mitad.</summary>
    /// <param name="ruta">Archivo a leer.</param>
    /// <param name="hasta">Posición final del tramo; el largo del archivo para leer el final.</param>
    /// <param name="bytes">Cuánto traer como máximo.</param>
    /// <param name="ct">Para cortar la lectura desde afuera.</param>
    /// <returns>El texto y desde qué posición quedó leído, para pedir lo anterior.</returns>
    public static async Task<(string Texto, long Desde)> LeerHaciaAtrasAsync(
        string ruta, long hasta, int bytes = BytesPorParte, CancellationToken ct = default)
    {
        await using var archivo = new FileStream(
            ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var fin = Math.Clamp(hasta, 0, archivo.Length);
        var desde = Math.Max(0, fin - bytes);
        var largo = (int)(fin - desde);

        if (largo <= 0)
        {
            return (string.Empty, 0);
        }

        archivo.Seek(desde, SeekOrigin.Begin);

        var buffer = new byte[largo];
        await archivo.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);

        var texto = new UTF8Encoding(false).GetString(buffer);

        // Salvo que se haya llegado al principio, el tramo arranca con media línea: se descarta.
        if (desde > 0)
        {
            var salto = texto.IndexOf('\n', StringComparison.Ordinal);

            if (salto >= 0)
            {
                desde += Encoding.UTF8.GetByteCount(texto[..(salto + 1)]);
                texto = texto[(salto + 1)..];
            }
        }

        return (texto, desde);
    }

    /// <summary>Cuánto pesa el archivo, o cero si no se puede saber.</summary>
    /// <param name="ruta">Archivo a medir.</param>
    public static long Largo(string ruta)
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
