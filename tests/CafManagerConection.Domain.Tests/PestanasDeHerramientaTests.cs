using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// Las pestañas dejaron de ser sólo sesiones: ahora también hay de herramienta —el ping, el visor de
// bitácoras—. `MainWindow` no se puede instanciar en la suite, así que se vigila lo que se olvida:
// contar pestañas donde había que contar sesiones. Con `_sesiones.Items.Count`, abrir un ping hacía
// que al cerrar la aplicación preguntara por sesiones que no existían.
public sealed partial class PestanasDeHerramientaTests
{
    private const string Conteo = "_sesiones.Items.Count";
    private const string Correcto = "SesionesAbiertas()";

    private static string Fuente(string archivo) =>
        File.ReadAllText(Path.Combine(
            Repositorio.Raiz(), "src", "CafManagerConection.App", "Views", archivo));

    [Fact]
    public void Lo_que_cuenta_sesiones_no_cuenta_pestanas()
    {
        var texto = Fuente("MainWindow.xaml.cs");

        foreach (Match linea in LineasQueCuentan().Matches(texto))
        {
            var recorte = texto[linea.Index..Math.Min(texto.Length, linea.Index + 400)];

            Assert.True(
                !MencionaSesiones(recorte),
                $"MainWindow.xaml.cs usa «{Conteo}» donde habla de sesiones. Una pestaña de "
                + $"herramienta no es una sesión: usá «{Correcto}».");
        }
    }

    [Fact]
    public void El_titulo_y_el_aviso_de_cierre_cuentan_sesiones()
    {
        var texto = Fuente("MainWindow.xaml.cs");

        Assert.Contains(Correcto, texto, StringComparison.Ordinal);

        Assert.DoesNotContain(
            $"TextoDeAvisoDeCierre({Conteo}", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_pestana_de_herramienta_se_reconoce_por_no_tener_identificador()
    {
        var texto = Fuente("MainWindow.Herramientas.cs");

        Assert.Contains("pestana.Tag is Guid", texto, StringComparison.Ordinal);
        Assert.Contains(Correcto, texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Cerrar_una_pestana_sin_sesion_pasa_por_el_camino_de_herramienta()
    {
        var texto = Fuente("MainWindow.xaml.cs");

        Assert.Contains("CerrarHerramienta(pestana)", texto, StringComparison.Ordinal);
    }

    /// <summary>Si el tramo habla de sesiones, contar pestañas ahí es el error que se vigila.</summary>
    /// <param name="recorte">Trozo de código alrededor del conteo.</param>
    private static bool MencionaSesiones(string recorte) =>
        recorte.Contains("TextoDeAvisoDeCierre", StringComparison.Ordinal)
        || recorte.Contains("TituloDeVentana", StringComparison.Ordinal);

    [GeneratedRegex(@"_sesiones\.Items\.Count")]
    private static partial Regex LineasQueCuentan();
}
