using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class MagnitudesTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KiB")]
    [InlineData(1048576, "1 MiB")]
    [InlineData(3221225472, "3 GiB")]
    public void El_tamano_sube_de_unidad_cada_1024(long bytes, string esperado) =>
        Assert.Equal(esperado, Magnitudes.Tamano(bytes));

    [Fact]
    public void El_tamano_lleva_un_decimal_cuando_no_es_redondo()
    {
        var coma = System.Globalization.CultureInfo.CurrentCulture
            .NumberFormat.NumberDecimalSeparator;

        Assert.Equal($"1{coma}5 KiB", Magnitudes.Tamano(1536));
    }

    [Fact]
    public void Un_tamano_enorme_se_queda_en_la_ultima_unidad()
    {
        Assert.EndsWith("TiB", Magnitudes.Tamano(long.MaxValue / 2));
    }

    [Theory]
    [InlineData(90, "1 min")]
    [InlineData(3700, "1 h 1 min")]
    [InlineData(90000, "1 día(s) y 1 h")]
    public void La_duracion_usa_la_unidad_mas_grande_que_aplica(int segundos, string esperado) =>
        Assert.Equal(esperado, Magnitudes.Duracion(TimeSpan.FromSeconds(segundos)));
}
