namespace CafManagerConection.Domain.Connections;

public static class EtiquetasDeFabrica
{
    // Los mismos identificadores que siembra Migration003_EtiquetasConfigurables.cs.
    private static readonly (string Id, string Codigo, string Nombre, string ClaveDeColor)[] Definicion =
    [
        ("11111111-0000-4000-8000-000000000001", "PRD", "Producción", "rojo"),
        ("11111111-0000-4000-8000-000000000002", "PRE", "PreProducción", "ambar"),
        ("11111111-0000-4000-8000-000000000005", "QA", "Quality Assurance", "violeta"),
        ("11111111-0000-4000-8000-000000000003", "CAPA", "Capacitación", "cyan"),
        ("11111111-0000-4000-8000-000000000004", "DESA", "Desarrollo", "verde"),
    ];

    public static int Cantidad => Definicion.Length;

    public static IReadOnlyList<Guid> Identificadores { get; } =
        [.. Definicion.Select(d => Guid.Parse(d.Id))];

    public static bool EsDeFabrica(Guid id) => Identificadores.Contains(id);

    // Instancias nuevas por llamada: Etiqueta es mutable y un Renombrar() ajeno corrompía el catálogo.
    public static IReadOnlyList<Etiqueta> Crear() =>
        [.. Definicion.Select((d, i) =>
            new Etiqueta(Guid.Parse(d.Id), d.Codigo, d.Nombre, d.ClaveDeColor, i + 1))];
}

public sealed record CambiosAlRestablecer(
    IReadOnlyList<Etiqueta> Faltantes,
    IReadOnlyList<Etiqueta> Modificadas,
    IReadOnlyList<Etiqueta> Agregadas,
    int ConexionesQuePierdenEtiqueta)
{
    public bool HayAlgoQueHacer =>
        Faltantes.Count > 0 || Modificadas.Count > 0 || Agregadas.Count > 0;

    public bool BorraAlgo => Agregadas.Count > 0;
}

public static class RestablecerEtiquetas
{
    public static CambiosAlRestablecer Comparar(
        IReadOnlyList<Etiqueta> actuales,
        Func<Guid, int> conexionesQueUsan)
    {
        var fabrica = EtiquetasDeFabrica.Crear();

        var faltantes = fabrica
            .Where(f => actuales.All(a => a.Id != f.Id))
            .ToList();

        var modificadas = fabrica
            .Where(f => actuales.FirstOrDefault(a => a.Id == f.Id) is { } actual
                        && !CoincideConFabrica(actual, f))
            .ToList();

        var agregadas = actuales
            .Where(a => !EtiquetasDeFabrica.EsDeFabrica(a.Id))
            .ToList();

        return new CambiosAlRestablecer(
            faltantes,
            modificadas,
            agregadas,
            agregadas.Sum(a => conexionesQueUsan(a.Id)));
    }

    private static bool CoincideConFabrica(Etiqueta actual, Etiqueta fabrica) =>
        string.Equals(actual.Codigo, fabrica.Codigo, StringComparison.Ordinal)
        && string.Equals(actual.Nombre, fabrica.Nombre, StringComparison.Ordinal)
        && string.Equals(actual.ClaveDeColor, fabrica.ClaveDeColor, StringComparison.Ordinal)
        && actual.Orden == fabrica.Orden;
}
