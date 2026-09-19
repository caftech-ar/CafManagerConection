using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// Las ventanas WPF no se pueden instanciar en la suite —piden una Application con los diccionarios
// de estilos cargados—, así que lo que se vigila es lo que se olvida: marcar como destructiva una
// confirmación que borra. Sin la marca, Enter la acepta.
public sealed partial class ConfirmacionesDestructivasTests
{
    private static readonly string[] VerbosQueDestruyen =
        ["Eliminar", "Borrar", "Sobrescribir", "Descartar"];

    public static TheoryData<string> Archivos()
    {
        var datos = new TheoryData<string>();
        var raiz = Repositorio.Raiz();

        foreach (var archivo in Repositorio.ArchivosDe("CafManagerConection.App", "*.cs"))
        {
            datos.Add(Path.GetRelativePath(raiz, archivo));
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Toda_confirmacion_de_borrado_se_marca_como_destructiva(string relativa)
    {
        var texto = File.ReadAllText(Path.Combine(Repositorio.Raiz(), relativa));

        foreach (var llamada in Llamadas(texto))
        {
            var verbo = VerbosQueDestruyen.FirstOrDefault(
                v => llamada.Contains(Entrecomillado(v), StringComparison.Ordinal));

            if (verbo is null)
            {
                continue;
            }

            Assert.True(
                llamada.Contains("destructivo: true", StringComparison.Ordinal),
                $"{relativa}: la confirmación con el verbo «{verbo}» no se marcó como destructiva, "
                + "así que Enter la acepta.");
        }
    }

    [Fact]
    public void El_guardian_reconoce_una_confirmacion_sin_marcar()
    {
        const string sinMarcar = """
            if (!Dialogos.Confirmar(this, "Eliminar conexión", aviso, "Eliminar"))
            """;

        var llamada = Assert.Single(Llamadas(sinMarcar));

        Assert.Contains(Entrecomillado("Eliminar"), llamada, StringComparison.Ordinal);
        Assert.DoesNotContain("destructivo: true", llamada, StringComparison.Ordinal);
    }

    [Fact]
    public void El_guardian_acepta_una_confirmacion_marcada()
    {
        const string marcada = """
            if (!Dialogos.Confirmar(this, "Eliminar conexión", aviso, "Eliminar", destructivo: true))
            """;

        var llamada = Assert.Single(Llamadas(marcada));

        Assert.Contains("destructivo: true", llamada, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_llamada_con_parentesis_adentro_se_lee_entera()
    {
        const string anidada = """
            Dialogos.Confirmar(this, Titulo(nodo), Detalle(nodo, 2), "Eliminar", destructivo: true)
            """;

        var llamada = Assert.Single(Llamadas(anidada));

        Assert.Contains("destructivo: true", llamada, StringComparison.Ordinal);
    }

    private static string Entrecomillado(string verbo) => $"\"{verbo}\"";

    /// <summary>Cada llamada a Confirmar con sus argumentos, aunque ocupe varias líneas.</summary>
    /// <param name="texto">Contenido a revisar.</param>
    private static IEnumerable<string> Llamadas(string texto)
    {
        foreach (Match inicio in NombreDeLaLlamada().Matches(texto))
        {
            var desde = inicio.Index + inicio.Length;
            var profundidad = 1;

            for (var i = desde; i < texto.Length && profundidad > 0; i++)
            {
                profundidad += texto[i] switch
                {
                    '(' => 1,
                    ')' => -1,
                    _ => 0,
                };

                if (profundidad == 0)
                {
                    yield return texto[desde..i];
                }
            }
        }
    }

    [GeneratedRegex(@"\bConfirmar\s*\(")]
    private static partial Regex NombreDeLaLlamada();
}
