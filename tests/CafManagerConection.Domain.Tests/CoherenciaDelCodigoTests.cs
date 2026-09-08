using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// Reemplaza al guardian que cruzaba el codigo contra specs/ y docs/. Al borrarse esas carpetas dos
// de sus cuatro cruces pasaban en el vacio y un tercero lanzaba DirectoryNotFoundException. Estos
// cuatro miran el codigo y el unico documento que queda.
public sealed class CoherenciaDelCodigoTests
{
    private static readonly Regex Ruta = new(
        @"\b(?:src|tests)/[A-Za-z0-9._/-]+\.(?:cs|xaml|csproj|md)\b", RegexOptions.Compiled);

    private static readonly Regex PorLinea = new(
        @"\b[A-Za-z0-9._/-]+\.(?:cs|xaml|md|ps1|yml|nsi|props):[0-9]+", RegexOptions.Compiled);

    private static readonly Regex ConstitucionPorNumero = new(
        @"constituci[oó]n\s+v?[0-9]+\.[0-9]+\.[0-9]+|constitution\.md:[0-9]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Sin guion tambien: «SC052» en el nombre de una clase de prueba se le escapaba a un patron
    // que exigia «SC-052». Ahi hacen falta dos digitos, porque «SC2» es cualquier cosa, y no cierra
    // con \b: el nombre sigue en «SC052_Algo» y el guion bajo es caracter de palabra.
    private static readonly Regex Identificador = new(
        @"\b(?:FR|SC)-[0-9]+[a-z]?\b|\b(?:FR|SC)[0-9]{2,}[a-z]?(?![0-9])", RegexOptions.Compiled);

    private static readonly Regex DocumentoInexistente = new(
        @"constituci[oó]n|\bPrincipio [IVX]+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void Ningun_archivo_cita_un_identificador_de_requisito()
    {
        var hallazgos = new List<string>();

        foreach (var (relativa, lineas) in Todo())
        {
            for (var i = 0; i < lineas.Length; i++)
            {
                foreach (var id in Identificador.Matches(lineas[i]).Select(m => m.Value).Distinct())
                {
                    hallazgos.Add($"{relativa}:{i + 1}  →  {id}");
                }
            }
        }

        Exigir(
            hallazgos,
            "Estos identificadores nombran requisitos de un documento que no existe. Si la cita va "
            + "de prefijo y la sigue la regla, borrá la cita y dejá la frase; si el comentario "
            + "entero es la cita, borralo: no dice nada.");
    }

    // Eran 30 y son la misma clase de defecto que los identificadores: nombran por numero un
    // documento que no existe. La regla queda enunciada en la frase, que es lo que se puede leer.
    [Fact]
    public void Ningun_archivo_cita_la_constitucion_ni_sus_principios()
    {
        var hallazgos = new List<string>();

        foreach (var (relativa, lineas) in Todo())
        {
            for (var i = 0; i < lineas.Length; i++)
            {
                foreach (var cita in DocumentoInexistente.Matches(lineas[i])
                             .Select(m => m.Value).Distinct())
                {
                    hallazgos.Add($"{relativa}:{i + 1}  →  {cita}");
                }
            }
        }

        Exigir(
            hallazgos,
            "Estas citas nombran una constitución que no existe, o uno de sus principios por "
            + "número. Enunciá la regla en la frase: «nunca contiene el secreto» dice lo mismo y "
            + "se puede leer sin ir a buscar nada.");
    }

    [Fact]
    public void Nada_se_cita_por_numero_de_linea()
    {
        var hallazgos = new List<string>();

        foreach (var (relativa, lineas) in Todo())
        {
            for (var i = 0; i < lineas.Length; i++)
            {
                foreach (var cita in PorLinea.Matches(lineas[i]).Select(m => m.Value)
                             .Concat(ConstitucionPorNumero.Matches(lineas[i]).Select(m => m.Value))
                             .Distinct())
                {
                    hallazgos.Add($"{relativa}:{i + 1}  →  {cita}");
                }
            }
        }

        Exigir(
            hallazgos,
            "Estas citas apuntan a un número de línea o a una versión de la constitución. Las dos "
            + "cosas se corren con la primera edición: citá el símbolo —«Migrate()»— y el título "
            + "de la sección.");
    }

    [Fact]
    public void Toda_ruta_citada_en_un_documento_existe()
    {
        var hallazgos = new List<string>();

        foreach (var (relativa, lineas) in Documentos())
        {
            for (var i = 0; i < lineas.Length; i++)
            {
                foreach (var cita in Ruta.Matches(lineas[i]).Select(m => m.Value).Distinct())
                {
                    if (File.Exists(Path.Combine(Repositorio.Raiz(), cita)))
                    {
                        continue;
                    }

                    hallazgos.Add($"{relativa}:{i + 1}  →  {cita}");
                }
            }
        }

        Exigir(
            hallazgos,
            "Estas rutas se citan en un documento y no existen en el árbol de trabajo. Es lo que "
            + "dejó siete enlaces muertos en el README anterior.");
    }

    private static IEnumerable<(string Relativa, string[] Lineas)> Todo() =>
        Fuentes().Concat(Documentos()).Concat(Auxiliares());

    // Los csproj, los guiones y la configuracion. Quedaban afuera, y ahi habia nueve citas: una en
    // un mensaje que auditar-secretos.ps1 imprime en pantalla, y un comentario de Ssh.csproj que
    // explicaba una ProjectReference ya borrada.
    private static IEnumerable<(string Relativa, string[] Lineas)> Auxiliares()
    {
        var raiz = Repositorio.Raiz();

        foreach (var (carpeta, patron, recursivo) in new[]
                 {
                     (".", "*.yml", false),
                     (".", "*.props", false),
                     ("src", "*.csproj", true),
                     ("tests", "*.csproj", true),
                     ("scripts", "*.ps1", false),
                     ("build", "*.ps1", false),
                     ("installer", "*.nsi", false),
                     (".github/workflows", "*.yml", false),
                 })
        {
            var completa = Path.Combine(raiz, carpeta.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(completa))
            {
                continue;
            }

            var busqueda = recursivo
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            foreach (var archivo in Directory.EnumerateFiles(completa, patron, busqueda))
            {
                if (EsGenerado(archivo))
                {
                    continue;
                }

                yield return (
                    Path.GetRelativePath(raiz, archivo).Replace(Path.DirectorySeparatorChar, '/'),
                    File.ReadAllLines(archivo));
            }
        }
    }

    // El README de la raiz y lo que haya en openspec/. Si no hay ninguno, no hay hallazgos: el
    // guardian anterior lanzaba al enumerar una carpeta borrada.
    private static IEnumerable<(string Relativa, string[] Lineas)> Documentos()
    {
        var raiz = Repositorio.Raiz();
        var readme = Path.Combine(raiz, "README.md");

        if (File.Exists(readme))
        {
            yield return ("README.md", File.ReadAllLines(readme));
        }

        foreach (var archivo in Archivos(["openspec"], "*.md"))
        {
            yield return archivo;
        }
    }

    private static IEnumerable<(string Relativa, string[] Lineas)> Fuentes() =>
        Archivos(["src", "tests"], "*.cs").Concat(Archivos(["src", "tests"], "*.xaml"));

    private static IEnumerable<(string Relativa, string[] Lineas)> Archivos(
        string[] carpetas, string patron)
    {
        var raiz = Repositorio.Raiz();

        foreach (var carpeta in carpetas)
        {
            var completa = Path.Combine(raiz, carpeta);

            if (!Directory.Exists(completa))
            {
                continue;
            }

            foreach (var archivo in Directory.EnumerateFiles(
                         completa, patron, SearchOption.AllDirectories))
            {
                if (EsGenerado(archivo))
                {
                    continue;
                }

                yield return (
                    Path.GetRelativePath(raiz, archivo).Replace(Path.DirectorySeparatorChar, '/'),
                    File.ReadAllLines(archivo));
            }
        }
    }

    private static bool EsGenerado(string archivo)
    {
        var separador = Path.DirectorySeparatorChar;

        return archivo.Contains($"{separador}obj{separador}")
               || archivo.Contains($"{separador}bin{separador}")
               || EsDelGuardian(archivo);
    }

    // Los dos archivos del guardian llevan los patrones que buscan y las citas de ejemplo con las
    // que se prueba que las detectan. Es la unica exencion, y sin ella el guardian se acusa a si
    // mismo.
    private static bool EsDelGuardian(string archivo)
    {
        var nombre = Path.GetFileNameWithoutExtension(archivo);

        return nombre is nameof(CoherenciaDelCodigoTests)
                      or nameof(CoherenciaDetectaLosDefectosTests);
    }

    internal static bool EsCitaPorLinea(string linea) =>
        PorLinea.IsMatch(linea) || ConstitucionPorNumero.IsMatch(linea);

    internal static IEnumerable<string> RutasCitadas(string linea) =>
        Ruta.Matches(linea).Select(m => m.Value);

    internal static IEnumerable<string> IdentificadoresCitados(string linea) =>
        Identificador.Matches(linea).Select(m => m.Value);

    internal static bool CitaLaConstitucion(string linea) =>
        DocumentoInexistente.IsMatch(linea);

    private static void Exigir(List<string> hallazgos, string porQue)
    {
        Assert.True(
            hallazgos.Count == 0,
            $"{hallazgos.Count} hallazgo(s). {porQue}{Environment.NewLine}{Environment.NewLine}"
            + string.Join(Environment.NewLine, hallazgos));
    }
}
