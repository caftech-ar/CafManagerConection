using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace CafManagerConection.App.Views;

/// <summary>Línea que se dibuja sobre una fila del árbol para marcar entre qué dos va a caer lo que se está arrastrando.</summary>
[SupportedOSPlatform("windows")]
internal sealed class LineaDeInsercion : Adorner
{
    private const double Grosor = 2;

    private readonly bool _abajo;
    private readonly Brush _pincel;

    public LineaDeInsercion(UIElement adornado, bool abajo)
        : base(adornado)
    {
        _abajo = abajo;
        IsHitTestVisible = false;

        _pincel = Application.Current?.TryFindResource("Primario") as Brush ?? Brushes.DodgerBlue;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var ancho = AdornedElement.RenderSize.Width;
        var alto = AdornedElement.RenderSize.Height;

        var y = _abajo ? alto - (Grosor / 2) : Grosor / 2;

        drawingContext.DrawLine(
            new Pen(_pincel, Grosor), new Point(0, y), new Point(ancho, y));
    }
}
