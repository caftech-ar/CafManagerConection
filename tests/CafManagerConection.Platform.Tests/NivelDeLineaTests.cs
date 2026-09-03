using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

// FR-100f
public sealed class NivelDeLineaTests
{
    [Theory]
    [InlineData("2026-09-01 10:22:31 ERROR no se pudo abrir el socket")]
    [InlineData("[error] 1234#0: *5 connect() failed")]
    [InlineData("FATAL: base de datos inaccesible")]
    [InlineData("app.service: Failed with result 'exit-code'")]
    [InlineData("CRITICAL - disco lleno")]
    [InlineData("Unhandled exception: NullReferenceException")]
    public void Las_lineas_de_error_se_reconocen(string linea) =>
        Assert.Equal(GravedadDeLinea.Error, NivelDeLinea.De(linea));

    [Theory]
    [InlineData("2026-09-01 10:22:31 WARN reintentando en 5s")]
    [InlineData("[warning] certificado vence en 10 días")]
    [InlineData("NOTICE: signal process started")]
    public void Las_advertencias_se_reconocen(string linea) =>
        Assert.Equal(GravedadDeLinea.Advertencia, NivelDeLinea.De(linea));

    [Theory]
    [InlineData("DeprecationWarning: usar la nueva API", GravedadDeLinea.Advertencia)]
    [InlineData("ValueError: invalid literal for int()", GravedadDeLinea.Error)]
    [InlineData("RuntimeError: dictionary changed size", GravedadDeLinea.Error)]
    public void Las_marcas_en_camelCase_se_reconocen(string linea, GravedadDeLinea esperada) =>
        Assert.Equal(esperada, NivelDeLinea.De(linea));

    [Theory]
    [InlineData("abriendo /var/log/nginx/access.log")]
    [InlineData("Listening on 0.0.0.0:8080")]
    [InlineData("terrorismo no es una marca de nivel")]
    [InlineData("GET /errorless/page 200")]
    [InlineData("started successfully")]
    [InlineData("")]
    [InlineData(null)]
    public void Lo_que_no_tiene_marca_queda_normal(string? linea) =>
        Assert.Equal(GravedadDeLinea.Normal, NivelDeLinea.De(linea));

    [Fact]
    public void Una_linea_con_marca_y_con_ruta_parecida_sigue_siendo_error() =>
        Assert.Equal(
            GravedadDeLinea.Error,
            NivelDeLinea.De("[error] no se pudo escribir /var/log/error.log"));

    [Fact]
    public void El_error_gana_sobre_la_advertencia() =>
        Assert.Equal(
            GravedadDeLinea.Error,
            NivelDeLinea.De("WARN: reintento agotado, ERROR final"));

    // FR-100f
    [Fact]
    public void Clasificar_no_toca_el_texto()
    {
        const string original = "  [error]   línea  con    espacios raros \t y tabs  ";
        var copia = new string(original.ToCharArray());

        NivelDeLinea.De(copia);

        Assert.Equal(original, copia);
    }
}
