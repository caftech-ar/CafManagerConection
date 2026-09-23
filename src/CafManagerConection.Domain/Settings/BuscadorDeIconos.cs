using System.Globalization;
using System.Text;

namespace CafManagerConection.Domain.Settings;

/// <summary>Encuentra iconos del catálogo por lo que el usuario escribe y por los filtros que elige.</summary>
public static class BuscadorDeIconos
{
    /// <summary>Los iconos que cumplen todos los criterios, en el orden del catálogo.</summary>
    /// <param name="texto">Lo escrito en el buscador; nulo o vacío no filtra.</param>
    /// <param name="grupo">Grupo al que acotar, o null para todos.</param>
    /// <param name="familia">Conceptos o logos, o null para las dos.</param>
    /// <param name="soloDelSelector">Deja afuera los grupos con los que se dibuja la propia interfaz.</param>
    public static IReadOnlyList<IconoDelCatalogo> Filtrar(
        string? texto = null,
        GrupoDeIconos? grupo = null,
        FamiliaDeIcono? familia = null,
        bool soloDelSelector = true)
    {
        var buscado = Normalizar(texto);

        return CatalogoDeIconos.Iconos
            .Where(icono => !soloDelSelector || icono.EnElSelector)
            .Where(icono => grupo is null || icono.Grupo == grupo)
            .Where(icono => familia is null || icono.Grupo.Familia == familia)
            .Where(icono => buscado.Length == 0 || Coincide(icono, buscado))
            .ToList();
    }

    /// <summary>Minúsculas y sin acentos: así se compara lo que se escribe contra lo que dice el catálogo.</summary>
    /// <param name="texto">Texto a normalizar.</param>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sinAcentos = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sinAcentos.Append(caracter);
            }
        }

        return sinAcentos.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool Coincide(IconoDelCatalogo icono, string buscado) =>
        Normalizar(icono.Clave).Contains(buscado, StringComparison.Ordinal)
        || Normalizar(icono.Etiqueta).Contains(buscado, StringComparison.Ordinal)
        || icono.Sinonimos.Any(sinonimo => Normalizar(sinonimo).Contains(buscado, StringComparison.Ordinal));
}
