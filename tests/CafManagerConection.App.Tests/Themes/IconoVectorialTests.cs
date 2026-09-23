using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using CafManagerConection.App.Themes;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Tests.Themes;

public sealed class IconoVectorialTests
{
    [Theory]
    [InlineData(16, 1.5)]
    [InlineData(20, 1.5)]
    [InlineData(24, 2.0)]
    [InlineData(32, 2.0)]
    [InlineData(48, 2.0)]
    [InlineData(64, 2.0)]
    public void El_grosor_del_trazo_cambia_a_los_veinte_puntos(double tamano, double esperado) =>
        Assert.Equal(esperado, IconoVectorial.GrosorDelTrazo(tamano));

    [Fact]
    public void La_caja_mide_lo_que_dice_el_tamano() => EnSta(() =>
    {
        var icono = Armar("server", 24);

        icono.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Assert.Equal(new Size(24, 24), icono.DesiredSize);
    });

    [Fact]
    public void Un_icono_de_trazo_se_dibuja_con_lapiz_y_sin_relleno() => EnSta(() =>
    {
        var dibujo = Unico("server", 24);

        Assert.Null(dibujo.Brush);
        Assert.NotNull(dibujo.Pen);
        Assert.Equal(PenLineCap.Round, dibujo.Pen.StartLineCap);
        Assert.Equal(PenLineJoin.Round, dibujo.Pen.LineJoin);
        Assert.Equal(2.0, dibujo.Pen.Thickness);
    });

    [Fact]
    public void Un_icono_de_relleno_se_dibuja_con_pincel_y_sin_lapiz() => EnSta(() =>
    {
        var dibujo = Unico("postgresql", 24);

        Assert.NotNull(dibujo.Brush);
        Assert.Null(dibujo.Pen);
    });

    // Una silueta maciza pesa más que un contorno del mismo alto: se dibuja al 88 % y centrada, así
    // que le queda un margen del 6 % de la caja por lado.
    [Theory]
    [InlineData("linux")]
    [InlineData("postgresql")]
    [InlineData("redis")]
    public void Una_silueta_maciza_se_dibuja_al_ochenta_y_ocho_por_ciento(string clave) => EnSta(() =>
    {
        const double caja = 64;
        const double margen = caja * 0.06;

        var limites = Unico(clave, caja).Geometry.Bounds;

        Assert.InRange(limites.Left, margen - 0.01, caja);
        Assert.InRange(limites.Right, 0, caja - margen + 0.01);
        Assert.InRange(limites.Top, margen - 0.01, caja);
        Assert.InRange(limites.Bottom, 0, caja - margen + 0.01);

        Assert.True(
            limites.Width > caja * 0.6,
            $"«{clave}» quedó de {limites.Width} en una caja de {caja}: no se está escalando al lienzo.");
    });

    // Un contorno se lleva al lienzo entero, sin margen propio: «arrows-maximize» va de 4 a 20 en un
    // lienzo de 24, así que a 64 puntos tiene que caer justo en 10,67 y 53,33.
    [Fact]
    public void Un_contorno_se_escala_al_lienzo_sin_margen() => EnSta(() =>
    {
        var limites = Unico("arrows-maximize", 64).Geometry.Bounds;
        var escala = 64 / 24d;

        Assert.Equal(4 * escala, limites.Left, 2);
        Assert.Equal(4 * escala, limites.Top, 2);
        Assert.Equal(20 * escala, limites.Right, 2);
        Assert.Equal(20 * escala, limites.Bottom, 2);
    });

    [Theory]
    [InlineData("server", 24)]
    [InlineData("postgresql", 24)]
    [InlineData("oracle-original", 128)]
    public void La_geometria_entra_en_la_caja_venga_del_lienzo_que_venga(string clave, double lienzo) =>
        EnSta(() =>
        {
            Assert.Equal(lienzo, CatalogoDeIconos.Resolver(clave)!.Lienzo);

            var limites = Unico(clave, 32).Geometry.Bounds;

            Assert.InRange(limites.Left, -0.01, 32);
            Assert.InRange(limites.Top, -0.01, 32);
            Assert.InRange(limites.Right, 0, 32.01);
            Assert.InRange(limites.Bottom, 0, 32.01);
        });

    [Fact]
    public void Una_clave_desconocida_dibuja_el_icono_de_desconocido() => EnSta(() =>
    {
        var desconocida = Unico("no-existe-este-icono", 32).Geometry.Bounds;
        var esperada = Unico(CatalogoDeIconos.ClaveDesconocido, 32).Geometry.Bounds;

        Assert.Equal(esperada, desconocida);
    });

    // Un Data nulo en el Path que había antes dejaba la celda limpia; sin clave se hace lo mismo.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_clave_no_dibuja_nada(string? clave) =>
        EnSta(() => Assert.Empty(Recolectar(Armar(clave, 24))));

    [Fact]
    public void Un_tamano_de_cero_no_dibuja_nada() =>
        EnSta(() => Assert.Empty(Recolectar(Armar("server", 0))));

    [Fact]
    public void Todo_el_catalogo_se_dibuja_sin_romperse() => EnSta(() =>
    {
        foreach (var icono in CatalogoDeIconos.Iconos)
        {
            var dibujos = Recolectar(Armar(icono.Clave, 16));

            Assert.True(dibujos.Count == 1, $"«{icono.Clave}» no se dibujó.");
        }
    });

    private static GeometryDrawing Unico(string? clave, double tamano)
    {
        var dibujos = Recolectar(Armar(clave, tamano));

        Assert.Single(dibujos);

        return dibujos[0];
    }

    private static IconoVectorial Armar(string? clave, double tamano)
    {
        var icono = new IconoVectorial { Clave = clave, Tamano = tamano };

        foreach (var diccionario in Diccionarios.Value)
        {
            icono.Resources.MergedDictionaries.Add(diccionario);
        }

        return icono;
    }

    // OnRender es protegido y el control no está en un árbol visual, así que se lo llama con un
    // DrawingVisual propio y se lee el DrawingGroup que quedó.
    private static List<GeometryDrawing> Recolectar(IconoVectorial icono)
    {
        // Sin arreglar, RenderSize es cero y el control no sabe dónde centrar el dibujo.
        icono.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        icono.Arrange(new Rect(icono.DesiredSize));

        var visual = new DrawingVisual();

        using (var contexto = visual.RenderOpen())
        {
            typeof(IconoVectorial)
                .GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(icono, [contexto]);
        }

        return Aplanar(visual.Drawing).ToList();
    }

    private static IEnumerable<GeometryDrawing> Aplanar(Drawing? dibujo)
    {
        switch (dibujo)
        {
            case GeometryDrawing geometria:
                yield return geometria;
                break;

            case DrawingGroup grupo:
                foreach (var hijo in grupo.Children.SelectMany(Aplanar))
                {
                    yield return hijo;
                }

                break;
        }
    }

    // Los diccionarios se leen del disco y se cuelgan del propio control: así la prueba no necesita
    // una Application con los recursos de App.xaml cargados.
    private static readonly Lazy<IReadOnlyList<ResourceDictionary>> Diccionarios = new(() =>
        Directory
            .EnumerateFiles(
                Path.Combine(RaizDelRepositorio(), "src", "CafManagerConection.App", "Themes"),
                "Iconos.*.xaml")
            .Select(a => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(a)))
            .ToList());

    // WPF exige STA, y el repositorio ya lo resuelve así en las pruebas del control de RDP.
    private static void EnSta(Action prueba)
    {
        Exception? falla = null;

        var hilo = new Thread(() =>
        {
            try
            {
                prueba();
            }
            catch (Exception ex)
            {
                falla = ex;
            }
        });

        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        hilo.Join();

        if (falla is not null)
        {
            throw new Xunit.Sdk.XunitException(falla.ToString());
        }
    }

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
