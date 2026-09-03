using System.Text.Json;

namespace CafManagerConection.Infrastructure.Database;

internal static class Serializacion
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = false,
    };

    public static string? EtiquetasATexto(IReadOnlyList<string> etiquetas) =>
        etiquetas.Count == 0 ? null : string.Join(",", etiquetas);

    public static IEnumerable<string> TextoAEtiquetas(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string? CamposATexto(IReadOnlyDictionary<string, string> campos) =>
        campos.Count == 0 ? null : JsonSerializer.Serialize(campos, Opciones);

    /// <summary>Lee los campos propios. Un JSON ilegible devuelve vacío en lugar de lanzar: un valor corrupto no puede sacar la conexión del árbol.</summary>
    public static Dictionary<string, string> TextoACampos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(texto, Opciones) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
