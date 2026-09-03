using System.Drawing;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

/// <summary>Qué columnas de cada fila entran en la selección (FR-154, FR-154d).</summary>
public sealed class SeleccionRectangularTests
{
    private const int UltimaColumna = 79;

    private static (int Desde, int Hasta)? Tramo(
        int fila, Point inicio, Point fin, bool rectangular = false) =>
        TerminalControl.TramoDeFila(fila, inicio, fin, UltimaColumna, rectangular);

    [Fact]
    public void Fuera_del_rango_de_filas_no_hay_tramo()
    {
        Assert.Null(Tramo(1, new Point(10, 2), new Point(20, 4)));
        Assert.Null(Tramo(5, new Point(10, 2), new Point(20, 4)));
    }

    [Fact]
    public void En_una_sola_fila_el_tramo_va_de_punta_a_punta_de_lo_marcado() =>
        Assert.Equal((10, 20), Tramo(3, new Point(10, 3), new Point(20, 3)));

    [Theory]
    [InlineData(2, 10, UltimaColumna)]
    [InlineData(3, 0, UltimaColumna)]
    [InlineData(4, 0, 20)]
    public void En_varias_filas_solo_se_recortan_los_extremos(int fila, int desde, int hasta) =>
        Assert.Equal((desde, hasta), Tramo(fila, new Point(10, 2), new Point(20, 4)));

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void El_rectangulo_toma_las_mismas_columnas_en_todas_las_filas(int fila) =>
        Assert.Equal((10, 20), Tramo(fila, new Point(10, 2), new Point(20, 4), rectangular: true));

    [Fact]
    public void El_rectangulo_se_ordena_aunque_se_arrastre_hacia_la_izquierda() =>
        Assert.Equal((10, 20), Tramo(3, new Point(20, 2), new Point(10, 4), rectangular: true));

    [Fact]
    public void El_rectangulo_tambien_respeta_el_rango_de_filas() =>
        Assert.Null(Tramo(9, new Point(10, 2), new Point(20, 4), rectangular: true));

    [Fact]
    public void El_rectangulo_puede_tener_una_sola_columna() =>
        Assert.Equal((7, 7), Tramo(3, new Point(7, 1), new Point(7, 6), rectangular: true));
}
