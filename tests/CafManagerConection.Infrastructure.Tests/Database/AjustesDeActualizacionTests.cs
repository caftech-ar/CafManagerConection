using CafManagerConection.Infrastructure.Database;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>Ida y vuelta de los ajustes del aviso de versión nueva (FR-159 a FR-162).</summary>
public sealed class AjustesDeActualizacionTests
{
    private static async Task<AjustesDeActualizacionStore> ServicioAsync(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();
        return new AjustesDeActualizacionStore(new SettingsStore(db.Factory));
    }

    [Fact]
    public async Task Sin_nada_guardado_el_origen_es_el_repositorio_del_proyecto()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        var actual = await ajustes.ObtenerAsync();

        Assert.Equal(AjustesDeActualizacion.OrigenPorOmision, actual.Origen);
        Assert.Null(actual.UltimaConsulta);
        Assert.Null(actual.VersionPospuesta);
        Assert.Null(actual.MomentoDePosposicion);
    }

    /// <remarks>Cubre FR-159b: sin esto, apagar el aviso desde Preferencias no sería posible.</remarks>
    [Fact]
    public async Task El_origen_es_fijo_y_no_se_puede_cambiar_guardando_otro()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.GuardarAsync(new AjustesDeActualizacion(Origen: "otro/repositorio"));

        Assert.Equal(AjustesDeActualizacion.Repositorio, (await ajustes.ObtenerAsync()).Origen);
    }

    [Fact]
    public async Task Vaciar_el_origen_no_apaga_la_funcion_porque_el_origen_es_fijo()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.GuardarAsync(new AjustesDeActualizacion(Origen: string.Empty));

        Assert.Equal(AjustesDeActualizacion.Repositorio, (await ajustes.ObtenerAsync()).Origen);
    }

    [Fact]
    public async Task Una_base_recien_creada_ya_trae_el_repositorio_del_proyecto()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        Assert.Equal("caftech-ar/CafManagerConection", (await ajustes.ObtenerAsync()).Origen);
    }

    /// <remarks>Se guarda en formato "O", con el desfase incluido.</remarks>
    [Fact]
    public async Task La_fecha_de_la_ultima_consulta_sobrevive_con_la_zona_horaria()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);
        var momento = new DateTimeOffset(2026, 3, 1, 10, 30, 0, TimeSpan.FromHours(-3));

        await ajustes.GuardarAsync(new AjustesDeActualizacion("operador/cmc", momento));

        var actual = await ajustes.ObtenerAsync();

        Assert.Equal(momento, actual.UltimaConsulta);
        Assert.Equal(momento.Offset, actual.UltimaConsulta!.Value.Offset);
    }

    [Fact]
    public async Task La_version_pospuesta_y_su_fecha_sobreviven_juntas()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);
        var momento = DateTimeOffset.Now;

        await ajustes.GuardarAsync(new AjustesDeActualizacion(
            "operador/cmc", DateTimeOffset.Now, "1.4.0", momento));

        var actual = await ajustes.ObtenerAsync();

        Assert.Equal("1.4.0", actual.VersionPospuesta);
        Assert.Equal(momento, actual.MomentoDePosposicion);
    }

    [Fact]
    public async Task Guardar_de_nuevo_sin_version_pospuesta_la_olvida()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.GuardarAsync(new AjustesDeActualizacion(
            "operador/cmc", DateTimeOffset.Now, "1.4.0", DateTimeOffset.Now));
        await ajustes.GuardarAsync(new AjustesDeActualizacion("operador/cmc"));

        var actual = await ajustes.ObtenerAsync();

        Assert.Null(actual.VersionPospuesta);
        Assert.Null(actual.MomentoDePosposicion);
    }
}
