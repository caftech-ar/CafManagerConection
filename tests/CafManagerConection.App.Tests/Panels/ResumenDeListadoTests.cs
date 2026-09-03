using CafManagerConection.App.Panels;

namespace CafManagerConection.App.Tests.Panels;

public sealed class ResumenDeListadoTests
{
    [Fact]
    public void Sin_enlaces_omitidos_el_resumen_no_los_menciona()
    {
        var texto = ResumenDeListado.Describir("/var", 2, 5, 0);

        Assert.Equal("/var — 2 carpeta(s), 5 archivo(s)", texto);
        Assert.DoesNotContain("enlace", texto, StringComparison.OrdinalIgnoreCase);
    }

    // FR-189c: un listado que calla lo que sacó se ve igual que un directorio con menos archivos.
    [Fact]
    public void Con_enlaces_omitidos_el_resumen_dice_cuantos()
    {
        Assert.Equal(
            "/etc — 1 carpeta(s), 3 archivo(s), 4 enlace(s) simbólico(s) omitido(s)",
            ResumenDeListado.Describir("/etc", 1, 3, 4));
    }

    [Fact]
    public void Un_solo_enlace_omitido_tambien_se_informa()
    {
        Assert.Contains(
            "1 enlace(s) simbólico(s) omitido(s)",
            ResumenDeListado.Describir("/", 0, 0, 1),
            StringComparison.Ordinal);
    }

    [Fact]
    public void El_destino_de_la_subida_se_anuncia_con_la_carpeta_y_el_servidor()
    {
        Assert.Equal(
            "Se van a subir 3 elemento(s) a «/srv/app» en produccion."
            + Environment.NewLine
            + "Nada se transfiere hasta que lo confirmes.",
            ResumenDeListado.ConfirmacionDeSubida("/srv/app", "produccion", 3));
    }

    [Fact]
    public void Un_solo_elemento_se_anuncia_igual_con_su_carpeta_de_destino()
    {
        Assert.Contains(
            "a «/» en local",
            ResumenDeListado.ConfirmacionDeSubida("/", "local", 1),
            StringComparison.Ordinal);
    }
}
