using CafManagerConection.Domain.Credentials;
using Xunit;

namespace CafManagerConection.Domain.Tests.Credentials;

public sealed class PoliticaDeClaveMaestraTests
{
    [Theory]
    [InlineData("abcd123!")]
    [InlineData("Zorro-Verde-2026!")]
    [InlineData("una frase larga con 4 palabras!")]
    [InlineData("ñandú-2026!")]
    [InlineData("clave 1 con espacios!")]
    public void Acepta_lo_que_cumple(string clave) =>
        Assert.True(PoliticaDeClaveMaestra.Cumple(clave));

    [Theory]
    [InlineData("abc12!", FaltaEnLaClaveMaestra.EsCorta)]
    [InlineData("1234567!", FaltaEnLaClaveMaestra.SinLetra)]
    [InlineData("abcdefg!", FaltaEnLaClaveMaestra.SinDigito)]
    [InlineData("abcd1234", FaltaEnLaClaveMaestra.SinCaracterEspecial)]
    [InlineData("", FaltaEnLaClaveMaestra.EsCorta)]
    public void Dice_exactamente_que_falta(string clave, FaltaEnLaClaveMaestra esperada) =>
        Assert.Equal(esperada, PoliticaDeClaveMaestra.Revisar(clave));

    [Fact]
    public void El_espacio_se_acepta_pero_no_cuenta_como_caracter_especial()
    {
        Assert.Equal(
            FaltaEnLaClaveMaestra.SinCaracterEspecial,
            PoliticaDeClaveMaestra.Revisar("cuatro palabras al azar 7"));

        Assert.True(PoliticaDeClaveMaestra.Cumple("cuatro palabras al azar 7!"));
    }

    [Theory]
    [InlineData("abcdefg1 ", FaltaEnLaClaveMaestra.SinCaracterEspecial)]
    [InlineData("abcdefg1	", FaltaEnLaClaveMaestra.SinCaracterEspecial)]
    [InlineData("abcdefg1!", FaltaEnLaClaveMaestra.Nada)]
    public void Un_espacio_al_final_no_alcanza_como_caracter_especial(
        string clave, FaltaEnLaClaveMaestra esperada) =>
        Assert.Equal(esperada, PoliticaDeClaveMaestra.Revisar(clave));

    [Theory]
    [InlineData("abc123", FuerzaDeLaClaveMaestra.Insuficiente)]
    [InlineData("abcdefg1!", FuerzaDeLaClaveMaestra.Debil)]
    [InlineData("Abcdefg1!", FuerzaDeLaClaveMaestra.Aceptable)]
    [InlineData("Abcdefghij12!", FuerzaDeLaClaveMaestra.Buena)]
    [InlineData("cuatro palabras al azar bien largas 7!", FuerzaDeLaClaveMaestra.Fuerte)]
    public void La_fuerza_premia_el_largo_por_sobre_la_variedad(
        string clave, FuerzaDeLaClaveMaestra esperada) =>
        Assert.Equal(esperada, PoliticaDeClaveMaestra.Fuerza(clave));

    [Fact]
    public void Una_clave_que_no_cumple_nunca_tiene_fuerza()
    {
        foreach (var clave in new[] { "corta1!", "sinnumeros!", "12345678!" })
        {
            Assert.Equal(FuerzaDeLaClaveMaestra.Insuficiente, PoliticaDeClaveMaestra.Fuerza(clave));
        }
    }

    [Fact]
    public void Una_frase_larga_no_se_rechaza_por_larga()
    {
        var larga = new string('a', 500) + "1!";

        Assert.True(PoliticaDeClaveMaestra.Cumple(larga));
    }

    [Fact]
    public void Cada_falta_tiene_una_explicacion_y_ninguna_esta_vacia()
    {
        foreach (var falta in Enum.GetValues<FaltaEnLaClaveMaestra>())
        {
            var texto = PoliticaDeClaveMaestra.Explicar(falta);

            if (falta == FaltaEnLaClaveMaestra.Nada)
            {
                Assert.Empty(texto);
                continue;
            }

            Assert.False(
                string.IsNullOrWhiteSpace(texto),
                $"{falta} no tiene explicación, así que la ventana no puede decir qué falta.");
        }
    }
}
