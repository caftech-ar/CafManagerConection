using System.Reflection;

namespace CafManagerConection.App.Services;

/// <summary>La versión que se muestra. Sale de <c>InformationalVersion</c> y no de <c>AssemblyVersion</c>: el primero tiene tres partes, igual que la etiqueta de la release, y el segundo cuatro.</summary>
public static class VersionDeLaAplicacion
{
    private static readonly Lazy<string> Texto = new(Leer);

    public static string Corta => Texto.Value;

    /// <summary>Recorta el <c>+hash</c> que agrega el SDK cuando compila desde un repositorio git.</summary>
    public static string Limpiar(string? informacional, string? deEnsamblado)
    {
        if (!string.IsNullOrWhiteSpace(informacional))
        {
            var mas = informacional.IndexOf('+', StringComparison.Ordinal);
            var limpio = mas < 0 ? informacional : informacional[..mas];

            if (limpio.Length > 0)
            {
                return limpio;
            }
        }

        return string.IsNullOrWhiteSpace(deEnsamblado) ? "desconocida" : deEnsamblado;
    }

    private static string Leer()
    {
        var ensamblado = typeof(VersionDeLaAplicacion).Assembly;

        return Limpiar(
            ensamblado.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion,
            ensamblado.GetName().Version?.ToString());
    }
}
