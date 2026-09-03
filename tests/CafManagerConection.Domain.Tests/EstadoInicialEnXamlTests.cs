using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// `IsChecked="True"` en el XAML dispara el manejador de `Checked` durante `InitializeComponent()`,
// cuando los campos con `x:Name` todavía son null. Pasó con `_porCpu` de `Panels/ProcesosPanel.xaml`:
// el manejador desreferenciaba `_arbol` y el panel no abría. No rompe la compilación.
public sealed class EstadoInicialEnXamlTests
{
    private static readonly Regex Elemento = new(
        @"<(RadioButton|ToggleButton|CheckBox)\b(?<atributos>[^>]*?)/?>",
        RegexOptions.Singleline);

    private static readonly Regex Marcado = new(@"IsChecked\s*=\s*""(True|true)""");

    private static readonly Regex Manejador = new(@"\b(Checked|Unchecked)\s*=\s*""");

    [Fact]
    public void Ningun_control_marcado_en_el_XAML_engancha_su_manejador_de_Checked()
    {
        var culpables = new List<string>();

        foreach (var archivo in Xaml())
        {
            var texto = File.ReadAllText(archivo);

            foreach (var m in Elemento.Matches(texto).Cast<Match>())
            {
                var atributos = m.Groups["atributos"].Value;

                if (!Marcado.IsMatch(atributos) || !Manejador.IsMatch(atributos))
                {
                    continue;
                }

                var linea = texto[..m.Index].Count(c => c == '\n') + 1;
                var nombre = Regex.Match(atributos, @"x:Name=""([^""]+)""");

                culpables.Add(
                    $"  {Path.GetFileName(archivo)}:{linea} "
                    + (nombre.Success ? nombre.Groups[1].Value : "(sin nombre)"));
            }
        }

        Assert.True(
            culpables.Count == 0,
            "Estos controles se marcan en el XAML y además engachan Checked o Unchecked, así que el "
            + "manejador corre durante InitializeComponent(), cuando los campos con x:Name todavía "
            + "son null. Marcalos por código después de InitializeComponent():"
            + Environment.NewLine
            + string.Join(Environment.NewLine, culpables));
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
