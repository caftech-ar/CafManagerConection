namespace CafManagerConection.Domain.Settings;

/// <summary>Elige y ordena las tipografías que se le ofrecen al usuario, a partir de las que tenga instaladas.</summary>
public static class TipografiasDisponibles
{
    /// <summary>Las monoespaciadas que valen para un terminal, en el orden en que conviene ofrecerlas. Sólo aparecen las que estén instaladas.</summary>
    public static readonly string[] PreferidasParaTerminal =
    [
        "Cascadia Mono",
        "Cascadia Code",
        "Consolas",
        "JetBrains Mono",
        "Fira Code",
        "Source Code Pro",
        "IBM Plex Mono",
        "Hack",
        "DejaVu Sans Mono",
        "Liberation Mono",
        "Courier New",
        "Lucida Console",
    ];

    /// <summary>Las de texto, para el árbol y los paneles.</summary>
    public static readonly string[] PreferidasParaInterfaz =
    [
        "Segoe UI",
        "Segoe UI Variable",
        "Inter",
        "Roboto",
        "Open Sans",
        "Noto Sans",
        "Calibri",
        "Verdana",
        "Tahoma",
        "Arial",
    ];

    /// <summary>Las preferidas que estén instaladas primero, después el resto en orden alfabético. Devolver todas y no sólo las conocidas: el usuario puede tener la que quiere y no estar en ninguna lista.</summary>
    public static IReadOnlyList<string> Ordenar(
        IEnumerable<string> instaladas, IEnumerable<string> preferidas)
    {
        ArgumentNullException.ThrowIfNull(instaladas);
        ArgumentNullException.ThrowIfNull(preferidas);

        var presentes = new HashSet<string>(
            instaladas.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var arriba = preferidas
            .Where(presentes.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var yaEsta = new HashSet<string>(arriba, StringComparer.OrdinalIgnoreCase);

        var resto = presentes
            .Where(f => !yaEsta.Contains(f))
            .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase);

        return [.. arriba, .. resto];
    }

    /// <summary>Filtra por lo que el usuario escribe. Coincide en cualquier parte del nombre: quien busca «mono» quiere ver «Cascadia Mono».</summary>
    public static IReadOnlyList<string> Buscar(IEnumerable<string> tipografias, string? texto)
    {
        ArgumentNullException.ThrowIfNull(tipografias);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return [.. tipografias];
        }

        var buscado = texto.Trim();

        return
        [
            .. tipografias.Where(f =>
                f.Contains(buscado, StringComparison.CurrentCultureIgnoreCase))
        ];
    }
}
