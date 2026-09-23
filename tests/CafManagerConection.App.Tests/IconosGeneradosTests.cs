using System.Diagnostics;

namespace CafManagerConection.App.Tests;

// El XAML de los iconos se versiona, así que puede quedar atrás de los SVG si alguien toca uno y no
// corre el script. Esta prueba lo corre en modo verificación y falla nombrando el diccionario viejo.
public sealed class IconosGeneradosTests
{
    private const int TopeDeEspera = 120_000;

    [Fact]
    public async Task Los_diccionarios_de_iconos_estan_al_dia_con_sus_svg()
    {
        var script = Path.Combine(RaizDelRepositorio(), "build", "convertir-iconos.ps1");
        Assert.True(File.Exists(script), $"No está {script}.");

        using var proceso = Process.Start(new ProcessStartInfo("pwsh")
        {
            ArgumentList = { "-NoProfile", "-NonInteractive", "-File", script, "-Verificar" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("No se pudo lanzar pwsh.");

        // Los dos flujos en paralelo: leer uno hasta el final mientras el otro llena su búfer traba
        // a los dos. Y con tope, para que un guion colgado no deje la suite esperando para siempre.
        var salida = proceso.StandardOutput.ReadToEndAsync();
        var error = proceso.StandardError.ReadToEndAsync();

        if (!proceso.WaitForExit(TopeDeEspera))
        {
            proceso.Kill(entireProcessTree: true);
            Assert.Fail($"build/convertir-iconos.ps1 no terminó en {TopeDeEspera / 1000} segundos.");
        }

        Assert.True(
            proceso.ExitCode == 0,
            await salida + await error);
    }

    [Fact]
    public void Todos_los_diccionarios_generados_estan_en_App_xaml()
    {
        var raiz = RaizDelRepositorio();
        var app = File.ReadAllText(Path.Combine(raiz, "src", "CafManagerConection.App", "App.xaml"));

        var generados = Directory.EnumerateFiles(
            Path.Combine(raiz, "src", "CafManagerConection.App", "Themes"), "Iconos.*.xaml");

        foreach (var archivo in generados)
        {
            var nombre = Path.GetFileName(archivo);
            Assert.Contains($"Themes/{nombre}", app, StringComparison.Ordinal);
        }
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
