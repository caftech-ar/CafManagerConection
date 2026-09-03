using System.Text.RegularExpressions;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

// La conversión de clave de color a nombre de recurso estaba copiada en tres ventanas: en una se
// perdió, pedía el recurso "azul" y la ventana de configuración de carpetas se caía al abrirse.
public sealed class PincelesDeLaPaletaTests
{
    private static readonly string[] Temas =
    [
        "src/CafManagerConection.App/Themes/Paleta.Claro.xaml",
        "src/CafManagerConection.App/Themes/Paleta.Oscuro.xaml",
    ];

    [Theory]
    [InlineData("azul", "IconoAzul")]
    [InlineData("gris", "IconoGris")]
    [InlineData("ambar", "IconoAmbar")]
    public void La_clave_del_color_se_convierte_en_el_nombre_del_recurso(string clave, string esperado)
    {
        Assert.Equal(esperado, PaletaIconos.ClaveDeRecurso(clave));
    }

    // Una clave que no está en la paleta no puede terminar en un recurso inventado como "IconoFucsia".
    [Theory]
    [InlineData("fucsia")]
    [InlineData("")]
    [InlineData(null)]
    public void Una_clave_desconocida_cae_en_un_recurso_que_existe(string? clave)
    {
        Assert.Equal("TextoTenue", PaletaIconos.ClaveDeRecurso(clave));
    }

    [Fact]
    public void Todos_los_colores_tienen_pincel_en_los_dos_temas()
    {
        var raiz = RaizDelRepositorio();

        foreach (var tema in Temas)
        {
            var declaradas = ClavesDe(Path.Combine(raiz, tema));

            foreach (var color in PaletaIconos.Colores)
            {
                var recurso = PaletaIconos.ClaveDeRecurso(color.Clave);

                Assert.True(
                    declaradas.Contains(recurso),
                    $"Falta el pincel «{recurso}» (color «{color.Clave}») en {tema}.");
            }
        }
    }

    [Fact]
    public void El_recurso_de_reserva_existe_en_los_dos_temas()
    {
        var raiz = RaizDelRepositorio();

        foreach (var tema in Temas)
        {
            Assert.Contains("TextoTenue", ClavesDe(Path.Combine(raiz, tema)));
        }
    }

    private static HashSet<string> ClavesDe(string archivo)
    {
        Assert.True(File.Exists(archivo), $"No está el archivo de tema {archivo}.");

        return [.. Regex.Matches(File.ReadAllText(archivo), "x:Key=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)];
    }

    private static string RaizDelRepositorio() => Repositorio.Raiz();
}
