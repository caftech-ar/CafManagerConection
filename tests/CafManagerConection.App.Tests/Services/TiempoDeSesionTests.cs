using CafManagerConection.App.Services;
using Xunit;

namespace CafManagerConection.App.Tests.Services;

public sealed class TiempoDeSesionTests
{
    private static readonly DateTimeOffset Abrio = new(2026, 9, 3, 14, 30, 12, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "0 ms")]
    [InlineData(1, "1 ms")]
    [InlineData(347, "347 ms")]
    [InlineData(999, "999 ms")]
    public void Hasta_el_segundo_va_en_milisegundos(double ms, string esperado) =>
        Assert.Equal(esperado, TiempoDeSesion.Apertura(TimeSpan.FromMilliseconds(ms)));

    [Theory]
    [InlineData(1000, "1,0 s")]
    [InlineData(1543, "1,5 s")]
    [InlineData(12400, "12,4 s")]
    public void Del_segundo_para_arriba_va_en_segundos(double ms, string esperado) =>
        Assert.Equal(esperado, TiempoDeSesion.Apertura(TimeSpan.FromMilliseconds(ms)));

    [Fact]
    public void Un_tiempo_negativo_no_muestra_un_menos()
    {
        // El reloj del sistema puede moverse entre las dos lecturas.
        Assert.Equal("0 ms", TiempoDeSesion.Apertura(TimeSpan.FromMilliseconds(-5)));
    }

    [Theory]
    [InlineData(0, "menos de un minuto")]
    [InlineData(59, "menos de un minuto")]
    public void Abajo_del_minuto_no_se_cuentan_segundos(int segundos, string esperado) =>
        Assert.Equal(esperado, TiempoDeSesion.Antiguedad(TimeSpan.FromSeconds(segundos)));

    [Theory]
    [InlineData(1, "1 minuto")]
    [InlineData(2, "2 minutos")]
    [InlineData(59, "59 minutos")]
    public void Entre_uno_y_sesenta_va_en_minutos(int minutos, string esperado) =>
        Assert.Equal(esperado, TiempoDeSesion.Antiguedad(TimeSpan.FromMinutes(minutos)));

    [Theory]
    [InlineData(60, "1 hora")]
    [InlineData(61, "1 hora y 1 minuto")]
    [InlineData(135, "2 horas y 15 minutos")]
    [InlineData(120, "2 horas")]
    public void De_la_hora_para_arriba_se_dicen_horas_y_minutos(int minutos, string esperado) =>
        Assert.Equal(esperado, TiempoDeSesion.Antiguedad(TimeSpan.FromMinutes(minutos)));

    [Fact]
    public void Sin_haber_conectado_no_se_muestra_nada()
    {
        Assert.Empty(TiempoDeSesion.Componer(null, null, Abrio));
        Assert.Empty(TiempoDeSesion.Componer(TimeSpan.FromSeconds(1), null, Abrio));
        Assert.Empty(TiempoDeSesion.Componer(null, Abrio, Abrio));
    }

    [Fact]
    public void La_linea_completa_dice_las_tres_cosas()
    {
        var texto = TiempoDeSesion.Componer(
            TimeSpan.FromMilliseconds(842), Abrio, Abrio.AddMinutes(7));

        Assert.Equal("abrió en 842 ms · 14:30:12 · hace 7 minutos", texto);
    }

    [Fact]
    public void Recien_conectada_dice_menos_de_un_minuto()
    {
        var texto = TiempoDeSesion.Componer(
            TimeSpan.FromMilliseconds(1200), Abrio, Abrio.AddSeconds(3));

        Assert.Equal("abrió en 1,2 s · 14:30:12 · hace menos de un minuto", texto);
    }

    [Fact]
    public void Un_reloj_que_retrocedio_no_muestra_un_negativo()
    {
        var texto = TiempoDeSesion.Componer(
            TimeSpan.FromMilliseconds(500), Abrio, Abrio.AddMinutes(-5));

        Assert.Contains("hace menos de un minuto", texto, StringComparison.Ordinal);
    }
}
