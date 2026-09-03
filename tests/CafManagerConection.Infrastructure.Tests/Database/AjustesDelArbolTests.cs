using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Database;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>
/// Ida y vuelta de la apariencia del árbol: tamaño de letra y si muestra el servidor (FR-165).
/// </summary>
public sealed class AjustesDelArbolTests
{
    private static async Task<AppSettingsService> ServicioAsync(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();
        return new AppSettingsService(new SettingsStore(db.Factory));
    }

    [Fact]
    public async Task Sin_nada_guardado_arranca_un_escalon_abajo_de_normal_y_sin_host()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        var leido = await ajustes.GetTreeAppearanceAsync();

        Assert.Equal(CafManagerConection.Domain.Settings.AjustesDelArbol.AjustePorOmision, leido.AjusteDeTamano);
        Assert.False(leido.MuestraHost);
    }

    [Fact]
    public async Task Los_dos_ajustes_sobreviven_a_guardar_y_leer()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.SaveTreeAppearanceAsync(new AjustesDelArbol(2, true));

        var leido = await ajustes.GetTreeAppearanceAsync();

        Assert.Equal(2, leido.AjusteDeTamano);
        Assert.True(leido.MuestraHost);
    }

    [Fact]
    public async Task Un_valor_fuera_de_rango_se_recorta_al_leer()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        var store = new SettingsStore(db.Factory);
        await store.SetAsync(SettingKeys.ArbolAjusteDeTamano, "99");

        var leido = await new AppSettingsService(store).GetTreeAppearanceAsync();

        Assert.Equal(AjustesDelArbol.MaximoAjuste, leido.AjusteDeTamano);
    }

    [Fact]
    public async Task Guardar_fuera_de_rango_tambien_se_recorta()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.SaveTreeAppearanceAsync(new AjustesDelArbol(-10, false));

        var leido = await ajustes.GetTreeAppearanceAsync();

        Assert.Equal(AjustesDelArbol.MinimoAjuste, leido.AjusteDeTamano);
    }
}
