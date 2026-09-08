using CafManagerConection.App.Services;
using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.App.Tests.Services;

public sealed class TituloDeVentanaTests
{
    [Fact]
    public void Sin_sesiones_solo_el_nombre() =>
        Assert.Equal("CMC", TituloDeVentana.Componer(null, SessionState.Disconnected, 0));

    [Fact]
    public void Con_un_nombre_pero_ninguna_sesion_gana_el_nombre_solo() =>
        Assert.Equal("CMC", TituloDeVentana.Componer("pgsql-prod", SessionState.Connected, 0));

    [Fact]
    public void Una_sesion_conectada_muestra_su_conexion() =>
        Assert.Equal(
            "CMC - pgsql-prod",
            TituloDeVentana.Componer("pgsql-prod", SessionState.Connected, 1));

    [Fact]
    public void Varias_sesiones_llevan_la_cuenta() =>
        Assert.Equal(
            "CMC - pgsql-prod (3 sesiones)",
            TituloDeVentana.Componer("pgsql-prod", SessionState.Connected, 3));

    [Theory]
    [InlineData(SessionState.Connecting, "CMC - web-01 (conectando)")]
    [InlineData(SessionState.Disconnected, "CMC - web-01 (desconectada)")]
    [InlineData(SessionState.Error, "CMC - web-01 (con error)")]
    public void El_estado_aparece_cuando_no_esta_conectada(SessionState estado, string esperado) =>
        Assert.Equal(esperado, TituloDeVentana.Componer("web-01", estado, 1));

    [Fact]
    public void El_estado_y_la_cuenta_conviven() =>
        Assert.Equal(
            "CMC - web-01 (con error, 4 sesiones)",
            TituloDeVentana.Componer("web-01", SessionState.Error, 4));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Un_nombre_vacio_no_deja_el_guion_colgado(string? nombre) =>
        Assert.Equal("CMC", TituloDeVentana.Componer(nombre, SessionState.Connected, 2));

    [Fact]
    public void Los_espacios_de_los_costados_no_viajan_al_titulo() =>
        Assert.Equal(
            "CMC - web-01",
            TituloDeVentana.Componer("  web-01  ", SessionState.Connected, 1));
}
