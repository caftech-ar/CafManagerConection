using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// El mismo error apareció tres veces en tres ventanas distintas, siempre descubierto mirando la
// pantalla: WPF no enlaza contra los miembros de un tipo no público y no avisa -sin excepción,
// sin registro, la ventana abre y las columnas salen en blanco-.
// Se lee el texto de los archivos en vez de reflexionar sobre el ensamblado porque el proyecto
// de la aplicación es de Windows y este proyecto de pruebas no lo referencia.
public sealed class TiposEnlazablesTests
{
    private static readonly string[] Convenciones = ["Fila", "Opcion", "Item", "Nodo", "Entrada"];

    [Fact]
    public void Ningun_tipo_de_fila_o_de_opcion_se_declara_sin_ser_publico()
    {
        var patron = new Regex(
            @"^\s*(?:private|internal)\s+(?:sealed\s+)?(?:record|class)\s+(\w+)",
            RegexOptions.Multiline);

        var culpables = new List<string>();

        foreach (var archivo in Repositorio.ArchivosDe("CafManagerConection.App", "*.cs"))
        {
            foreach (Match m in patron.Matches(File.ReadAllText(archivo)))
            {
                var tipo = m.Groups[1].Value;

                if (Convenciones.Any(c => tipo.StartsWith(c, StringComparison.Ordinal)))
                {
                    culpables.Add($"{Path.GetFileName(archivo)}: {tipo}");
                }
            }
        }

        Assert.True(
            culpables.Count == 0,
            "WPF no puede enlazar contra estos tipos y va a fallar en silencio, dejando la lista "
            + "en blanco. Hacelos públicos:\n  " + string.Join("\n  ", culpables));
    }
}
