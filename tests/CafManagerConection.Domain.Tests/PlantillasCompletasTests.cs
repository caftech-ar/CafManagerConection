using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// Una plantilla propia que se come una parte obligatoria no rompe la compilación ni tira ninguna
// excepción: el control se dibuja y la parte que falta simplemente no existe. `MenuItem` estaba sin
// `Popup` ni forma de alojar sus hijos en `Themes/Estilos.xaml`, así que la fila del submenú se
// dibujaba y no se desplegaba nunca.
public sealed class PlantillasCompletasTests
{
    // `<ControlTemplate.Triggers>` no abre una plantilla anidada: se exige espacio o `>` detrás.
    private static readonly Regex Abre = new(@"<ControlTemplate(?=[\s>])");

    private static readonly Regex Cierra = new(@"</ControlTemplate>");

    private static readonly string[] AlojanSusHijos =
        ["MenuItem", "ComboBox", "TabControl", "TreeViewItem", "ListBox", "ContextMenu", "Menu"];

    /// <summary>Los que muestran sus hijos en un desplegable, no en su propia superficie.</summary>
    private static readonly string[] Desplegables = ["MenuItem", "ComboBox"];

    [Fact]
    public void Toda_plantilla_de_un_control_con_hijos_sabe_donde_ponerlos()
    {
        var faltantes = new List<string>();

        foreach (var (archivo, control, plantilla) in Plantillas(AlojanSusHijos))
        {
            var aloja = plantilla.Contains("ItemsPresenter", StringComparison.Ordinal)
                        || plantilla.Contains("IsItemsHost=\"True\"", StringComparison.Ordinal);

            if (!aloja)
            {
                faltantes.Add(
                    $"  {Path.GetFileName(archivo)}: la plantilla de {control} no tiene "
                    + "ItemsPresenter ni un panel con IsItemsHost");
            }
        }

        Assert.True(faltantes.Count == 0, Explicacion(faltantes));
    }

    [Fact]
    public void Toda_plantilla_de_un_control_desplegable_tiene_su_Popup()
    {
        var faltantes = Plantillas(Desplegables)
            .Where(p => !p.Plantilla.Contains("<Popup", StringComparison.Ordinal))
            .Select(p => $"  {Path.GetFileName(p.Archivo)}: la plantilla de {p.Control} no tiene Popup")
            .ToList();

        Assert.True(faltantes.Count == 0, Explicacion(faltantes));
    }

    [Fact]
    public void El_guardian_encuentra_las_plantillas_que_el_proyecto_declara()
    {
        var cuantas = Plantillas(AlojanSusHijos).Count();

        Assert.True(cuantas >= 4, $"Sólo se encontraron {cuantas} plantillas; el patrón dejó de reconocerlas.");
    }

    private static string Explicacion(List<string> faltantes) =>
        "Estas plantillas se comen una parte que el control necesita. No falla la compilación ni "
        + "tira excepción: la parte simplemente no existe en tiempo de ejecución."
        + Environment.NewLine
        + string.Join(Environment.NewLine, faltantes);

    private static IEnumerable<(string Archivo, string Control, string Plantilla)> Plantillas(
        string[] controles)
    {
        foreach (var archivo in Xaml())
        {
            var xaml = File.ReadAllText(archivo);

            foreach (var control in controles)
            {
                var declara = new Regex($"<ControlTemplate\\s+TargetType=\"{control}\"\\s*>");

                foreach (var m in declara.Matches(xaml).Cast<Match>())
                {
                    yield return (archivo, control, Cuerpo(xaml, m.Index + m.Length));
                }
            }
        }
    }

    /// <summary>Hasta el cierre que corresponde, contando las plantillas anidadas.</summary>
    private static string Cuerpo(string xaml, int desde)
    {
        var i = desde;
        var nivel = 1;

        while (nivel > 0)
        {
            var cierra = Cierra.Match(xaml, i);

            if (!cierra.Success)
            {
                break;
            }

            var abre = Abre.Match(xaml, i);

            if (abre.Success && abre.Index < cierra.Index)
            {
                nivel++;
                i = abre.Index + abre.Length;
                continue;
            }

            nivel--;
            i = cierra.Index + cierra.Length;
        }

        return xaml[desde..i];
    }

    private static IEnumerable<string> Xaml() =>
        Directory.EnumerateFiles(
                Path.Combine(Repositorio.Raiz(), "src", "CafManagerConection.App"),
                "*.xaml",
                SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                        && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
}
