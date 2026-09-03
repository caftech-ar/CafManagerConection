using CafManagerConection.Domain.Credentials;
using Xunit;

namespace CafManagerConection.Domain.Tests.Credentials;

public sealed class FilaDelVaultTests
{
    private static byte[] Bytes(int largo) => Enumerable.Repeat((byte)7, largo).ToArray();

    private static FilaDelVault Fila(
        byte[]? dpapi = null,
        byte[]? sal = null,
        int? iteraciones = null,
        byte[]? nonce = null,
        byte[]? envuelta = null) =>
        new(FilaDelVault.FormatoActual, dpapi, sal, iteraciones, nonce, envuelta);

    [Fact]
    public void Sin_clave_maestra_y_con_dpapi_abre_sola()
    {
        var fila = Fila(dpapi: Bytes(178));

        Assert.False(fila.PideClaveMaestra);
        Assert.True(fila.AbreSola);
        Assert.False(fila.EstaHuerfano);
    }

    [Fact]
    public void Con_clave_maestra_y_sin_dpapi_pide_la_clave()
    {
        var fila = Fila(sal: Bytes(16), iteraciones: 600_000, nonce: Bytes(12), envuelta: Bytes(48));

        Assert.True(fila.PideClaveMaestra);
        Assert.False(fila.AbreSola);
        Assert.False(fila.EstaHuerfano);
    }

    [Fact]
    public void Con_las_dos_envolturas_abre_sola_y_tambien_acepta_la_clave()
    {
        var fila = Fila(
            dpapi: Bytes(178), sal: Bytes(16), iteraciones: 600_000,
            nonce: Bytes(12), envuelta: Bytes(48));

        Assert.True(fila.PideClaveMaestra);
        Assert.True(fila.AbreSola);
    }

    // Es el unico estado imposible de recuperar, asi que tiene que ser detectable.
    [Fact]
    public void Sin_ninguna_envoltura_queda_huerfano()
    {
        Assert.True(Fila().EstaHuerfano);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Una_clave_maestra_a_medias_no_cuenta_como_configurada(
        bool sinSal, bool sinIteraciones, bool sinNonce, bool sinEnvuelta)
    {
        var fila = Fila(
            dpapi: Bytes(178),
            sal: sinSal ? null : Bytes(16),
            iteraciones: sinIteraciones ? null : 600_000,
            nonce: sinNonce ? null : Bytes(12),
            envuelta: sinEnvuelta ? null : Bytes(48));

        Assert.False(fila.PideClaveMaestra);
        Assert.Null(fila.SobreDeLaClaveMaestra);
    }

    [Fact]
    public void El_sobre_de_la_clave_maestra_sale_armado_cuando_esta_completa()
    {
        var fila = Fila(sal: Bytes(16), iteraciones: 600_000, nonce: Bytes(12), envuelta: Bytes(48));

        var sobre = fila.SobreDeLaClaveMaestra;

        Assert.NotNull(sobre);
        Assert.Equal(12, sobre!.Nonce.Length);
        Assert.True(sobre.EsValido);
    }

    [Fact]
    public void Cero_iteraciones_no_cuenta_como_configurada()
    {
        var fila = Fila(sal: Bytes(16), iteraciones: 0, nonce: Bytes(12), envuelta: Bytes(48));

        Assert.False(fila.PideClaveMaestra);
    }
}
