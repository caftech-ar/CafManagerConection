using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class TiempoArribaTests
{
    private static SupervisorProcess Proceso(string detalle) => new("api", "RUNNING", detalle);

    [Fact]
    public void Sin_dias_se_leen_horas_minutos_y_segundos()
    {
        var arriba = Proceso("pid 2316422, uptime 17:12:00").Uptime;

        Assert.Equal(new TimeSpan(0, 17, 12, 0), arriba);
    }

    /// <remarks>
    /// Las líneas reales de servidor-uno, con sus 77 y 81 días.
    /// </remarks>
    [Theory]
    [InlineData("pid 80621, uptime 77 days, 22:02:44", 77, 22, 2, 44)]
    [InlineData("pid 919313, uptime 81 days, 13:52:40", 81, 13, 52, 40)]
    [InlineData("pid 3316634, uptime 15 days, 17:32:28", 15, 17, 32, 28)]
    public void Con_dias_se_leen_los_dias(
        string detalle, int dias, int horas, int minutos, int segundos) =>
        Assert.Equal(
            new TimeSpan(dias, horas, minutos, segundos), Proceso(detalle).Uptime);

    [Fact]
    public void Un_solo_dia_va_en_singular()
    {
        var arriba = Proceso("pid 123, uptime 1 day, 0:03:00").Uptime;

        Assert.Equal(new TimeSpan(1, 0, 3, 0), arriba);
    }

    [Fact]
    public void Los_segundos_recien_arrancado_se_leen()
    {
        var arriba = Proceso("pid 123, uptime 0:00:08").Uptime;

        Assert.Equal(TimeSpan.FromSeconds(8), arriba);
    }

    [Theory]
    [InlineData("Exited too quickly (process log may have details)")]
    [InlineData("Not started")]
    [InlineData("")]
    [InlineData(null)]
    public void Sin_tiempo_arriba_no_se_inventa_uno(string? detalle) =>
        Assert.Null(new SupervisorProcess("api", "FATAL", detalle).Uptime);

    [Fact]
    public void El_identificador_de_proceso_no_se_confunde_con_el_tiempo()
    {
        var arriba = Proceso("pid 2316422, uptime 17:12:00").Uptime;

        Assert.Equal(0, arriba!.Value.Days);
    }
}
