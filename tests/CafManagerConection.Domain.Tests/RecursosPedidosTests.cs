using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// Misma familia de defectos que EstilosAplicadosTests y PincelesDeLaPaletaTests: ya pasó dos
// veces en este proyecto, un pincel "Advertencia" y un recurso "azul" (en vez de "IconoAzul")
// que no existían y tiraban abajo la pantalla recién al abrirla.
public sealed class RecursosPedidosTests
{
    private static readonly Regex Declaradas = new(@"x:Key=""([^""]+)""");

    private static readonly Regex PedidasEnXaml = new(
        @"\{(?:StaticResource|DynamicResource)\s+([^}\s]+)\s*\}");

    // Sólo las claves escritas como literal: las que se arman en tiempo de ejecución —el icono de
    // cada panel se pide como $"IconoPanel{nombre}"— no se pueden comprobar leyendo el texto, y
    // pretender lo contrario daría una prueba que falla sobre código correcto.
    private static readonly Regex PedidasEnCodigo = new(
        @"(?:FindResource|TryFindResource|SetResourceReference)\((?:[^(),]*,\s*)?""([^""{}]+)""\)");

    private static IReadOnlySet<string> Definidas()
    {
        var claves = new HashSet<string>(StringComparer.Ordinal);

        foreach (var archivo in Repositorio.ArchivosDe("CafManagerConection.App", "*.xaml"))
        {
            foreach (var m in Declaradas.Matches(File.ReadAllText(archivo)).Cast<Match>())
            {
                claves.Add(m.Groups[1].Value);
            }
        }

        return claves;
    }

    [Fact]
    public void Todo_recurso_pedido_desde_XAML_esta_declarado()
    {
        var definidas = Definidas();

        Assert.NotEmpty(definidas);

        var faltan = new List<string>();

        foreach (var archivo in Repositorio.ArchivosDe("CafManagerConection.App", "*.xaml"))
        {
            var contenido = File.ReadAllText(archivo);

            foreach (var m in PedidasEnXaml.Matches(contenido).Cast<Match>())
            {
                var clave = m.Groups[1].Value;

                if (clave.StartsWith('{') || definidas.Contains(clave))
                {
                    continue;
                }

                var linea = contenido[..m.Index].Count(c => c == '\n') + 1;
                faltan.Add($"{Path.GetFileName(archivo)}:{linea}: {clave}");
            }
        }

        Assert.Empty(faltan);
    }

    [Fact]
    public void Todo_recurso_pedido_desde_codigo_esta_declarado()
    {
        var definidas = Definidas();

        Assert.NotEmpty(definidas);

        var faltan = new List<string>();

        foreach (var archivo in Repositorio.ArchivosDe("CafManagerConection.App", "*.cs"))
        {
            var contenido = File.ReadAllText(archivo);

            foreach (var m in PedidasEnCodigo.Matches(contenido).Cast<Match>())
            {
                var clave = m.Groups[1].Value;

                if (definidas.Contains(clave))
                {
                    continue;
                }

                var linea = contenido[..m.Index].Count(c => c == '\n') + 1;
                faltan.Add($"{Path.GetFileName(archivo)}:{linea}: {clave}");
            }
        }

        Assert.Empty(faltan);
    }

    // Una prueba que sólo afirma ausencias pasa igual cuando su búsqueda está mal escrita. Los
    // mínimos son bajos a propósito: fijan que hay búsqueda, no cuántos recursos debe tener el
    // proyecto.
    [Fact]
    public void Las_busquedas_de_esta_prueba_encuentran_algo()
    {
        var definidas = Definidas();

        var enXaml = Repositorio.ArchivosDe("CafManagerConection.App", "*.xaml")
            .Sum(a => PedidasEnXaml.Matches(File.ReadAllText(a)).Count);

        var enCodigo = Repositorio.ArchivosDe("CafManagerConection.App", "*.cs")
            .Sum(a => PedidasEnCodigo.Matches(File.ReadAllText(a)).Count);

        Assert.True(definidas.Count > 50, $"claves declaradas: {definidas.Count}");
        Assert.True(enXaml > 100, $"recursos pedidos desde XAML: {enXaml}");
        Assert.True(enCodigo > 20, $"recursos pedidos desde código: {enCodigo}");
    }
}
