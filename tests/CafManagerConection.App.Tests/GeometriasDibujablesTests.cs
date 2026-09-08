using System.Text.RegularExpressions;
using System.Windows.Media;

namespace CafManagerConection.App.Tests;

// Un trazado inválido no rompe la compilación: tira `XamlParseException` al cargar el XAML y el
// panel entero no abre. Pasó con `IconoEliminarArchivo` de `Panels/FilesPanel.xaml`, partido en
// varias líneas: el salto separó `.56` en `.5` y `6`, dos números donde iba uno.
public sealed class GeometriasDibujablesTests
{
    private static readonly Regex Declaradas = new(
        @"<(StreamGeometry|PathGeometry)\s+x:Key=""(?<clave>[^""]+)""\s*>(?<trazado>[^<]*)</\1>",
        RegexOptions.Singleline);

    private static readonly Regex EnAtributo = new(@"\sData=""(?<trazado>[MmLlHhVvCcSsQqTtAaZzFf][^""]*)""");

    public static TheoryData<string> Archivos()
    {
        var datos = new TheoryData<string>();

        foreach (var archivo in Xaml())
        {
            datos.Add(Path.GetRelativePath(RaizDelRepositorio(), archivo));
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Todo_trazado_declarado_en_XAML_lo_puede_dibujar_WPF(string relativa)
    {
        var texto = File.ReadAllText(Path.Combine(RaizDelRepositorio(), relativa));
        var fallas = new List<string>();

        foreach (var (clave, trazado) in Trazados(texto))
        {
            try
            {
                Geometry.Parse(trazado);
            }
            catch (FormatException ex)
            {
                fallas.Add($"{clave}: {ex.Message}");
            }
        }

        Assert.True(
            fallas.Count == 0,
            $"{relativa} declara trazados que WPF no puede dibujar, así que el archivo no carga y "
            + "la pantalla que lo usa no abre:" + Environment.NewLine
            + string.Join(Environment.NewLine, fallas));
    }

    [Fact]
    public void El_guardian_alcanza_a_los_cuarenta_trazados_del_proyecto()
    {
        var cuantos = Xaml().Sum(a => Trazados(File.ReadAllText(a)).Count);

        Assert.True(cuantos >= 40, $"Sólo se encontraron {cuantos} trazados; el patrón dejó de reconocerlos.");
    }

    private static List<(string Clave, string Trazado)> Trazados(string xaml)
    {
        var encontrados = Declaradas.Matches(xaml)
            .Select(m => (m.Groups["clave"].Value, m.Groups["trazado"].Value))
            .ToList();

        encontrados.AddRange(EnAtributo.Matches(xaml)
            .Select(m => ("Data", m.Groups["trazado"].Value)));

        return encontrados;
    }

    private static IEnumerable<string> Xaml() =>
        Directory.EnumerateFiles(
                Path.Combine(RaizDelRepositorio(), "src", "CafManagerConection.App"),
                "*.xaml",
                SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                        && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));

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
