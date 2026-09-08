using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Tests;

// Las validaciones de unicidad viven en el dominio y no sólo en el índice de la base: un
// índice único de SQLite no da un mensaje que le sirva a nadie.
public sealed class CatalogoDeEtiquetasTests
{
    private static CatalogoDeEtiquetas ConLasCuatro()
    {
        var catalogo = new CatalogoDeEtiquetas();

        catalogo.Agregar("PRD", "Producción", "rojo");
        catalogo.Agregar("PRE", "PreProducción", "ambar");
        catalogo.Agregar("CAP", "Capacitación", "cyan");
        catalogo.Agregar("DEV", "Desarrollo", "verde");

        return catalogo;
    }

    [Fact]
    public void El_codigo_queda_en_mayusculas()
    {
        var etiqueta = new Etiqueta(Guid.NewGuid(), "prd", "Producción", "rojo");

        Assert.Equal("PRD", etiqueta.Codigo);
    }

    [Fact]
    public void Se_recortan_los_espacios()
    {
        var etiqueta = new Etiqueta(Guid.NewGuid(), "  prd  ", "  Producción  ", "rojo");

        Assert.Equal("PRD", etiqueta.Codigo);
        Assert.Equal("Producción", etiqueta.Nombre);
    }

    // El código se corta al largo que entra en la sigla del árbol.
    [Fact]
    public void Un_codigo_largo_se_corta()
    {
        var etiqueta = new Etiqueta(Guid.NewGuid(), "PRODUCCIONGRANDE", "x", "rojo");

        Assert.Equal(Etiqueta.LargoMaximoDeCodigo, etiqueta.Codigo.Length);
    }

    [Fact]
    public void El_pincel_sale_del_color()
    {
        var etiqueta = new Etiqueta(Guid.NewGuid(), "PRD", "Producción", "rojo");

        Assert.Equal("IconoRojo", etiqueta.ClaveDePincel);
    }

    [Fact]
    public void Un_color_que_no_es_de_la_paleta_no_es_valido()
    {
        var etiqueta = new Etiqueta(Guid.NewGuid(), "PRD", "Producción", "#FF0000");

        Assert.False(etiqueta.EsValida);
    }

    [Fact]
    public void Los_cuatro_iniciales_entran()
    {
        var catalogo = ConLasCuatro();

        Assert.Equal(4, catalogo.Todas.Count);
        Assert.Equal(
            ["Producción", "PreProducción", "Capacitación", "Desarrollo"],
            catalogo.Todas.Select(e => e.Nombre));
    }

    [Fact]
    public void Se_ordenan_por_el_orden_que_se_les_dio()
    {
        var catalogo = ConLasCuatro();

        Assert.Equal([1, 2, 3, 4], catalogo.Todas.Select(e => e.Orden));
    }

    [Fact]
    public void No_entran_dos_con_el_mismo_codigo()
    {
        var catalogo = ConLasCuatro();

        Assert.Null(catalogo.Agregar("PRD", "Otro nombre", "azul"));
        Assert.Equal(4, catalogo.Todas.Count);
    }

    [Fact]
    public void El_codigo_repetido_no_distingue_mayusculas()
    {
        var catalogo = ConLasCuatro();

        Assert.NotNull(catalogo.PorQueNo("prd", "Otro", "azul"));
    }

    [Fact]
    public void No_entran_dos_con_el_mismo_nombre()
    {
        var catalogo = ConLasCuatro();

        Assert.Null(catalogo.Agregar("XXX", "producción", "azul"));
    }

    [Fact]
    public void El_motivo_dice_cual_es_el_problema()
    {
        var catalogo = ConLasCuatro();

        Assert.Contains("PRD", catalogo.PorQueNo("PRD", "Otro", "azul")!, StringComparison.Ordinal);
        Assert.Contains("vacío", catalogo.PorQueNo("", "Otro", "azul")!, StringComparison.Ordinal);
        Assert.Contains("color", catalogo.PorQueNo("XX", "Otro", "fucsia")!, StringComparison.Ordinal);
    }

    // Caso que rompe una validación de unicidad ingenua: guardar sin cambiar el código
    // chocaría consigo misma.
    [Fact]
    public void Guardar_sin_cambiar_el_codigo_no_choca_consigo_misma()
    {
        var catalogo = ConLasCuatro();
        var prd = catalogo.Todas.First(e => e.Codigo == "PRD");

        Assert.True(catalogo.Actualizar(prd.Id, "PRD", "Producción", "naranja"));
        Assert.Equal("naranja", catalogo.Por(prd.Id)!.ClaveDeColor);
    }

    [Fact]
    public void No_se_puede_cambiar_a_un_codigo_que_ya_esta()
    {
        var catalogo = ConLasCuatro();
        var dev = catalogo.Todas.First(e => e.Codigo == "DEV");

        Assert.False(catalogo.Actualizar(dev.Id, "PRD", "Desarrollo", "verde"));
        Assert.Equal("DEV", catalogo.Por(dev.Id)!.Codigo);
    }

    [Fact]
    public void Actualizar_una_que_no_esta_no_la_agrega()
    {
        var catalogo = ConLasCuatro();

        Assert.False(catalogo.Actualizar(Guid.NewGuid(), "XXX", "Nueva", "azul"));
        Assert.Equal(4, catalogo.Todas.Count);
    }

    [Fact]
    public void Quitar_saca_solo_esa()
    {
        var catalogo = ConLasCuatro();
        var cap = catalogo.Todas.First(e => e.Codigo == "CAP");

        Assert.True(catalogo.Quitar(cap.Id));
        Assert.Equal(3, catalogo.Todas.Count);
        Assert.Null(catalogo.Por(cap.Id));
    }

    // Si no se pudiera, borrar una etiqueta dejaría su código inutilizable para siempre.
    [Fact]
    public void El_codigo_de_una_borrada_se_puede_reusar()
    {
        var catalogo = ConLasCuatro();

        catalogo.Quitar(catalogo.Todas.First(e => e.Codigo == "CAP").Id);

        Assert.NotNull(catalogo.Agregar("CAP", "Capacitaciones", "cyan"));
    }

    [Fact]
    public void Quitar_algo_que_no_esta_no_es_un_error() =>
        Assert.False(new CatalogoDeEtiquetas().Quitar(Guid.NewGuid()));

    [Fact]
    public void Una_etiqueta_nueva_va_al_final()
    {
        var catalogo = ConLasCuatro();
        var nueva = catalogo.Agregar("QA", "Testing", "violeta");

        Assert.NotNull(nueva);
        Assert.Equal(5, nueva.Orden);
    }

    [Fact]
    public void El_catalogo_vacio_admite_la_primera()
    {
        var catalogo = new CatalogoDeEtiquetas();
        var primera = catalogo.Agregar("PRD", "Producción", "rojo");

        Assert.NotNull(primera);
        Assert.Equal(1, primera.Orden);
    }
}
