using System.Reflection;
using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Tests;

// Vuelve ejecutable el Principio I de la constitución: el dominio no depende de UI,
// persistencia, red ni COM.
public class ArchitectureTests
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
            "El Principio I lo prohíbe.");
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
    // se llama UseCases justamente por esto (constitución v1.1.0).
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
}
