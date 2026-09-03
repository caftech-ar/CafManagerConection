using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

/// <summary>FR-087a, FR-087b, FR-087c.</summary>
public sealed class NivelDeUsoTests
{
    [Theory]
    [InlineData(0, NivelDeMedida.Normal)]
    [InlineData(74.9, NivelDeMedida.Normal)]
    [InlineData(75, NivelDeMedida.Advertencia)]
    [InlineData(89.9, NivelDeMedida.Advertencia)]
    [InlineData(90, NivelDeMedida.Critico)]
    [InlineData(100, NivelDeMedida.Critico)]
    public void Los_tramos_de_un_porcentaje(double porcentaje, NivelDeMedida esperado) =>
        Assert.Equal(esperado, NivelDeUso.DePorcentaje(porcentaje));

    /// <summary>FR-087c.</summary>
    [Theory]
    [InlineData(4, 8, NivelDeMedida.Normal)]
    [InlineData(4, 4, NivelDeMedida.Advertencia)]
    [InlineData(4, 2, NivelDeMedida.Critico)]
    public void La_misma_carga_cambia_de_tramo_segun_los_nucleos(
        double carga, int nucleos, NivelDeMedida esperado) =>
        Assert.Equal(esperado, NivelDeUso.DeCarga(carga, nucleos));

    [Theory]
    [InlineData(0.99, NivelDeMedida.Normal)]
    [InlineData(1.0, NivelDeMedida.Advertencia)]
    [InlineData(1.49, NivelDeMedida.Advertencia)]
    [InlineData(1.5, NivelDeMedida.Critico)]
    public void Los_bordes_de_la_carga_por_nucleo(double porNucleo, NivelDeMedida esperado) =>
        Assert.Equal(esperado, NivelDeUso.DeCarga(porNucleo, 1));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sin_nucleos_informados_no_se_clasifica(int nucleos) =>
        Assert.Equal(NivelDeMedida.Normal, NivelDeUso.DeCarga(99, nucleos));

    [Fact]
    public void El_tramo_normal_no_lleva_etiqueta() =>
        Assert.Null(NivelDeUso.Etiqueta(NivelDeMedida.Normal));

    [Theory]
    [InlineData(NivelDeMedida.Advertencia)]
    [InlineData(NivelDeMedida.Critico)]
    public void Los_otros_dos_tramos_se_pueden_leer_sin_color(NivelDeMedida nivel) =>
        Assert.False(string.IsNullOrWhiteSpace(NivelDeUso.Etiqueta(nivel)));
}
