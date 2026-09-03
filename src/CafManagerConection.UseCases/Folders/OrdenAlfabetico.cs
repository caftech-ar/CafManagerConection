using System.Globalization;

namespace CafManagerConection.UseCases.Folders;

/// <summary>Dónde entra un nombre nuevo entre sus hermanos, y en qué orden van todos (FR-193a, FR-193c).</summary>
public static class OrdenAlfabetico
{
    /// <summary>Compara como se lee: «Álvarez» antes de «Bravo», y «zeta» y «Zeta» en el mismo lugar.</summary>
    public static StringComparer Comparador { get; } =
        StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true);

    public static int Posicion(IReadOnlyList<string> hermanos, string nuevo)
    {
        for (var i = 0; i < hermanos.Count; i++)
        {
            if (Comparador.Compare(hermanos[i], nuevo) > 0)
            {
                return i;
            }
        }

        return hermanos.Count;
    }

    public static List<Guid> Ordenar<T>(
        IEnumerable<T> elementos, Func<T, string> nombre, Func<T, Guid> id) =>
        [.. elementos.OrderBy(nombre, Comparador).Select(id)];
}
