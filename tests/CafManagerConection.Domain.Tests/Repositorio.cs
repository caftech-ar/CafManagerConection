namespace CafManagerConection.Domain.Tests;

// La ruta relativa desde el directorio de salida cambia con el destino y la configuración, así
// que se sube buscando la forma del repositorio en lugar de contar carpetas.
internal static class Repositorio
{
    public static string Raiz()
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

    /// <summary>Los archivos de un proyecto de <c>src/</c>, sin lo generado.</summary>
    public static IEnumerable<string> ArchivosDe(string proyecto, string patron) =>
        Directory.EnumerateFiles(
                Path.Combine(Raiz(), "src", proyecto), patron, SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                        && !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
}
