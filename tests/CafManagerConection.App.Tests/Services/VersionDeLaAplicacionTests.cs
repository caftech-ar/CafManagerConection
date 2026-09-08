using CafManagerConection.App.Services;
using CafManagerConection.Domain.Sessions;
using Xunit;

namespace CafManagerConection.App.Tests.Services;

public sealed class VersionDeLaAplicacionTests
{
    [Fact]
    public void El_hash_que_agrega_el_SDK_no_se_muestra()
    {
        // El SDK escribe «0.1.0+f4759e62c851450ce5b4824fde67759f8c3fd825» al compilar desde git.
        Assert.Equal("0.1.0", VersionDeLaAplicacion.Limpiar("0.1.0+f4759e62c851", null));
    }

    [Fact]
    public void Se_prefiere_la_informacional_sobre_la_del_ensamblado()
    {
        // La del ensamblado tiene cuatro componentes y la etiqueta de la release tiene tres.
        Assert.Equal("0.1.0", VersionDeLaAplicacion.Limpiar("0.1.0", "0.1.0.0"));
    }

    [Fact]
    public void Sin_informacional_cae_a_la_del_ensamblado()
    {
        Assert.Equal("0.1.0.0", VersionDeLaAplicacion.Limpiar(null, "0.1.0.0"));
    }

    [Fact]
    public void Sin_ninguna_de_las_dos_lo_dice_en_lugar_de_quedar_vacia()
    {
        Assert.Equal("desconocida", VersionDeLaAplicacion.Limpiar(null, null));
    }

    [Fact]
    public void La_version_real_del_ensamblado_no_esta_vacia_ni_trae_el_hash()
    {
        var version = VersionDeLaAplicacion.Corta;

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.DoesNotContain("+", version, StringComparison.Ordinal);
    }

    [Fact]
    public void El_titulo_sin_sesiones_muestra_la_version()
    {
        Assert.Equal("CMC 0.1.0", TituloDeVentana.Componer(null, SessionState.Disconnected, 0, "0.1.0"));
    }

    [Fact]
    public void El_titulo_con_una_sesion_muestra_la_version_y_la_conexion()
    {
        Assert.Equal(
            "CMC 0.1.0 - servidor-uno",
            TituloDeVentana.Componer("servidor-uno", SessionState.Connected, 1, "0.1.0"));
    }

    [Fact]
    public void Sin_version_el_titulo_queda_como_antes()
    {
        Assert.Equal("CMC", TituloDeVentana.Componer(null, SessionState.Disconnected, 0));

        Assert.Equal(
            "CMC - servidor-uno",
            TituloDeVentana.Componer("servidor-uno", SessionState.Connected, 1));
    }
}
