using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

// FR-150a
public sealed class GravedadDeContenedorTests
{
    private static ContainerInfo Contenedor(string estado, string status = "") =>
        new("abc123", "api", "api:1.0", estado, status, []);

    [Fact]
    public void Corriendo_sin_chequeo_de_salud_es_corriendo() =>
        Assert.Equal(GravedadDeContenedor.Corriendo, Contenedor("running", "Up 3 minutes").Gravedad);

    [Fact]
    public void Corriendo_y_sano_es_corriendo() =>
        Assert.Equal(
            GravedadDeContenedor.Corriendo,
            Contenedor("running", "Up 3 minutes (healthy)").Gravedad);

    [Fact]
    public void Corriendo_pero_enfermo_es_una_falla() =>
        Assert.Equal(
            GravedadDeContenedor.Falla,
            Contenedor("running", "Up 3 minutes (unhealthy)").Gravedad);

    [Fact]
    public void Muerto_es_una_falla() =>
        Assert.Equal(GravedadDeContenedor.Falla, Contenedor("dead", "Dead").Gravedad);

    [Theory]
    [InlineData("restarting")]
    [InlineData("paused")]
    [InlineData("created")]
    public void Los_estados_de_transicion_son_advertencia(string estado) =>
        Assert.Equal(GravedadDeContenedor.Advertencia, Contenedor(estado).Gravedad);

    [Fact]
    public void Detenido_a_proposito_no_es_una_alarma() =>
        Assert.Equal(
            GravedadDeContenedor.Detenido,
            Contenedor("exited", "Exited (0) 2 hours ago").Gravedad);

    [Theory]
    [InlineData("RUNNING")]
    [InlineData("Running")]
    public void No_distingue_mayusculas(string estado) =>
        Assert.Equal(GravedadDeContenedor.Corriendo, Contenedor(estado).Gravedad);

    private static DetalleDeContenedor Detalle(string estado, string? salud = null) =>
        new() { Nombre = "api", Estado = estado, Salud = salud };

    [Fact]
    public void Ficha_corriendo_sin_salud_es_corriendo() =>
        Assert.Equal(GravedadDeContenedor.Corriendo, Detalle("running").Gravedad);

    [Fact]
    public void Ficha_corriendo_y_sana_es_corriendo() =>
        Assert.Equal(GravedadDeContenedor.Corriendo, Detalle("running", "healthy").Gravedad);

    [Fact]
    public void Ficha_corriendo_pero_enferma_es_una_falla() =>
        Assert.Equal(GravedadDeContenedor.Falla, Detalle("running", "unhealthy").Gravedad);

    [Fact]
    public void Ficha_muerta_es_una_falla() =>
        Assert.Equal(GravedadDeContenedor.Falla, Detalle("dead").Gravedad);

    [Theory]
    [InlineData("restarting")]
    [InlineData("paused")]
    [InlineData("created")]
    public void Ficha_en_transicion_es_advertencia(string estado) =>
        Assert.Equal(GravedadDeContenedor.Advertencia, Detalle(estado).Gravedad);

    [Fact]
    public void Ficha_detenida_a_proposito_no_es_una_alarma() =>
        Assert.Equal(GravedadDeContenedor.Detenido, Detalle("exited").Gravedad);
}
