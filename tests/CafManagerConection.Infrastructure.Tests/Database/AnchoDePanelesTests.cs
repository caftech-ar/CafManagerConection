using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Database;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <remarks>Van todos en una sola clave con formato <c>panel=ancho;panel=ancho</c>.</remarks>
public sealed class AnchoDePanelesTests
{
    private static async Task<AppSettingsService> ServicioAsync(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();
        return new AppSettingsService(new SettingsStore(db.Factory));
    }

    [Fact]
    public async Task Sin_nada_guardado_no_hay_anchos()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        Assert.Empty(await ajustes.GetPanelWidthsAsync());
    }

    [Fact]
    public async Task Un_ancho_sobrevive_a_guardar_y_leer()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.SavePanelWidthAsync("Docker", 620.5);

        Assert.Equal(620.5, (await ajustes.GetPanelWidthsAsync())["Docker"]);
    }

    [Fact]
    public async Task Cada_panel_recuerda_el_suyo()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.SavePanelWidthAsync("Docker", 700);
        await ajustes.SavePanelWidthAsync("Puertos", 380);
        await ajustes.SavePanelWidthAsync("Supervisord", 520);

        var anchos = await ajustes.GetPanelWidthsAsync();

        Assert.Equal(3, anchos.Count);
        Assert.Equal(700, anchos["Docker"]);
        Assert.Equal(380, anchos["Puertos"]);
        Assert.Equal(520, anchos["Supervisord"]);
    }

    [Fact]
    public async Task Guardar_el_mismo_panel_dos_veces_deja_el_ultimo()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.SavePanelWidthAsync("Nginx", 400);
        await ajustes.SavePanelWidthAsync("Nginx", 900);

        var anchos = await ajustes.GetPanelWidthsAsync();

        Assert.Single(anchos);
        Assert.Equal(900, anchos["Nginx"]);
    }

    [Fact]
    public async Task El_decimal_no_depende_de_la_configuracion_regional()
    {
        using var db = new TempDatabase();
        var store = new SettingsStore(db.Factory);
        await db.CreateInitializer().InitializeAsync();

        await new AppSettingsService(store).SavePanelWidthAsync("Docker", 620.5);

        var crudo = await store.GetAsync(SettingKeys.PanelWidths);

        Assert.NotNull(crudo);
        Assert.Contains("620.5", crudo, StringComparison.Ordinal);
        Assert.DoesNotContain(",5", crudo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restablecer_los_olvida_a_todos()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.SavePanelWidthAsync("Docker", 700);
        await ajustes.SavePanelWidthAsync("Puertos", 380);
        await ajustes.ResetPanelWidthsAsync();

        Assert.Empty(await ajustes.GetPanelWidthsAsync());
    }

    [Theory]
    [InlineData("Docker=abc;Puertos=380")]
    [InlineData("Docker;Puertos=380")]
    [InlineData("Docker=-50;Puertos=380")]
    [InlineData("Docker=0;Puertos=380")]
    [InlineData(";;;Puertos=380;;")]
    public async Task Un_valor_ilegible_se_descarta_sin_llevarse_los_demas(string crudo)
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        var store = new SettingsStore(db.Factory);
        await store.SetAsync(SettingKeys.PanelWidths, crudo);

        var anchos = await new AppSettingsService(store).GetPanelWidthsAsync();

        Assert.Equal(380, anchos["Puertos"]);
        Assert.False(anchos.ContainsKey("Docker"));
    }
}
