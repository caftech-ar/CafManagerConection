using System.Xml.Linq;

namespace CafManagerConection.Domain.Tests;

// El estilo implícito de TextBox fija el alto de un control de una línea. Un campo pensado para
// mostrar varias —con barra de desplazamiento vertical o que acepta Enter— tiene que soltarlo, o
// el contenido entra en 32 píxeles y parece que no se cargó.
public sealed class CamposDeTextoConAltoPropioTests
{
    private const string Multilinea = "CampoMultilinea";

    public static TheoryData<string> Vistas()
    {
        var datos = new TheoryData<string>();
        var raiz = Repositorio.Raiz();

        foreach (var archivo in Repositorio.ArchivosDe("CafManagerConection.App", "*.xaml")
                     .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}",
                         StringComparison.Ordinal)))
        {
            datos.Add(Path.GetRelativePath(raiz, archivo));
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Vistas))]
    public void Un_campo_de_varias_lineas_declara_su_alto(string relativa)
    {
        var documento = XDocument.Load(Path.Combine(Repositorio.Raiz(), relativa));

        foreach (var campo in documento.Descendants()
                     .Where(e => e.Name.LocalName is "TextBox" or "RichTextBox"))
        {
            if (!EsDeVariasLineas(campo) || SueltaElAlto(campo))
            {
                continue;
            }

            Assert.Fail(
                $"{relativa}: el campo «{Nombre(campo)}» muestra varias líneas pero hereda el alto "
                + $"fijo del estilo de TextBox. Poné Height=\"Auto\" o el estilo {Multilinea}.");
        }
    }

    private static bool EsDeVariasLineas(XElement campo) =>
        Atributo(campo, "VerticalScrollBarVisibility") is not null
        || string.Equals(Atributo(campo, "AcceptsReturn"), "True", StringComparison.OrdinalIgnoreCase);

    private static bool SueltaElAlto(XElement campo) =>
        // RichTextBox no toma el estilo implícito de TextBox, así que no arrastra el alto.
        campo.Name.LocalName == "RichTextBox"
        || string.Equals(Atributo(campo, "Height"), "Auto", StringComparison.OrdinalIgnoreCase)
        || Atributo(campo, "Style")?.Contains(Multilinea, StringComparison.Ordinal) == true;

    private static string? Atributo(XElement campo, string nombre) =>
        campo.Attribute(nombre)?.Value;

    private static string Nombre(XElement campo) =>
        campo.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value
        ?? campo.Name.LocalName;
}
