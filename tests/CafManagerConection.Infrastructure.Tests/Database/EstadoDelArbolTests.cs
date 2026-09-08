using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Database;

namespace CafManagerConection.Infrastructure.Tests.Database;

public sealed class EstadoDelArbolTests
{
    private static async Task<AppSettingsService> ServicioAsync(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();
        return new AppSettingsService(new SettingsStore(db.Factory));
    }

    [Fact]
    public async Task Sin_nada_guardado_devuelve_nulo()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        Assert.Null(await ajustes.GetTreeStateAsync());
    }

    [Fact]
    public async Task Las_carpetas_abiertas_sobreviven_a_guardar_y_leer()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var elegida = Guid.NewGuid();

        await ajustes.SaveTreeStateAsync(new EstadoDelArbol([a, b], elegida));

        var leido = await ajustes.GetTreeStateAsync();

        Assert.NotNull(leido);
        Assert.Equal([a, b], leido.CarpetasAbiertas);
        Assert.Equal(elegida, leido.Seleccionado);
    }

    [Fact]
    public async Task Guardar_el_arbol_cerrado_no_se_confunde_con_no_haber_guardado()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.SaveTreeStateAsync(new EstadoDelArbol([], null));

        var leido = await ajustes.GetTreeStateAsync();

        Assert.NotNull(leido);
        Assert.Empty(leido.CarpetasAbiertas);
        Assert.Null(leido.Seleccionado);
    }

    [Fact]
    public async Task Sin_fila_elegida_se_guarda_y_se_lee_como_ninguna()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.SaveTreeStateAsync(new EstadoDelArbol([Guid.NewGuid()], null));

        Assert.Null((await ajustes.GetTreeStateAsync())!.Seleccionado);
    }

    [Fact]
    public async Task Un_identificador_ilegible_se_descarta_sin_romper()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        var store = new SettingsStore(db.Factory);
        var bueno = Guid.NewGuid();

        await store.SetAsync(SettingKeys.TreeExpandedFolders, $"esto-no-es-un-guid,{bueno:D},,");

        var leido = await new AppSettingsService(store).GetTreeStateAsync();

        Assert.NotNull(leido);
        Assert.Equal([bueno], leido.CarpetasAbiertas);
    }

    [Fact]
    public async Task Guardar_dos_veces_deja_el_ultimo_estado()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        var primera = Guid.NewGuid();
        var segunda = Guid.NewGuid();

        await ajustes.SaveTreeStateAsync(new EstadoDelArbol([primera], null));
        await ajustes.SaveTreeStateAsync(new EstadoDelArbol([segunda], null));

        Assert.Equal([segunda], (await ajustes.GetTreeStateAsync())!.CarpetasAbiertas);
    }
}
