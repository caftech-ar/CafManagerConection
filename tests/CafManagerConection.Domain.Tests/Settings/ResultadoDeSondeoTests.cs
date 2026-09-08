using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests.Settings;

public sealed class ResultadoDeSondeoTests
{
    [Fact]
    public void El_codigo_cero_dice_que_escala_sin_contrasena()
    {
        var resultado = SondeoDeSudo.Interpretar(0, string.Empty, string.Empty);

        Assert.Equal(ResultadoDeSondeo.SinContrasena, resultado);
    }

    [Fact]
    public void Sudo_que_pide_la_contrasena_no_se_confunde_con_no_estar_en_sudoers()
    {
        var resultado = SondeoDeSudo.Interpretar(
            1, string.Empty, "sudo: a password is required\n");

        Assert.Equal(ResultadoDeSondeo.PideContrasena, resultado);
        Assert.NotEqual(ResultadoDeSondeo.Imposible, resultado);
    }

    [Theory]
    [InlineData("sudo: no tty present and no askpass program specified")]
    [InlineData("sudo: a terminal is required to read the password")]
    public void Los_otros_avisos_de_sudo_sin_terminal_tambien_son_pedido_de_contrasena(string error)
    {
        Assert.Equal(
            ResultadoDeSondeo.PideContrasena,
            SondeoDeSudo.Interpretar(1, string.Empty, error));
    }

    [Fact]
    public void Quien_no_esta_en_sudoers_no_puede_escalar()
    {
        var resultado = SondeoDeSudo.Interpretar(
            1, string.Empty, "andres is not in the sudoers file.\n");

        Assert.Equal(ResultadoDeSondeo.Imposible, resultado);
    }

    [Theory]
    [InlineData("Sorry, user andres is not allowed to execute '/bin/true' as root on srv01.")]
    [InlineData("Sorry, user andres may not run sudo on srv01.")]
    [InlineData("sudo: unknown user: andres")]
    public void El_veto_de_sudoers_en_cualquiera_de_sus_formas_es_imposible(string error)
    {
        Assert.Equal(
            ResultadoDeSondeo.Imposible, SondeoDeSudo.Interpretar(1, string.Empty, error));
    }

    [Fact]
    public void Un_fallo_sin_motivo_reconocible_se_declara_imposible_y_no_pedido_de_contrasena()
    {
        var resultado = SondeoDeSudo.Interpretar(127, string.Empty, "sudo: command not found");

        Assert.Equal(ResultadoDeSondeo.Imposible, resultado);
    }

    [Fact]
    public void El_aviso_que_llega_por_la_salida_estandar_cuenta_igual()
    {
        var resultado = SondeoDeSudo.Interpretar(
            1, "sudo: a password is required", string.Empty);

        Assert.Equal(ResultadoDeSondeo.PideContrasena, resultado);
    }

    [Fact]
    public void Tolera_CRLF_y_mayusculas()
    {
        var resultado = SondeoDeSudo.Interpretar(
            1, string.Empty, "sudo: A PASSWORD IS REQUIRED\r\n");

        Assert.Equal(ResultadoDeSondeo.PideContrasena, resultado);
    }

    [Fact]
    public void El_codigo_cero_manda_sobre_cualquier_ruido_en_la_salida()
    {
        var resultado = SondeoDeSudo.Interpretar(
            0, string.Empty, "sudo: unable to resolve host srv01");

        Assert.Equal(ResultadoDeSondeo.SinContrasena, resultado);
    }

    [Fact]
    public void Solo_lo_imposible_apaga_el_boton_de_reintentar_con_privilegios()
    {
        Assert.True(ResultadoDeSondeo.SinContrasena.PuedeEscalar());
        Assert.True(ResultadoDeSondeo.PideContrasena.PuedeEscalar());
        Assert.False(ResultadoDeSondeo.Imposible.PuedeEscalar());
    }

    [Fact]
    public void Solo_el_primero_escala_sin_molestar_al_usuario()
    {
        Assert.True(ResultadoDeSondeo.SinContrasena.EscalaSinPreguntar());
        Assert.False(ResultadoDeSondeo.PideContrasena.EscalaSinPreguntar());
        Assert.False(ResultadoDeSondeo.Imposible.EscalaSinPreguntar());
    }

    [Fact]
    public void Los_tres_estados_estan_y_no_hay_un_cuarto()
    {
        Assert.Equal(3, Enum.GetValues<ResultadoDeSondeo>().Length);
    }

    [Fact]
    public void Sin_sondear_todavia_no_se_asume_que_pueda_escalar()
    {
        Assert.Equal(default, ResultadoDeSondeo.Imposible);
    }
}
