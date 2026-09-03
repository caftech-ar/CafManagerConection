using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests.Settings;

// Si la geometría guardada sigue cayendo dentro de algún monitor (FR-047). El síntoma de
// tenerlo mal: la ventana arranca, no falla, no registra nada raro y no se ve por ningún lado,
// típicamente al cerrarla en un monitor que después se desconecta.
public sealed class GeometriaDeVentanaTests
{
    private static readonly AreaDePantalla Principal = new(0, 0, 1920, 1080);

    private static readonly AreaDePantalla Secundario = new(-1920, 0, 1920, 1080);

    [Fact]
    public void Una_ventana_dentro_del_monitor_principal_es_visible()
    {
        var g = new WindowPlacement(100, 100, 1280, 800, false);

        Assert.True(g.EsVisibleEn([Principal]));
    }

    [Fact]
    public void Una_ventana_en_un_monitor_desconectado_no_es_visible()
    {
        var g = new WindowPlacement(-1500, 200, 1280, 800, false);

        Assert.False(g.EsVisibleEn([Principal]));
    }

    [Fact]
    public void Con_el_segundo_monitor_conectado_vuelve_a_ser_visible()
    {
        var g = new WindowPlacement(-1500, 200, 1280, 800, false);

        Assert.True(g.EsVisibleEn([Principal, Secundario]));
    }

    [Fact]
    public void Alcanza_con_que_asome_por_un_borde()
    {
        // Reubicar una ventana que asoma contradiría a quien la dejó ahí: se puede agarrar y
        // arrastrar de vuelta sin ayuda.
        var g = new WindowPlacement(1900, 500, 1280, 800, false);

        Assert.True(g.EsVisibleEn([Principal]));
    }

    [Fact]
    public void Pegada_al_borde_por_fuera_no_cuenta()
    {
        var g = new WindowPlacement(1920, 0, 1280, 800, false);

        Assert.False(g.EsVisibleEn([Principal]));
    }

    [Fact]
    public void Sin_monitores_nada_es_visible()
    {
        var g = new WindowPlacement(0, 0, 1280, 800, false);

        Assert.False(g.EsVisibleEn([]));
    }

    [Theory]
    [InlineData(0, 800)]
    [InlineData(1280, 0)]
    [InlineData(-100, -100)]
    public void Una_ventana_sin_superficie_no_es_visible(int ancho, int alto)
    {
        // Un tamaño en cero o negativo llegaría de una preferencia corrupta; sin esta guarda el
        // solapamiento daría verdadero y la ventana se restauraría con tamaño inválido.
        var g = new WindowPlacement(100, 100, ancho, alto, false);

        Assert.False(g.EsVisibleEn([Principal]));
    }

    [Fact]
    public void La_geometria_por_omision_es_visible_en_una_pantalla_comun()
    {
        Assert.True(WindowPlacement.Default.EsVisibleEn([Principal]));
    }
}
