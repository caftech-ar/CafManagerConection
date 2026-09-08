using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// El README anterior tenia siete enlaces muertos, declaraba una version que no existia en ningun
// lado, listaba seis de los ocho paneles de sesion y le faltaban tres atajos. Las cuatro clases de
// afirmacion que se le desfasaron son las que una prueba puede cruzar contra el codigo.
public sealed class ReadmeComprobableTests
{
    private static readonly Regex Version = new(
        @"\b[0-9]+\.[0-9]+\.[0-9]+\b", RegexOptions.Compiled);

    private static readonly Regex TareaCitada = new(
        @"`task ([a-z][a-zA-Z0-9:_-]*)", RegexOptions.Compiled);

    // Solo entre acentos graves, y una letra suelta sin modificador no cuenta: esta aplicacion no
    // tiene atajos de una tecla alfabetica, asi que una letra entre acentos graves en la prosa es
    // un ejemplo y no un atajo.
    private static readonly Regex AtajoCitado = new(
        @"`(Ctrl\+(?:F[0-9]{1,2}|Tab|Enter|Delete|[A-Z])|F[0-9]{1,2}|Tab|Enter|Delete)`",
        RegexOptions.Compiled);

    private static readonly Regex CasoDeTecla = new(
        @"case Key\.(?<tecla>[A-Za-z0-9]+)\b", RegexOptions.Compiled);

    private static readonly Regex TareaDelTaskfile = new(
        @"^  (?<nombre>[a-z][a-zA-Z0-9:_-]*):", RegexOptions.Compiled);

    private static readonly Regex VersionDelProyecto = new(
        @"<Version>(?<valor>[^<]+)</Version>", RegexOptions.Compiled);

    [Fact]
    public void La_version_que_declara_el_readme_es_la_del_proyecto()
    {
        if (Readme() is not { } readme)
        {
            return;
        }

        var esperada = VersionDelProyecto
            .Match(File.ReadAllText(Path.Combine(Repositorio.Raiz(), "Directory.Build.props")))
            .Groups["valor"].Value;

        Assert.False(string.IsNullOrWhiteSpace(esperada), "Directory.Build.props no declara Version.");

        var equivocadas = Version.Matches(readme)
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .Where(v => v != esperada)
            .ToArray();

        Assert.True(
            equivocadas.Length == 0,
            $"El README nombra estas versiones y el proyecto está en {esperada}: "
            + string.Join(", ", equivocadas)
            + ". El README anterior anunciaba una versión que no existía en ningún lado.");
    }

    [Fact]
    public void Toda_tarea_que_menciona_el_readme_existe_en_el_taskfile()
    {
        if (Readme() is not { } readme)
        {
            return;
        }

        var definidas = File
            .ReadAllLines(Path.Combine(Repositorio.Raiz(), "Taskfile.yml"))
            .Select(l => TareaDelTaskfile.Match(l))
            .Where(m => m.Success)
            .Select(m => m.Groups["nombre"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(definidas);

        var inexistentes = TareaCitada.Matches(readme)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(t => !definidas.Contains(t))
            .ToArray();

        Assert.True(
            inexistentes.Length == 0,
            "El README menciona tareas que el Taskfile no define: "
            + string.Join(", ", inexistentes));
    }

    [Fact]
    public void Los_atajos_del_readme_y_los_del_codigo_son_los_mismos()
    {
        if (Readme() is not { } readme)
        {
            return;
        }

        var enElCodigo = AtajosDeLaVentanaPrincipal();
        var enElReadme = AtajoCitado.Matches(readme)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(enElCodigo);

        var faltan = enElCodigo.Except(enElReadme, StringComparer.Ordinal).Order().ToArray();
        var sobran = enElReadme.Except(enElCodigo, StringComparer.Ordinal).Order().ToArray();

        Assert.True(
            faltan.Length == 0 && sobran.Length == 0,
            $"Atajos que el código atiende y el README no lista: [{string.Join(", ", faltan)}]. "
            + $"Atajos que el README lista y el código no atiende: [{string.Join(", ", sobran)}]. "
            + "Al README anterior le faltaban tres.");
    }

    [Fact]
    public void El_readme_es_el_unico_documento_del_proyecto()
    {
        var raiz = Repositorio.Raiz();

        var otros = Directory
            .EnumerateFiles(raiz, "*.md", SearchOption.AllDirectories)
            .Where(a => !EsDeHerramienta(Path.GetRelativePath(raiz, a)))
            .Select(a => Path.GetRelativePath(raiz, a).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(r => r != "README.md")
            .ToArray();

        Assert.True(
            otros.Length == 0,
            "Además del README hay estos documentos, y cada uno es algo más que se puede desfasar: "
            + string.Join(", ", otros));
    }

    internal static IEnumerable<string> AtajosCitadosEn(string texto) =>
        AtajoCitado.Matches(texto).Select(m => m.Groups[1].Value);

    internal static IEnumerable<string> TareasCitadasEn(string texto) =>
        TareaCitada.Matches(texto).Select(m => m.Groups[1].Value);

    internal static IEnumerable<string> VersionesCitadasEn(string texto) =>
        Version.Matches(texto).Select(m => m.Value);

    // Las teclas del manejador global de la ventana principal, con Ctrl cuando la guarda lo pide.
    private static HashSet<string> AtajosDeLaVentanaPrincipal()
    {
        var archivo = Path.Combine(
            Repositorio.Raiz(),
            "src", "CafManagerConection.App", "Views", "MainWindow.xaml.cs");

        var atajos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var linea in File.ReadAllLines(archivo))
        {
            if (CasoDeTecla.Match(linea) is not { Success: true } caso)
            {
                continue;
            }

            var tecla = caso.Groups["tecla"].Value;
            var conControl = linea.Contains("when control", StringComparison.Ordinal);

            atajos.Add(conControl ? $"Ctrl+{tecla}" : tecla);
        }

        return atajos;
    }

    private static string? Readme()
    {
        var ruta = Path.Combine(Repositorio.Raiz(), "README.md");

        return File.Exists(ruta) ? File.ReadAllText(ruta) : null;
    }

    private static bool EsDeHerramienta(string relativa)
    {
        var normalizada = relativa.Replace(Path.DirectorySeparatorChar, '/');

        return normalizada.StartsWith("openspec/", StringComparison.Ordinal)
               || normalizada.StartsWith(".claude/", StringComparison.Ordinal)
               || normalizada.StartsWith("publish/", StringComparison.Ordinal)
               || normalizada.Contains("/obj/", StringComparison.Ordinal)
               || normalizada.Contains("/bin/", StringComparison.Ordinal)
               || normalizada.Contains("/node_modules/", StringComparison.Ordinal);
    }
}
