using CafManagerConection.Domain.Settings;
using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class EscaladaDeLecturaTests
{
    [Fact]
    public void El_comando_guardado_corta_antes_de_leer_cuando_el_canario_falla()
    {
        var guardado = EscaladaDeLectura.Guardado("ss -tulnpH");

        Assert.StartsWith(EscaladaDeLectura.Canario, guardado);
        Assert.Contains("|| exit 1", guardado);
        Assert.EndsWith("ss -tulnpH", guardado);
    }

    // El canario tiene que fallar antes del comando: A && B; C deja correr C igual y el estado final vuelve a ser cero.
    [Fact]
    public void El_canario_no_se_encadena_con_y_logico()
    {
        Assert.DoesNotContain("&&", EscaladaDeLectura.Guardado("cat /proc/uptime; exit 0"));
    }

    [Fact]
    public void El_canario_lee_algo_que_solo_root_puede_leer()
    {
        Assert.Contains("/proc/1/io", EscaladaDeLectura.Canario);
    }

    [Fact]
    public void Guardar_dos_veces_no_apila_canarios()
    {
        var una = EscaladaDeLectura.Guardado("ss -tulnpH");

        Assert.Equal(una, EscaladaDeLectura.Guardado(una));
    }

    [Theory]
    [InlineData(ResultadoDeSondeo.SinContrasena, true)]
    [InlineData(ResultadoDeSondeo.PideContrasena, true)]
    [InlineData(ResultadoDeSondeo.Imposible, false)]
    public void El_boton_sale_solo_cuando_la_escalada_es_posible(
        ResultadoDeSondeo resultado, bool esperado) =>
        Assert.Equal(esperado, MensajeDeEscalada.MuestraElBoton(resultado));

    [Fact]
    public void Sin_sondeo_todavia_no_se_muestra_ningun_boton()
    {
        Assert.False(MensajeDeEscalada.MuestraElBoton(null));
        Assert.Contains("Todavía", MensajeDeEscalada.Texto(null, "los procesos ajenos"));
    }

    [Fact]
    public void Quien_no_esta_en_sudoers_lo_lee_con_esas_palabras()
    {
        var texto = MensajeDeEscalada.Texto(ResultadoDeSondeo.Imposible, "los procesos ajenos");

        Assert.Contains("sudoers", texto);
        Assert.Contains("los procesos ajenos", texto);
        Assert.DoesNotContain("contraseña", texto);
    }

    // FR-184d: «no estás en sudoers» y «sudo te va a pedir la contraseña» exigen cosas distintas del usuario.
    [Fact]
    public void Pedir_la_contrasena_no_se_confunde_con_no_poder()
    {
        var pide = MensajeDeEscalada.Texto(ResultadoDeSondeo.PideContrasena, "los procesos ajenos");

        Assert.Contains("contraseña", pide);
        Assert.DoesNotContain("sudoers", pide);
    }

    [Fact]
    public void Con_sudo_sin_contrasena_el_texto_no_habla_de_contrasenas()
    {
        var texto = MensajeDeEscalada.Texto(
            ResultadoDeSondeo.SinContrasena, "los procesos ajenos");

        Assert.Contains("los procesos ajenos", texto);
        Assert.DoesNotContain("contraseña", texto);
        Assert.DoesNotContain("sudoers", texto);
    }

    [Fact]
    public void Los_cuatro_estados_dicen_algo_distinto()
    {
        string[] textos =
        [
            MensajeDeEscalada.Texto(null, "x"),
            MensajeDeEscalada.Texto(ResultadoDeSondeo.Imposible, "x"),
            MensajeDeEscalada.Texto(ResultadoDeSondeo.PideContrasena, "x"),
            MensajeDeEscalada.Texto(ResultadoDeSondeo.SinContrasena, "x"),
        ];

        Assert.Equal(4, textos.Distinct(StringComparer.Ordinal).Count());
        Assert.All(textos, t => Assert.False(string.IsNullOrWhiteSpace(t)));
    }
}
