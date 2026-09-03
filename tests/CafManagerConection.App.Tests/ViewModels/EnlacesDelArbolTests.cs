using System.Reflection;
using System.Text.RegularExpressions;
using CafManagerConection.App.ViewModels;

namespace CafManagerConection.App.Tests.ViewModels;

// Renombrar una propiedad enlazada no rompe la compilación: WPF resuelve el enlace en tiempo de
// ejecución y una propiedad inexistente sólo deja la celda vacía. Pasó con NodoArbol.Detalle.
public sealed class EnlacesDelArbolTests
{
    private static readonly Regex Enlace = new(@"\{Binding\s+([A-Za-z_][A-Za-z0-9_]*)[,}\s]");

    [Fact]
    public void Toda_propiedad_enlazada_en_la_plantilla_del_arbol_existe_en_NodoArbol()
    {
        var propiedades = typeof(NodoArbol)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var faltantes = Pedidas()
            .Where(n => !propiedades.Contains(n))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            faltantes.Count == 0,
            $"La plantilla del árbol enlaza propiedades que NodoArbol no tiene: "
            + string.Join(", ", faltantes));
    }

    private static IEnumerable<string> Pedidas()
    {
        var xaml = File.ReadAllText(Path.Combine(RaizDelRepositorio(),
            "src", "CafManagerConection.App", "Views", "MainWindow.xaml"));

        var abre = xaml.IndexOf("<HierarchicalDataTemplate", StringComparison.Ordinal);
        var cierra = xaml.IndexOf("</HierarchicalDataTemplate>", StringComparison.Ordinal);

        Assert.True(abre >= 0 && cierra > abre, "No se encontró la plantilla del árbol.");

        return Enlace.Matches(xaml[abre..cierra]).Select(m => m.Groups[1].Value);
    }

    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (Directory.Exists(Path.Combine(directorio.FullName, "src"))
                && Directory.Exists(Path.Combine(directorio.FullName, "tests")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException(
            $"No se encontró la raíz del repositorio subiendo desde {AppContext.BaseDirectory}.");
    }
}
