using System.Security.Cryptography;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Infrastructure.Credentials;
using Xunit;

namespace CafManagerConection.Infrastructure.Tests.Credentials;

public sealed class ProteccionDpapiTests
{
    [Fact]
    public void La_clave_del_vault_va_y_vuelve()
    {
        var clave = CifradoDeSecretos.ClaveNueva();

        var envuelto = ProteccionDpapi.Proteger(clave);
        var vuelta = ProteccionDpapi.Desproteger(envuelto);

        Assert.Equal(Convert.ToHexString(clave), Convert.ToHexString(vuelta));
    }

    [Fact]
    public void El_envuelto_no_contiene_la_clave_en_claro()
    {
        var clave = CifradoDeSecretos.ClaveNueva();

        var envuelto = Convert.ToHexString(ProteccionDpapi.Proteger(clave));

        Assert.DoesNotContain(Convert.ToHexString(clave), envuelto, StringComparison.Ordinal);
    }

    // Es lo que sostiene el camino de «DPAPI fallo, pedi la clave maestra»: un blob de otro
    // usuario o de otra maquina tiene que fallar, no devolver bytes distintos en silencio.
    [Fact]
    public void Un_envuelto_tocado_se_rechaza()
    {
        var envuelto = ProteccionDpapi.Proteger(CifradoDeSecretos.ClaveNueva());

        envuelto[envuelto.Length / 2] ^= 0xFF;

        Assert.Throws<CryptographicException>(() => ProteccionDpapi.Desproteger(envuelto));
    }

    [Fact]
    public void Un_envuelto_vacio_o_basura_se_rechaza()
    {
        Assert.Throws<CryptographicException>(() => ProteccionDpapi.Desproteger([1, 2, 3, 4]));
    }

    [Fact]
    public void Envolver_la_misma_clave_dos_veces_no_da_el_mismo_blob()
    {
        var clave = CifradoDeSecretos.ClaveNueva();

        Assert.NotEqual(
            Convert.ToHexString(ProteccionDpapi.Proteger(clave)),
            Convert.ToHexString(ProteccionDpapi.Proteger(clave)));
    }

    [Fact]
    public void Cien_vueltas_no_pierden_memoria_no_administrada()
    {
        var clave = CifradoDeSecretos.ClaveNueva();

        for (var i = 0; i < 100; i++)
        {
            var vuelta = ProteccionDpapi.Desproteger(ProteccionDpapi.Proteger(clave));
            Assert.Equal(clave.Length, vuelta.Length);
        }
    }
}
