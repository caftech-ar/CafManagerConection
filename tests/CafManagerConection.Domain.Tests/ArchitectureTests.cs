using System.Reflection;
using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Tests;

public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Connection).Assembly;

    private static readonly string[] DependenciasProhibidas =
    [
        "System.Windows.Forms",
        "Microsoft.Data.Sqlite",
        "Dapper",
        "Renci.SshNet",
        "VtNetCore",
        "Serilog",
        "System.Runtime.InteropServices.COM",
    ];

    [Fact]
    public void El_dominio_no_referencia_infraestructura()
    {
        var referencias = Domain.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        var violaciones = referencias
            .Where(r => DependenciasProhibidas.Any(
                p => r.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(
            violaciones.Length == 0,
            $"El dominio referencia: {string.Join(", ", violaciones)}. " +
            "El dominio se compila solo.");
    }

    [Fact]
    public void El_dominio_no_expone_tipos_de_WinForms()
    {
        var tiposExpuestos = Domain.GetExportedTypes()
            .SelectMany(t => t.GetProperties())
            .Select(p => p.PropertyType.FullName ?? string.Empty)
            .Where(n => n.StartsWith("System.Windows.Forms", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(tiposExpuestos);
    }

    // Cubre el error CS0118 por colisión con System.Windows.Forms.Application; la capa
    // se llama UseCases justamente por esto.
    [Fact]
    public void No_existe_un_namespace_Application()
    {
        var conflictivos = Domain.GetTypes()
            .Select(t => t.Namespace ?? string.Empty)
            .Where(ns => ns.EndsWith(".Application", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        Assert.True(
            conflictivos.Length == 0,
            $"Namespaces que colisionan con System.Windows.Forms.Application: " +
            $"{string.Join(", ", conflictivos)}");
    }

    private static readonly string[] Adaptadores =
    [
        "Infrastructure", "Monitoring", "Platform", "Rdp", "Ssh", "Terminal",
    ];

    // Ssh referenciaba a Platform sólo para implementar un puerto que Platform declaraba. Nada lo
    // detectaba: las tres pruebas de arriba miran el dominio y ninguna mira entre adaptadores.
    [Fact]
    public void Ningun_adaptador_referencia_a_otro_adaptador()
    {
        var permitidos = new[] { "CafManagerConection.UseCases", "CafManagerConection.Domain" };
        var violaciones = new List<string>();

        foreach (var adaptador in Adaptadores)
        {
            var proyecto = $"CafManagerConection.{adaptador}";
            var csproj = Path.Combine(
                Repositorio.Raiz(), "src", proyecto, $"{proyecto}.csproj");

            foreach (var referencia in ReferenciasDe(csproj))
            {
                if (!permitidos.Contains(referencia, StringComparer.Ordinal))
                {
                    violaciones.Add($"{proyecto} → {referencia}");
                }
            }
        }

        Assert.True(
            violaciones.Count == 0,
            "Un adaptador referencia a otro adaptador: "
            + string.Join(", ", violaciones)
            + ". El puerto va en UseCases/Abstractions y los dos dependen del puerto.");
    }

    // El mismo puerto estaba declarado en Monitoring y en Platform con la misma firma, y por eso
    // dos adaptadores de App tenían que implementar las dos interfaces con el mismo cuerpo.
    [Fact]
    public void Solo_la_capa_de_abstracciones_declara_la_ejecucion_remota()
    {
        var raiz = Repositorio.Raiz();
        var esperada = Path.Combine("src", "CafManagerConection.UseCases", "Abstractions");

        var declaran = Directory
            .EnumerateFiles(Path.Combine(raiz, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(a => !EsGenerado(a))
            .Where(a => DeclaraEjecucionRemota(File.ReadAllLines(a)))
            .Select(a => Path.GetRelativePath(raiz, a))
            .ToArray();

        Assert.NotEmpty(declaran);

        var afuera = declaran
            .Where(r => !r.StartsWith(esperada, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(
            afuera.Length == 0,
            "Estos archivos declaran una interfaz de ejecución remota fuera de la capa de "
            + $"abstracciones: {string.Join(", ", afuera)}");
    }

    internal static IEnumerable<string> ReferenciasDe(string csproj)
    {
        foreach (var linea in File.ReadAllLines(csproj))
        {
            var marca = linea.IndexOf("ProjectReference Include=\"", StringComparison.Ordinal);

            if (marca < 0)
            {
                continue;
            }

            var ruta = linea[(marca + "ProjectReference Include=\"".Length)..];
            ruta = ruta[..ruta.IndexOf('"', StringComparison.Ordinal)];

            yield return Path.GetFileNameWithoutExtension(ruta);
        }
    }

    // Una interfaz cuyo cuerpo toma «int timeoutSeconds»: es la firma de esta familia de puertos.
    internal static bool DeclaraEjecucionRemota(string[] lineas)
    {
        var profundidad = 0;
        var dentro = false;

        foreach (var linea in lineas)
        {
            if (!dentro && linea.Contains(" interface ", StringComparison.Ordinal))
            {
                dentro = true;
                profundidad = 0;
            }

            if (!dentro)
            {
                continue;
            }

            if (linea.Contains("int timeoutSeconds", StringComparison.Ordinal))
            {
                return true;
            }

            profundidad += linea.Count(c => c == '{') - linea.Count(c => c == '}');

            if (profundidad <= 0 && linea.Contains('}', StringComparison.Ordinal))
            {
                dentro = false;
            }
        }

        return false;
    }

    private static bool EsGenerado(string ruta)
    {
        var separador = Path.DirectorySeparatorChar;

        return ruta.Contains($"{separador}obj{separador}", StringComparison.Ordinal)
               || ruta.Contains($"{separador}bin{separador}", StringComparison.Ordinal);
    }
}
