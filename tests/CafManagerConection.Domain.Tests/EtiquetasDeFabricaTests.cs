using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

public sealed class EtiquetasDeFabricaTests
{
    private static Etiqueta Etiqueta(string codigo, string nombre, string color, int orden) =>
        new(Guid.NewGuid(), codigo, nombre, color, orden);

    [Fact]
    public void El_catalogo_de_fabrica_incluye_QA()
    {
        Assert.Contains(EtiquetasDeFabrica.Crear(), e => e.Codigo == "QA");
        Assert.Equal(5, EtiquetasDeFabrica.Cantidad);
    }

    [Fact]
    public void Todas_las_de_fabrica_son_validas_y_usan_claves_de_la_paleta()
    {
        Assert.All(EtiquetasDeFabrica.Crear(), e =>
        {
            Assert.True(e.EsValida);
            Assert.True(PaletaIconos.EsValido(e.ClaveDeColor));
        });
    }

    [Fact]
    public void Ni_los_codigos_ni_los_nombres_ni_los_identificadores_se_repiten()
    {
        var codigos = EtiquetasDeFabrica.Crear().Select(e => e.Codigo).ToList();
        var nombres = EtiquetasDeFabrica.Crear().Select(e => e.Nombre).ToList();
        var ids = EtiquetasDeFabrica.Crear().Select(e => e.Id).ToList();

        Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(nombres.Count, nombres.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void El_orden_no_tiene_huecos_ni_repetidos()
    {
        Assert.Equal(
            Enumerable.Range(1, EtiquetasDeFabrica.Cantidad),
            EtiquetasDeFabrica.Crear().Select(e => e.Orden).Order());
    }

    [Fact]
    public void Un_catalogo_igual_al_de_fabrica_no_tiene_nada_que_restablecer()
    {
        var cambios = RestablecerEtiquetas.Comparar(EtiquetasDeFabrica.Crear(), _ => 0);

        Assert.False(cambios.HayAlgoQueHacer);
        Assert.False(cambios.BorraAlgo);
        Assert.Equal(0, cambios.ConexionesQuePierdenEtiqueta);
    }

    [Fact]
    public void Una_base_con_las_cuatro_viejas_detecta_QA_como_faltante()
    {
        var sinQa = EtiquetasDeFabrica.Crear().Where(e => e.Codigo != "QA").ToList();

        var cambios = RestablecerEtiquetas.Comparar(sinQa, _ => 0);

        Assert.Equal("QA", Assert.Single(cambios.Faltantes).Codigo);
        Assert.False(cambios.BorraAlgo);
    }

    [Fact]
    public void Una_de_fabrica_renombrada_por_el_usuario_figura_como_modificada()
    {
        var actuales = EtiquetasDeFabrica.Crear().ToList();
        actuales[0].Renombrar("PROD", "Prod", "azul");

        var cambios = RestablecerEtiquetas.Comparar(actuales, _ => 0);

        Assert.Equal("PRD", Assert.Single(cambios.Modificadas).Codigo);
        Assert.Empty(cambios.Faltantes);
    }

    [Fact]
    public void Un_cambio_de_orden_tambien_cuenta_como_modificada()
    {
        var actuales = EtiquetasDeFabrica.Crear().ToList();
        actuales[0].Orden = 99;

        Assert.Single(RestablecerEtiquetas.Comparar(actuales, _ => 0).Modificadas);
    }

    [Fact]
    public void Las_etiquetas_propias_del_usuario_se_cuentan_como_agregadas()
    {
        var actuales = EtiquetasDeFabrica.Crear()
            .Append(Etiqueta("LAB", "Laboratorio", "rosa", 6))
            .ToList();

        var cambios = RestablecerEtiquetas.Comparar(actuales, _ => 0);

        Assert.Equal("LAB", Assert.Single(cambios.Agregadas).Codigo);
        Assert.True(cambios.BorraAlgo);
    }

    [Fact]
    public void Se_cuentan_las_conexiones_que_quedarian_sin_etiqueta()
    {
        var propia = Etiqueta("LAB", "Laboratorio", "rosa", 6);
        var actuales = EtiquetasDeFabrica.Crear().Append(propia).ToList();

        var cambios = RestablecerEtiquetas.Comparar(
            actuales, id => id == propia.Id ? 7 : 0);

        Assert.Equal(7, cambios.ConexionesQuePierdenEtiqueta);
    }

    [Fact]
    public void Restablecer_sobre_un_catalogo_vacio_repone_las_cinco()
    {
        var cambios = RestablecerEtiquetas.Comparar([], _ => 0);

        Assert.Equal(5, cambios.Faltantes.Count);
        Assert.Empty(cambios.Modificadas);
        Assert.False(cambios.BorraAlgo);
    }

    [Fact]
    public void Solo_las_de_fabrica_se_reconocen_por_identificador()
    {
        Assert.True(EtiquetasDeFabrica.EsDeFabrica(EtiquetasDeFabrica.Identificadores[0]));
        Assert.False(EtiquetasDeFabrica.EsDeFabrica(Guid.NewGuid()));
    }
    [Fact]
    public void Mutar_lo_que_devuelve_Crear_no_corrompe_el_catalogo_de_fabrica()
    {
        EtiquetasDeFabrica.Crear()[0].Renombrar("XXX", "Roto", "gris");

        Assert.Equal("PRD", EtiquetasDeFabrica.Crear()[0].Codigo);
    }
}
