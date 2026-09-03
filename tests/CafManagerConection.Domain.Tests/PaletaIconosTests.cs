using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

// Se guarda la clave del color y no su hexadecimal, para que el ajuste sirva en tema claro y oscuro.
public sealed class PaletaIconosTests
{
    [Fact]
    public void La_paleta_tiene_diez_colores()
    {
        Assert.Equal(10, PaletaIconos.Colores.Count);
    }

    [Fact]
    public void Las_claves_no_se_repiten()
    {
        var claves = PaletaIconos.Colores.Select(c => c.Clave).ToList();

        Assert.Equal(claves.Count, claves.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Las_claves_son_minusculas_y_sin_espacios()
    {
        // Terminan como valor en el almacén de preferencias: deben ser legibles a mano.
        foreach (var color in PaletaIconos.Colores)
        {
            Assert.Equal(color.Clave.ToLowerInvariant(), color.Clave);
            Assert.DoesNotContain(' ', color.Clave);
            Assert.NotEmpty(color.Nombre);
        }
    }

    [Theory]
    [InlineData("azul", true)]
    [InlineData("gris", true)]
    [InlineData("fucsia", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Solo_las_claves_de_la_paleta_son_validas(string? clave, bool esperado)
    {
        Assert.Equal(esperado, PaletaIconos.EsValido(clave));
    }

    [Fact]
    public void Una_clave_desconocida_cae_en_el_color_por_omision()
    {
        // Es lo que pasaría si en una versión posterior se quitara un color de la lista.
        var color = PaletaIconos.Resolver("fucsia", PaletaIconos.PorOmisionSsh);

        Assert.Equal(PaletaIconos.PorOmisionSsh, color.Clave);
    }

    [Fact]
    public void Una_clave_nula_cae_en_el_color_por_omision()
    {
        var color = PaletaIconos.Resolver(null, PaletaIconos.PorOmisionRdp);

        Assert.Equal(PaletaIconos.PorOmisionRdp, color.Clave);
    }

    [Fact]
    public void Los_colores_por_omision_existen_en_la_paleta()
    {
        Assert.True(PaletaIconos.EsValido(PaletaIconos.PorOmisionRdp));
        Assert.True(PaletaIconos.EsValido(PaletaIconos.PorOmisionSsh));
        Assert.True(PaletaIconos.EsValido(PaletaIconos.PorOmisionWeb));
    }

    [Fact]
    public void Los_tres_protocolos_arrancan_con_colores_distintos()
    {
        var d = ColoresDeIconos.Default;

        Assert.Equal(3, new[] { d.Rdp, d.Ssh, d.Web }.Distinct(StringComparer.Ordinal).Count());
    }
}
