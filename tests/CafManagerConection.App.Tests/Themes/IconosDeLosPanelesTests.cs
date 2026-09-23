using System.Reflection;
using System.Text.RegularExpressions;
using CafManagerConection.App.Themes;
using CafManagerConection.Domain.Monitoring;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Tests.Themes;

public sealed class IconosDeLosPanelesTests
{
    // Un emoji lo dibuja la fuente del sistema: no sigue el tema, no sigue el tamaño y no se puede
    // pintar con la paleta. El último que quedaba era el reloj del panel de inventario.
    private static readonly Regex Emoji = new(
        @"[⌚-⌛⏩-⏺◽-◾☀-➿⬀-⯿"
        + @"\uD83C-\uD83E][\uDC00-\uDFFF]?", RegexOptions.Compiled);

    [Fact]
    public void Ningun_texto_visible_de_la_interfaz_lleva_un_emoji()
    {
        var conEmoji = new List<string>();

        foreach (var archivo in Xaml())
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(archivo), @"Text=""(?<texto>[^""]*)"""))
            {
                if (Emoji.IsMatch(m.Groups["texto"].Value))
                {
                    conEmoji.Add($"{Path.GetFileName(archivo)}: «{m.Groups["texto"].Value}»");
                }
            }
        }

        Assert.True(conEmoji.Count == 0, "Esto tiene que salir del catálogo:"
            + Environment.NewLine + string.Join(Environment.NewLine, conEmoji));
    }

    [Fact]
    public void Toda_constante_de_la_interfaz_existe_en_el_catalogo()
    {
        foreach (var campo in Constantes())
        {
            var clave = (string)campo.GetRawConstantValue()!;

            Assert.True(
                CatalogoDeIconos.EsValido(clave),
                $"IconosDeLaInterfaz.{campo.Name} apunta a «{clave}», que el catálogo no tiene.");
        }
    }

    [Fact]
    public void Un_proceso_que_no_se_reconoce_lleva_el_generico_y_no_un_hueco()
    {
        Assert.Equal(IconosPorOmision.Desconocido, IconoDeProceso.ClaveDeIcono("un-binario-propio"));
        Assert.False(IconoDeProceso.EsConocido("un-binario-propio"));
    }

    // Se compara la clave y no el pincel resuelto: sin una Application los dos caen en el gris de
    // reserva de Pinceles.De y la prueba pasaría creyendo que compara algo.
    [Fact]
    public void Un_icono_reconocido_pide_un_pincel_distinto_del_generico()
    {
        Assert.NotEqual(
            PincelDelEnfasis.ClaveDelPincel(conocido: true),
            PincelDelEnfasis.ClaveDelPincel(conocido: false));

        Assert.Equal("Texto", PincelDelEnfasis.ClaveDelPincel(conocido: true));
        Assert.Equal("TextoTenue", PincelDelEnfasis.ClaveDelPincel(conocido: false));
    }

    // El panel de procesos colapsaba el icono cuando no reconocía, y el nombre saltaba a la
    // izquierda. Un control colapsado ocupa cero.
    [Fact]
    public void Ningun_icono_de_un_panel_se_colapsa_segun_lo_que_dibuja()
    {
        var conVisibilidad = new List<string>();

        foreach (var archivo in Xaml())
        {
            var texto = File.ReadAllText(archivo);

            foreach (Match m in Regex.Matches(
                         texto, @"<temas:IconoVectorial\b[^>]*?Visibility=""\{Binding[^>]*?>",
                         RegexOptions.Singleline))
            {
                conVisibilidad.Add($"{Path.GetFileName(archivo)}: {m.Value[..40]}…");
            }
        }

        Assert.True(conVisibilidad.Count == 0,
            "Un icono que se colapsa desarma la columna:" + Environment.NewLine
            + string.Join(Environment.NewLine, conVisibilidad));
    }

    [Fact]
    public void Ninguna_entrada_del_menu_del_arbol_queda_sin_icono()
    {
        var acciones = File.ReadAllText(Path.Combine(
            RaizDelRepositorio(), "src", "CafManagerConection.App", "Views", "MainWindow.Acciones.cs"));

        var sinIcono = Regex.Matches(acciones, @"Agregar\(""(?<texto>[^""]+)""(?<resto>.*?)\);",
                RegexOptions.Singleline)
            .Where(m => !m.Groups["resto"].Value.Contains("Iconos", StringComparison.Ordinal))
            .Select(m => m.Groups["texto"].Value)
            .ToList();

        Assert.True(sinIcono.Count == 0,
            "Estas entradas del menú no llevan icono: " + string.Join(", ", sinIcono));
    }

    [Fact]
    public void Los_tres_filtros_del_arbol_llevan_icono()
    {
        var ventana = File.ReadAllText(Path.Combine(
            RaizDelRepositorio(), "src", "CafManagerConection.App", "Views", "MainWindow.xaml"));

        var chips = Regex.Matches(ventana, @"<ToggleButton[^>]*?Style=""\{StaticResource ChipDeFiltro\}""(?<cuerpo>.*?)</ToggleButton>",
            RegexOptions.Singleline);

        Assert.Equal(3, chips.Count);
        Assert.All(chips, m => Assert.Contains("IconoVectorial", m.Groups["cuerpo"].Value, StringComparison.Ordinal));
    }

    // Con dos botones y su texto no hay nada que encontrar, y un tilde en Guardar competiría con el
    // de «correcto» del catálogo.
    [Theory]
    [InlineData("Guardar")]
    [InlineData("Cancelar")]
    [InlineData("Aceptar")]
    [InlineData("Cerrar")]
    public void Un_boton_de_dialogo_que_no_destruye_no_lleva_icono(string texto)
    {
        var conIcono = new List<string>();

        foreach (var archivo in Xaml())
        {
            foreach (Match m in Regex.Matches(
                         File.ReadAllText(archivo),
                         $@"<Button[^>]*?Content=""{texto}""[^>]*?>", RegexOptions.Singleline))
            {
                if (m.Value.Contains("BotonConIcono", StringComparison.Ordinal))
                {
                    conIcono.Add(Path.GetFileName(archivo));
                }
            }
        }

        Assert.True(conIcono.Count == 0,
            $"«{texto}» no lleva icono: " + string.Join(", ", conIcono));
    }

    [Fact]
    public void Todo_boton_con_icono_apunta_a_una_clave_del_catalogo()
    {
        var claves = new HashSet<string>(StringComparer.Ordinal);

        foreach (var archivo in Xaml())
        {
            foreach (Match m in Regex.Matches(
                         File.ReadAllText(archivo),
                         @"BotonConIcono\.Clave=""\{x:Static temas:IconosDeLaInterfaz\.(?<nombre>\w+)\}"""))
            {
                claves.Add(m.Groups["nombre"].Value);
            }
        }

        Assert.NotEmpty(claves);

        var declaradas = Constantes().ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

        foreach (var nombre in claves)
        {
            Assert.True(declaradas.ContainsKey(nombre), $"IconosDeLaInterfaz no declara «{nombre}».");
            Assert.True(CatalogoDeIconos.EsValido(declaradas[nombre]), nombre);
        }
    }

    private static IReadOnlyList<FieldInfo> Constantes() =>
        typeof(IconosDeLaInterfaz)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToList();

    private static IEnumerable<string> Xaml() =>
        Directory.EnumerateFiles(
                Path.Combine(RaizDelRepositorio(), "src", "CafManagerConection.App"),
                "*.xaml",
                SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                        && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));

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
