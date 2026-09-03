using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class GravedadDeProcesoTests
{
    private static SupervisorProcess Proceso(string estado) => new("api", estado, null);

    [Fact]
    public void Corriendo_es_corriendo() =>
        Assert.Equal(GravedadDeProceso.Corriendo, Proceso("RUNNING").Gravedad);

    [Theory]
    [InlineData("FATAL")]
    [InlineData("BACKOFF")]
    [InlineData("UNKNOWN")]
    public void Los_estados_de_falla_son_falla(string estado) =>
        Assert.Equal(GravedadDeProceso.Falla, Proceso(estado).Gravedad);

    [Theory]
    [InlineData("STOPPED")]
    [InlineData("STARTING")]
    [InlineData("STOPPING")]
    [InlineData("EXITED")]
    public void Los_estados_intermedios_son_advertencia(string estado) =>
        Assert.Equal(GravedadDeProceso.Advertencia, Proceso(estado).Gravedad);

    [Theory]
    [InlineData("running")]
    [InlineData("Running")]
    public void No_distingue_mayusculas(string estado) =>
        Assert.Equal(GravedadDeProceso.Corriendo, Proceso(estado).Gravedad);

    [Fact]
    public void Un_estado_desconocido_no_pasa_por_sano() =>
        Assert.NotEqual(GravedadDeProceso.Corriendo, Proceso("ALGO_NUEVO").Gravedad);
}
