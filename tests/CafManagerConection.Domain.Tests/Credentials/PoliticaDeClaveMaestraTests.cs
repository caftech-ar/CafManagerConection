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
    public void El_espacio_cuenta_como_caracter_especial()
    {
        // Una frase con espacios y un digito alcanza, y es mas fuerte que ocho con un signo.
        Assert.True(PoliticaDeClaveMaestra.Cumple("cuatro palabras al azar 7"));
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
