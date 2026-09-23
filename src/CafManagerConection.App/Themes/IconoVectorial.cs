using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Themes;

/// <summary>Dibuja un icono del catálogo en el tamaño y el color que le pidan.</summary>
/// <remarks>
/// Quien lo usa da una clave del catálogo, no un recurso ni un archivo. Si va por trazo o por
/// relleno lo decide el icono: contornear una silueta maciza la convierte en una mancha, y rellenar
/// un icono de trazo tapa el dibujo.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class IconoVectorial : FrameworkElement
{
    /// <summary>Hasta este tamaño el trazo es más fino; de acá en adelante, el grueso.</summary>
    private const double TamanoDelTrazoFino = 20;

    private const double GrosorFino = 1.5;
    private const double GrosorGrueso = 2.0;

    /// <summary>Una silueta maciza pesa más que un contorno del mismo alto, así que se dibuja más chica.</summary>
    private const double ProporcionDeLaSilueta = 0.88;

    private const double TamanoPorOmision = 16;

    public static readonly DependencyProperty ClaveProperty = DependencyProperty.Register(
        nameof(Clave),
        typeof(string),
        typeof(IconoVectorial),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TamanoProperty = DependencyProperty.Register(
        nameof(Tamano),
        typeof(double),
        typeof(IconoVectorial),
        new FrameworkPropertyMetadata(
            TamanoPorOmision,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PincelProperty = DependencyProperty.Register(
        nameof(Pincel),
        typeof(Brush),
        typeof(IconoVectorial),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Clave del catálogo. Una que no exista dibuja el icono de desconocido.</summary>
    public string? Clave
    {
        get => (string?)GetValue(ClaveProperty);
        set => SetValue(ClaveProperty, value);
    }

    /// <summary>Lado de la caja, en unidades independientes del dispositivo.</summary>
    public double Tamano
    {
        get => (double)GetValue(TamanoProperty);
        set => SetValue(TamanoProperty, value);
    }

    /// <summary>Con qué se pinta. Sin pincel usa el color de texto del tema.</summary>
    public Brush? Pincel
    {
        get => (Brush?)GetValue(PincelProperty);
        set => SetValue(PincelProperty, value);
    }

    /// <summary>Grosor del trazo que le toca a un tamaño.</summary>
    /// <param name="tamano">Lado de la caja.</param>
    public static double GrosorDelTrazo(double tamano) =>
        tamano <= TamanoDelTrazoFino ? GrosorFino : GrosorGrueso;

    /// <summary>El lado que pide el control: siempre cuadrado y del tamaño que le fijaron.</summary>
    /// <param name="disponible">Lo que el contenedor ofrece, que acá no cambia nada.</param>
    protected override Size MeasureOverride(Size disponible) => new(Tamano, Tamano);

    /// <summary>Dibuja el icono centrado en la caja que le tocó.</summary>
    /// <param name="dibujo">Contexto sobre el que se pinta.</param>
    protected override void OnRender(DrawingContext dibujo)
    {
        // Sin clave no se dibuja nada: es lo que significaba un Data nulo en el Path que había antes.
        if (string.IsNullOrEmpty(Clave) || Tamano <= 0)
        {
            return;
        }

        var icono = CatalogoDeIconos.Resolver(Clave)
                    ?? CatalogoDeIconos.Resolver(CatalogoDeIconos.ClaveDesconocido);

        if (icono is null)
        {
            return;
        }

        if (BuscarGeometria(icono.ClaveDeRecurso) is not { } geometria)
        {
            return;
        }

        var pincel = Pincel ?? (Brush?)TryFindResource("Texto") ?? Brushes.Black;

        if (icono.Modo == ModoDePintado.Relleno)
        {
            dibujo.DrawGeometry(pincel, null, Encajada(geometria, icono.Lienzo, ProporcionDeLaSilueta));
            return;
        }

        var lapiz = new Pen(pincel, GrosorDelTrazo(Tamano))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        lapiz.Freeze();

        dibujo.DrawGeometry(null, lapiz, Encajada(geometria, icono.Lienzo, 1.0));
    }

    /// <summary>La geometría llevada del lienzo del SVG a la caja del control, centrada.</summary>
    /// <param name="original">Geometría tal como la declara el diccionario generado.</param>
    /// <param name="lienzo">Lado del lienzo en el que está dibujada.</param>
    /// <param name="proporcion">Cuánto del tamaño ocupa.</param>
    /// <remarks>Se centra contra <c>RenderSize</c> y no contra el tamaño: un contenedor que estire
    /// el control lo arregla más grande, y ahí dibujar desde el origen lo pega a la esquina.</remarks>
    private Geometry Encajada(Geometry original, double lienzo, double proporcion)
    {
        var escala = Tamano * proporcion / lienzo;
        var dibujado = lienzo * escala;

        var izquierda = Math.Max(0, (RenderSize.Width - dibujado) / 2);
        var arriba = Math.Max(0, (RenderSize.Height - dibujado) / 2);

        var encajada = original.Clone();
        encajada.Transform = new MatrixTransform(escala, 0, 0, escala, izquierda, arriba);
        encajada.Freeze();

        return encajada;
    }

    private Geometry? BuscarGeometria(string claveDeRecurso) =>
        TryFindResource(claveDeRecurso) as Geometry;
}
