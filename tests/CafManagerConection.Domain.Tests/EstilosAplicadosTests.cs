using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// WPF rechaza un Style cuyo TargetType no coincide con el elemento recién al cargar el XAML, sin
// error de compilación: un ToggleButton con el estilo BotonTenue (TargetType="Button") dejó el
// panel de estado del servidor sin abrirse nunca.
// Se lee el XAML como texto y no se carga con WPF, igual que PincelesDeLaPaletaTests: este
// proyecto no depende de WPF.
public sealed class EstilosAplicadosTests
{
    private static readonly Dictionary<string, string> Padre = new(StringComparer.Ordinal)
    {
        ["Button"] = "ButtonBase",
        ["ToggleButton"] = "ButtonBase",
        ["RepeatButton"] = "ButtonBase",
        ["CheckBox"] = "ToggleButton",
        ["RadioButton"] = "ToggleButton",
        ["ButtonBase"] = "Control",
        ["TextBox"] = "Control",
        ["ComboBox"] = "Control",
        ["DataGrid"] = "Control",
        ["ListBox"] = "Control",
        ["TabControl"] = "Control",
        ["Control"] = "FrameworkElement",
        ["TextBlock"] = "FrameworkElement",
        ["Border"] = "FrameworkElement",
    };

    private static readonly Regex Estilos = new(
        @"<Style\s+((?:[^<>""]|""[^""]*"")*?)>", RegexOptions.Singleline);

    private static readonly Regex Elementos = new(
        @"<([A-Za-z]+)\b((?:[^<>""]|""[^""]*"")*?)/?>", RegexOptions.Singleline);

    // El negativo de antes de «Style» es lo que evita confundir CellStyle, RowStyle,
    // ItemContainerStyle y compañía con el Style del propio elemento: esos apuntan a otro tipo a
    // propósito, y sin el negativo esta prueba fallaría sobre código correcto.
    private static readonly Regex EstiloAplicado = new(
        @"(?<![A-Za-z])Style=""\{StaticResource\s+([^}]+)\}""");

    private static readonly Regex ClaveDeEstilo = new(@"x:Key=""([^""]+)""");

    private static readonly Regex TipoDestino = new(
        @"TargetType=""(?:\{x:Type\s+)?([A-Za-z]+)\}?""");

    [Fact]
    public void Ningun_estilo_se_aplica_a_un_elemento_que_no_acepta_su_TargetType()
    {
        var archivos = Repositorio.ArchivosDe("CafManagerConection.App", "*.xaml").ToList();

        Assert.NotEmpty(archivos);

        var destinos = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var contenido in archivos.Select(File.ReadAllText))
        {
            foreach (var estilo in Estilos.Matches(contenido).Cast<Match>())
            {
                var atributos = estilo.Groups[1].Value;
                var clave = ClaveDeEstilo.Match(atributos);
                var destino = TipoDestino.Match(atributos);

                if (clave.Success && destino.Success)
                {
                    destinos[clave.Groups[1].Value] = destino.Groups[1].Value;
                }
            }
        }

        Assert.NotEmpty(destinos);

        var problemas = new List<string>();

        foreach (var archivo in archivos)
        {
            var contenido = File.ReadAllText(archivo);

            foreach (var elemento in Elementos.Matches(contenido).Cast<Match>())
            {
                var tipo = elemento.Groups[1].Value;
                var aplicado = EstiloAplicado.Match(elemento.Groups[2].Value);

                if (!aplicado.Success
                    || !destinos.TryGetValue(aplicado.Groups[1].Value.Trim(), out var destino)
                    || EsCompatible(tipo, destino))
                {
                    continue;
                }

                var linea = contenido[..elemento.Index].Count(c => c == '\n') + 1;

                problemas.Add(
                    $"{Path.GetFileName(archivo)}:{linea}: <{tipo}> usa el estilo "
                    + $"«{aplicado.Groups[1].Value.Trim()}», cuyo TargetType es {destino}.");
            }
        }

        Assert.Empty(problemas);
    }

    // Un tipo que esta prueba no conoce se acepta: la alternativa es fallar sobre código
    // correcto cada vez que alguien usa un control nuevo.
    private static bool EsCompatible(string tipo, string destino)
    {
        if (!Padre.ContainsKey(tipo) || !Padre.ContainsKey(destino))
        {
            return true;
        }

        var actual = tipo;

        while (true)
        {
            if (string.Equals(actual, destino, StringComparison.Ordinal))
            {
                return true;
            }

            if (!Padre.TryGetValue(actual, out var siguiente))
            {
                return false;
            }

            actual = siguiente;
        }
    }

    // Sin esto, la comprobación de arriba pasaría igual con las expresiones regulares mal
    // escritas: no encontraría nada y no habría nada que reprochar.
    [Theory]
    [InlineData("ToggleButton", "Button", false)]
    [InlineData("Button", "ToggleButton", false)]
    [InlineData("Button", "Button", true)]
    [InlineData("Button", "ButtonBase", true)]
    [InlineData("ToggleButton", "ButtonBase", true)]
    [InlineData("CheckBox", "ToggleButton", true)]
    [InlineData("CheckBox", "ButtonBase", true)]
    [InlineData("TextBlock", "Button", false)]
    [InlineData("Popup", "Button", true)] // Tipo desconocido: se acepta a propósito.
    public void La_compatibilidad_de_tipos_es_la_que_dice_WPF(
        string tipo, string destino, bool esperado)
    {
        Assert.Equal(esperado, EsCompatible(tipo, destino));
    }
}
