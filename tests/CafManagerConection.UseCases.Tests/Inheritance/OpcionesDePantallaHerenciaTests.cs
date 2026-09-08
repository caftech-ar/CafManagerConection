using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Tests.Inheritance;

/// <summary>Las opciones de pantalla RDP viven en campos reservados y se heredan por el mismo camino que el resto.</summary>
public class OpcionesDePantallaHerenciaTests
{
    private static Connection ConexionRdp(Guid? folderId = null) =>
        new(Guid.NewGuid(), "Servidor", Protocol.Rdp, "192.0.2.1") { FolderId = folderId };

    [Fact]
    public void La_opcion_propia_gana_sobre_la_de_la_carpeta()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        carpeta.Settings.CustomFields[AjustesReservados.ModoDeTamano] =
            ModoDeTamanoRdp.RenegociarResolucion.ToString();

        var conexion = ConexionRdp(carpeta.Id);
        conexion.SetCustomField(
            AjustesReservados.ModoDeTamano, ModoDeTamanoRdp.EscalarPixeles.ToString());

        var efectivo = new SettingsResolver([carpeta]).Resolve(conexion);

        Assert.Equal(ModoDeTamanoRdp.EscalarPixeles, efectivo.ResolvedModoDeTamano);
        Assert.Equal(ValueSource.Own, efectivo.ModoDeTamano.Source);
    }

    [Fact]
    public void La_opcion_sin_valor_propio_se_hereda_de_la_carpeta()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        carpeta.Settings.CustomFields[AjustesReservados.EscalaDeEscritorio] = "150";
        carpeta.Settings.CustomFields[AjustesReservados.Rendimiento] =
            ((int)BanderasDeRendimientoRdp.SinFondoDeEscritorio).ToString();

        var efectivo = new SettingsResolver([carpeta]).Resolve(ConexionRdp(carpeta.Id));

        Assert.Equal(150, efectivo.ResolvedEscalaDeEscritorio);
        Assert.True(efectivo.EscalaDeEscritorio.IsInherited);
        Assert.Equal(carpeta.Id, efectivo.EscalaDeEscritorio.SourceFolderId);
        Assert.Equal(BanderasDeRendimientoRdp.SinFondoDeEscritorio, efectivo.ResolvedRendimiento);
    }

    [Fact]
    public void Sin_modo_definido_se_deriva_del_ajuste_viejo_de_pestana()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción")
        {
            Settings = { RdpFitToTab = true },
        };

        var efectivo = new SettingsResolver([carpeta]).Resolve(ConexionRdp(carpeta.Id));

        Assert.Equal(ModoDeTamanoRdp.EscalarPixeles, efectivo.ResolvedModoDeTamano);
    }

    [Fact]
    public void Sin_nada_definido_las_opciones_quedan_en_su_valor_por_omision()
    {
        var efectivo = new SettingsResolver([]).Resolve(ConexionRdp());

        Assert.Equal(BanderasDeRendimientoRdp.Ninguna, efectivo.ResolvedRendimiento);
        Assert.Null(efectivo.ResolvedTipoDeRed);
        Assert.Null(efectivo.ResolvedEscalaDeEscritorio);
        Assert.False(efectivo.ResolvedComoRemoteApp);
    }
}
