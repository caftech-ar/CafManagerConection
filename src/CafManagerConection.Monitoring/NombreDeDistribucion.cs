using System.Text.RegularExpressions;

namespace CafManagerConection.Monitoring;

/// <summary>Acorta el nombre de la distribución para que entre en la franja.</summary>
public static partial class NombreDeDistribucion
{
    /// <summary>Deja nombre y versión: «Ubuntu 22.04.3 LTS» queda «Ubuntu 22.04».</summary>
    /// <param name="completo">El <c>PRETTY_NAME</c> que informa el servidor.</param>
    public static string Abreviar(string completo)
    {
        if (string.IsNullOrWhiteSpace(completo))
        {
            return string.Empty;
        }

        var limpio = completo.Trim();
        var version = Version().Match(limpio);
        var nombre = Nombre(limpio);

        if (!version.Success)
        {
            return nombre;
        }

        var numero = version.Value;
        var punto = numero.IndexOf('.', StringComparison.Ordinal);

        // Dos tramos alcanzan: «22.04.3» queda «22.04» y «8.9» no se toca.
        if (punto >= 0 && numero.IndexOf('.', punto + 1) is var segundo && segundo > 0)
        {
            numero = numero[..segundo];
        }

        return $"{nombre} {numero}".Trim();
    }

    /// <summary>La primera palabra significativa, sin las coletillas del nombre largo.</summary>
    private static string Nombre(string completo)
    {
        var palabras = completo.Split(
            [' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var palabra in palabras)
        {
            if (!EsRelleno(palabra) && !char.IsAsciiDigit(palabra[0]))
            {
                return palabra;
            }
        }

        return palabras.Length > 0 ? palabras[0] : completo;
    }

    private static bool EsRelleno(string palabra) => palabra.ToLowerInvariant()
        is "gnu" or "linux" or "server" or "os" or "release" or "lts";

    [GeneratedRegex(@"\d+(\.\d+)*")]
    private static partial Regex Version();
}
