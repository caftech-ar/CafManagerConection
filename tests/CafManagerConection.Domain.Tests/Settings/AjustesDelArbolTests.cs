using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests.Settings;

// Escala de tamaño de letra del árbol y su acotado (FR-165). Único lugar donde se prueba la
// regla de Acotado: la infraestructura confía en que ya hizo el trabajo.
public sealed class AjustesDelArbolTests
{
    [Fact]
    public void Hay_cinco_escalones_separados_por_el_mismo_paso()
    {
        Assert.Equal(5, AjustesDelArbol.Escalones.Count);

        for (var i = 1; i < AjustesDelArbol.Escalones.Count; i++)
        {
            Assert.Equal(
                AjustesDelArbol.Paso,
                AjustesDelArbol.Escalones[i].Ajuste - AjustesDelArbol.Escalones[i - 1].Ajuste,
                3);
        }
    }

    [Fact]
    public void El_escalon_normal_es_el_tamano_de_siempre()
    {
        var normal = AjustesDelArbol.Escalones.Single(e => e.Nombre == "Normal");

        Assert.Equal(0, normal.Ajuste);
    }

    // En una lista de nombres como el árbol de conexiones, un escalón menos entra bastante más
    // en la misma pantalla sin costo de lectura.
    [Fact]
    public void Por_omision_arranca_un_escalon_abajo_de_normal()
    {
        var ajustes = new AjustesDelArbol();

        Assert.Equal(-AjustesDelArbol.Paso, ajustes.AjusteDeTamano);
        Assert.Equal("Chico", AjustesDelArbol.Escalones[ajustes.IndiceDeEscalon()].Nombre);
    }

    [Theory]
    [InlineData(0, "Muy chico")]
    [InlineData(1, "Chico")]
    [InlineData(2, "Normal")]
    [InlineData(3, "Grande")]
    [InlineData(4, "Muy grande")]
    public void Elegir_un_escalon_deja_su_ajuste_y_vuelve_a_encontrarlo(int indice, string nombre)
    {
        var ajustes = new AjustesDelArbol().ConEscalon(indice);

        Assert.Equal(AjustesDelArbol.Escalones[indice].Ajuste, ajustes.AjusteDeTamano);
        Assert.Equal(indice, ajustes.IndiceDeEscalon());
        Assert.Equal(nombre, AjustesDelArbol.Escalones[indice].Nombre);
    }

    // Migración de preferencia: la versión anterior guardaba enteros de -2 a +4, que no caen en
    // ningún escalón de la escala nueva. Se elige el más cercano en vez de descartar el valor.
    [Theory]
    [InlineData(-2, "Chico")]
    [InlineData(-1, "Chico")]
    [InlineData(1, "Grande")]
    [InlineData(4, "Muy grande")]
    public void Un_valor_guardado_por_la_version_anterior_cae_en_el_escalon_mas_parecido(
        double guardado, string esperado)
    {
        var ajustes = new AjustesDelArbol(guardado).Acotado();

        Assert.Equal(esperado, AjustesDelArbol.Escalones[ajustes.IndiceDeEscalon()].Nombre);
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(-1.5)]
    [InlineData(0)]
    [InlineData(1.5)]
    [InlineData(3)]
    public void Dentro_del_rango_queda_igual(double valor)
    {
        Assert.Equal(valor, new AjustesDelArbol(valor).Acotado().AjusteDeTamano);
    }

    [Theory]
    [InlineData(-4, -3)]
    [InlineData(-100, -3)]
    [InlineData(5, 3)]
    [InlineData(100, 3)]
    public void Fuera_del_rango_se_recorta_al_extremo_mas_cercano(double valor, double esperado)
    {
        Assert.Equal(esperado, new AjustesDelArbol(valor).Acotado().AjusteDeTamano);
    }

    // El índice puede venir de un desplegable que quedó fuera de sincronía; una excepción acá
    // cerraría la ventana de preferencias.
    [Theory]
    [InlineData(-5)]
    [InlineData(99)]
    public void Un_escalon_fuera_de_la_lista_se_acota_al_extremo(int indice)
    {
        var ajustes = new AjustesDelArbol().ConEscalon(indice);

        Assert.InRange(
            ajustes.AjusteDeTamano, AjustesDelArbol.MinimoAjuste, AjustesDelArbol.MaximoAjuste);
    }

    [Fact]
    public void Acotar_no_toca_si_muestra_el_host()
    {
        Assert.True(new AjustesDelArbol(100, MuestraHost: true).Acotado().MuestraHost);
    }

    [Fact]
    public void Por_omision_no_muestra_el_host()
    {
        Assert.False(new AjustesDelArbol().MuestraHost);
    }
}
