using System.Text.RegularExpressions;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

// Misma familia que PincelesDeLaPaletaTests: un recurso inventado no falla al compilar, deja el
// icono vacío o tira la ventana al abrirla.
public sealed class JuegoDeIconosTests
{
    private const string Estilos = "src/CafManagerConection.App/Themes/Estilos.xaml";

    private static readonly string[] UsosDeServidor =
    [
        "base-de-datos", "web", "correo", "archivos",
        "respaldo", "contenedor", "cortafuegos", "monitoreo",
    ];

    [Fact]
    public void El_juego_cubre_los_usos_habituales_de_un_servidor()
    {
        foreach (var clave in UsosDeServidor)
        {
            Assert.True(JuegoDeIconos.EsValido(clave), $"Falta el icono «{clave}» en el juego.");
        }
    }

    [Fact]
    public void El_juego_tambien_trae_los_genericos()
    {
        foreach (var clave in new[] { "carpeta", "escritorio", "terminal", "aplicacion" })
        {
            Assert.True(JuegoDeIconos.EsValido(clave), $"Falta el icono «{clave}» en el juego.");
        }
    }

    [Fact]
    public void Las_claves_no_se_repiten()
    {
        var claves = JuegoDeIconos.Iconos.Select(i => i.Clave).ToList();

        Assert.Equal(claves.Count, claves.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Los_recursos_no_se_repiten()
    {
        var recursos = JuegoDeIconos.Iconos.Select(i => i.Recurso).ToList();

        Assert.Equal(recursos.Count, recursos.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Las_claves_son_minusculas_y_sin_espacios()
    {
        foreach (var icono in JuegoDeIconos.Iconos)
        {
            Assert.Equal(icono.Clave.ToLowerInvariant(), icono.Clave);
            Assert.DoesNotContain(' ', icono.Clave);
            Assert.NotEmpty(icono.Nombre);
        }
    }

    [Theory]
    [InlineData("correo", true)]
    [InlineData("cortafuegos", true)]
    [InlineData("dinosaurio", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Solo_las_claves_del_juego_son_validas(string? clave, bool esperado)
    {
        Assert.Equal(esperado, JuegoDeIconos.EsValido(clave));
    }

    [Theory]
    [InlineData("dinosaurio")]
    [InlineData("")]
    [InlineData(null)]
    public void Una_clave_desconocida_no_devuelve_recurso(string? clave)
    {
        Assert.Null(JuegoDeIconos.ClaveDeRecurso(clave));
    }

    [Fact]
    public void Todo_icono_del_juego_apunta_a_una_geometria_declarada_en_Estilos()
    {
        var declaradas = ClavesDe(Path.Combine(Repositorio.Raiz(), Estilos));

        Assert.NotEmpty(declaradas);

        foreach (var icono in JuegoDeIconos.Iconos)
        {
            Assert.True(
                declaradas.Contains(icono.Recurso),
                $"Falta la geometría «{icono.Recurso}» (icono «{icono.Clave}») en {Estilos}.");
        }
    }

    // Estilos.xaml esta en los MergedDictionaries de App.xaml; una geometria declarada en el
    // UserControl.Resources de un panel no se resuelve desde el arbol.
    [Fact]
    public void Ningun_icono_del_juego_depende_de_un_diccionario_local_de_panel()
    {
        var deEstilos = ClavesDe(Path.Combine(Repositorio.Raiz(), Estilos));

        var locales = Repositorio.ArchivosDe("CafManagerConection.App", "*.xaml")
            .Where(a => !a.EndsWith("Estilos.xaml", StringComparison.Ordinal))
            .SelectMany(a => ClavesDe(a))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var icono in JuegoDeIconos.Iconos.Where(i => !deEstilos.Contains(i.Recurso)))
        {
            Assert.False(
                locales.Contains(icono.Recurso),
                $"«{icono.Recurso}» sólo existe en un diccionario local.");
        }
    }

    private static HashSet<string> ClavesDe(string archivo)
    {
        Assert.True(File.Exists(archivo), $"No está el archivo {archivo}.");

        return [.. Regex.Matches(File.ReadAllText(archivo), "x:Key=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)];
    }
}
