using CafManagerConection.Infrastructure.Database;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>Ida y vuelta de los ajustes del aviso de versión nueva.</summary>
public sealed class AjustesDeActualizacionTests
{
    private static async Task<AjustesDeActualizacionStore> ServicioAsync(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();
        return new AjustesDeActualizacionStore(new SettingsStore(db.Factory));
    }

    [Fact]
    public async Task Sin_nada_guardado_no_hay_consulta_ni_posposicion()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        var actual = await ajustes.ObtenerAsync();

        Assert.Null(actual.UltimaConsulta);
        Assert.Null(actual.VersionPospuesta);
        Assert.Null(actual.MomentoDePosposicion);
    }

    /// <remarks>Se guarda en formato "O", con el desfase incluido.</remarks>
    [Fact]
    public async Task La_fecha_de_la_ultima_consulta_sobrevive_con_la_zona_horaria()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);
        var momento = new DateTimeOffset(2026, 3, 1, 10, 30, 0, TimeSpan.FromHours(-3));

        await ajustes.GuardarAsync(new AjustesDeActualizacion(momento));

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

        await ajustes.GuardarAsync(
            new AjustesDeActualizacion(DateTimeOffset.Now, "1.4.0", momento));

        var actual = await ajustes.ObtenerAsync();

        Assert.Equal("1.4.0", actual.VersionPospuesta);
        Assert.Equal(momento, actual.MomentoDePosposicion);
    }

    [Fact]
    public async Task Guardar_de_nuevo_sin_version_pospuesta_la_olvida()
    {
        using var db = new TempDatabase();
        var ajustes = await ServicioAsync(db);

        await ajustes.GuardarAsync(
            new AjustesDeActualizacion(DateTimeOffset.Now, "1.4.0", DateTimeOffset.Now));

        await ajustes.GuardarAsync(new AjustesDeActualizacion(DateTimeOffset.Now));

        var actual = await ajustes.ObtenerAsync();

        Assert.Null(actual.VersionPospuesta);
        Assert.Null(actual.MomentoDePosposicion);
    }
}
