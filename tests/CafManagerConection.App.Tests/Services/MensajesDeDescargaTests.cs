using CafManagerConection.App.Services;
using CafManagerConection.Infrastructure.Actualizaciones;

namespace CafManagerConection.App.Tests.Services;

public sealed class MensajesDeDescargaTests
{
    [Fact]
    public void Verificada_es_la_unica_que_habilita_ejecutar()
    {
        var (mensaje, ejecutar) = MensajesDeDescarga.Interpretar(
            ResultadoDeDescarga.Verificada(@"C:\ruta\setup.exe"));

        Assert.True(ejecutar);
        Assert.Contains("verificada", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(EstadosQueNoEjecutan))]
    public void Los_otros_tres_estados_no_habilitan_ejecutar(ResultadoDeDescarga resultado)
    {
        var (_, ejecutar) = MensajesDeDescarga.Interpretar(resultado);

        Assert.False(ejecutar);
    }

    public static IEnumerable<object[]> EstadosQueNoEjecutan()
    {
        yield return [ResultadoDeDescarga.HashNoCoincide("el archivo no coincide con el hash publicado")];
        yield return [ResultadoDeDescarga.SinHashPublicado("la release no publica un hash")];
        yield return [ResultadoDeDescarga.Fallo("no hay conexión")];
    }

    [Fact]
    public void El_motivo_de_hash_no_coincide_llega_al_mensaje()
    {
        var (mensaje, _) = MensajesDeDescarga.Interpretar(
            ResultadoDeDescarga.HashNoCoincide("el archivo no coincide con el hash publicado"));

        Assert.Contains("el archivo no coincide con el hash publicado", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void El_motivo_de_falla_de_red_llega_al_mensaje()
    {
        var (mensaje, _) = MensajesDeDescarga.Interpretar(ResultadoDeDescarga.Fallo("no hay conexión"));

        Assert.Contains("no hay conexión", mensaje, StringComparison.Ordinal);
    }
}
