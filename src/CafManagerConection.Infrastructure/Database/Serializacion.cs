using System.Text.Json;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Database;

internal static class Serializacion
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = false,
    };

    public static string? CamposATexto(IReadOnlyDictionary<string, string> campos) =>
        campos.Count == 0 ? null : JsonSerializer.Serialize(campos, Opciones);

    /// <summary>Lee los campos propios. Un JSON ilegible se registra y devuelve vacío en lugar de lanzar: un valor corrupto no puede sacar la conexión del árbol.</summary>
    /// <param name="texto">El JSON guardado, o <c>null</c>.</param>
    /// <param name="fila">Identificador de la fila, para que el registro diga cuál es.</param>
    /// <param name="logger">Dónde anotar que el texto no se pudo leer.</param>
    public static Dictionary<string, string> TextoACampos(
        string? texto, string? fila = null, IAppLogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return Vacio();
        }

        try
        {
            var crudo = JsonSerializer.Deserialize<Dictionary<string, string>>(texto, Opciones);

            return crudo is null
                ? Vacio()
                : new Dictionary<string, string>(crudo, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            logger?.TechnicalError($"leer los campos propios de la fila {fila ?? "(sin id)"}", ex);
            return Vacio();
        }
    }

    /// <summary>Los campos propios comparan claves sin distinguir mayúsculas, igual que la conexión.</summary>
    private static Dictionary<string, string> Vacio() => new(StringComparer.OrdinalIgnoreCase);
}
