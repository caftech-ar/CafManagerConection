using System.Reflection;
using System.Text.RegularExpressions;
using CafManagerConection.App.Services;
using CafManagerConection.App.Themes;
using CafManagerConection.Platform;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Tests.Themes;

// Antes había 41 geometrías escritas a mano repartidas entre Estilos.xaml, dos diccionarios locales
// y seis constantes en C#. Nadie sabía de dónde había salido cada una ni cómo actualizarla.
public sealed class IconografiaUnificadaTests
{
    private static readonly Regex GeometriaDeclarada = new(
        @"<(?:Stream|Path)Geometry\s+x:Key=""(?<clave>[^""]+)""", RegexOptions.Compiled);

    // Un trazado también se escribe a mano dentro de un atributo Data.
    private static readonly Regex TrazadoEnAtributo = new(
        @"Data\s*=\s*""(?<trazado>[FfMm][\s\d.,-][^""]*)""", RegexOptions.Compiled);

    // `using Forma = System.Windows.Shapes;` esconde el nombre: hay que mirar el alias, no el literal.
    private static readonly Regex DibujaConPath = new(
        @"new\s+(?:[A-Za-z_][\w.]*\.)?Path\s*[{(]|typeof\s*\(\s*(?:[A-Za-z_][\w.]*\.)?Path\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex GeometriaEnCodigo = new(
        @"Geometry\.Parse\s*\(", RegexOptions.Compiled);

    [Fact]
    public void Ninguna_geometria_de_icono_se_declara_a_mano_en_XAML()
    {
        var aMano = new List<string>();

        foreach (var archivo in Xaml().Where(a => !Path.GetFileName(a).StartsWith("Iconos.", StringComparison.Ordinal)))
        {
            var texto = File.ReadAllText(archivo);

            foreach (Match m in GeometriaDeclarada.Matches(texto))
            {
                aMano.Add($"{Path.GetFileName(archivo)}: {m.Groups["clave"].Value}");
            }

            foreach (Match m in TrazadoEnAtributo.Matches(texto))
            {
                aMano.Add($"{Path.GetFileName(archivo)}: Data=\"{m.Groups["trazado"].Value[..20]}…\"");
            }
        }

        Assert.True(aMano.Count == 0, "Estas geometrías tienen que venir del catálogo:"
            + Environment.NewLine + string.Join(Environment.NewLine, aMano));
    }

    [Fact]
    public void Ninguna_geometria_se_arma_con_Geometry_Parse_en_el_codigo()
    {
        var aMano = Codigo()
            .Where(a => GeometriaEnCodigo.IsMatch(File.ReadAllText(a)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(aMano.Count == 0,
            "Estos archivos arman geometrías a mano: " + string.Join(", ", aMano));
    }

    [Fact]
    public void Ningun_control_de_la_interfaz_dibuja_con_un_Path()
    {
        var conPath = Codigo()
            .Where(a => DibujaConPath.IsMatch(File.ReadAllText(a)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(conPath.Count == 0,
            "Estos archivos siguen dibujando con Path en vez de IconoVectorial: "
            + string.Join(", ", conPath));
    }

    // El guardián tiene que reconocer lo que busca, incluido el alias que se le escapó una vez.
    [Theory]
    [InlineData("var p = new System.Windows.Shapes.Path {")]
    [InlineData("var p = new Forma.Path {")]
    [InlineData("Content = new Path()")]
    [InlineData("new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));")]
    public void El_guardian_reconoce_todas_las_formas_de_nombrar_un_Path(string sembrado) =>
        Assert.Matches(DibujaConPath, sembrado);

    [Fact]
    public void Toda_clave_de_la_interfaz_existe_en_el_catalogo()
    {
        foreach (var campo in Constantes())
        {
            var clave = (string)campo.GetRawConstantValue()!;

            Assert.True(
                CatalogoDeIconos.EsValido(clave),
                $"IconosDeLaInterfaz.{campo.Name} apunta a «{clave}», que el catálogo no tiene.");
        }
    }

    [Fact]
    public void La_interfaz_nombra_todo_lo_que_dibuja()
    {
        Assert.NotEmpty(Constantes());
    }

    [Fact]
    public void Todo_proceso_conocido_apunta_a_un_icono_del_catalogo()
    {
        foreach (var nombre in ProcesosConocidos())
        {
            var clave = Domain.Monitoring.IconoDeProceso.ClaveDeIcono(nombre);

            Assert.True(
                CatalogoDeIconos.EsValido(clave),
                $"El proceso «{nombre}» apunta a «{clave}», que el catálogo no tiene.");
        }
    }

    [Fact]
    public void Toda_clase_de_aplicacion_apunta_a_un_icono_del_catalogo()
    {
        foreach (var clase in Enum.GetValues<ClaseDeAplicacion>())
        {
            var clave = IconosDeAplicacion.Glifo(clase);

            Assert.True(CatalogoDeIconos.EsValido(clave),
                $"La clase «{clase}» apunta a «{clave}», que el catálogo no tiene.");
        }
    }

    [Fact]
    public void Toda_aplicacion_conocida_apunta_a_un_icono_del_catalogo()
    {
        foreach (var aplicacion in AplicacionesConocidasDeLaTabla())
        {
            var clave = IconosDeAplicacion.Glifo(aplicacion);

            Assert.True(CatalogoDeIconos.EsValido(clave),
                $"«{aplicacion.Nombre}» apunta a «{clave}», que el catálogo no tiene.");
        }
    }

    private static IEnumerable<AplicacionConocida> AplicacionesConocidasDeLaTabla()
    {
        var tabla = typeof(AplicacionesConocidas)
            .GetField("Tabla", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        foreach (var entrada in (Array)tabla)
        {
            yield return (AplicacionConocida)entrada.GetType().GetField("Item2")!.GetValue(entrada)!;
        }
    }

    [Fact]
    public void Los_iconos_por_omision_existen_en_el_catalogo()
    {
        foreach (var clave in new[]
                 {
                     IconosPorOmision.Carpeta, IconosPorOmision.Rdp, IconosPorOmision.Ssh,
                     IconosPorOmision.Web, IconosPorOmision.Aplicacion,
                 })
        {
            Assert.True(CatalogoDeIconos.EsValido(clave), $"«{clave}» no está en el catálogo.");
        }
    }

    private static IReadOnlyList<FieldInfo> Constantes() =>
        typeof(IconosDeLaInterfaz)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToList();

    private static IEnumerable<string> ProcesosConocidos()
    {
        var conocidos = typeof(Domain.Monitoring.IconoDeProceso)
            .GetField("Conocidos", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        foreach (var entrada in (Array)conocidos)
        {
            yield return (string)entrada.GetType().GetField("Item1")!.GetValue(entrada)!;
        }
    }

    private static IEnumerable<string> Xaml() => Archivos("*.xaml");

    private static IEnumerable<string> Codigo() => Archivos("*.cs");

    private static IEnumerable<string> Archivos(string patron) =>
        Directory.EnumerateFiles(
                Path.Combine(RaizDelRepositorio(), "src", "CafManagerConection.App"),
                patron,
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
