using CafManagerConection.Domain.Credentials;
using Xunit;

namespace CafManagerConection.Domain.Tests.Credentials;

public sealed class FilaDelVaultTests
{
    private const int Minimas = CifradoDeSecretos.IteracionesMinimas;

    private static FilaDelVault Fila(
        int formato = FilaDelVault.FormatoActual,
        string hash = "SHA512",
        int iteraciones = Minimas) =>
        new(formato, hash, [1, 2, 3], iteraciones, [6], [7]);

    [Fact]
    public void Una_fila_sana_no_da_motivo_para_rechazarla() =>
        Assert.Null(Fila().PorQueNoSeUsa(FilaDelVault.FormatoActual, Minimas));

    [Fact]
    public void Un_formato_mas_nuevo_que_el_conocido_se_rechaza_nombrando_los_dos()
    {
        var motivo = Fila(formato: FilaDelVault.FormatoActual + 1)
            .PorQueNoSeUsa(FilaDelVault.FormatoActual, Minimas);

        Assert.NotNull(motivo);
        Assert.Contains((FilaDelVault.FormatoActual + 1).ToString(), motivo, StringComparison.Ordinal);
        Assert.Contains(FilaDelVault.FormatoActual.ToString(), motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Las_iteraciones_por_debajo_del_piso_se_rechazan()
    {
        var motivo = Fila(iteraciones: 1).PorQueNoSeUsa(FilaDelVault.FormatoActual, Minimas);

        Assert.NotNull(motivo);
        Assert.Contains("iteraciones", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_hash_desconocido_se_rechaza()
    {
        var motivo = Fila(hash: "MD5").PorQueNoSeUsa(FilaDelVault.FormatoActual, Minimas);

        Assert.NotNull(motivo);
        Assert.Contains("MD5", motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void El_nombre_del_hash_se_convierte_en_el_algoritmo_que_pide_el_BCL() =>
        Assert.Equal("SHA512", Fila().Hash.Name);

    [Fact]
    public void El_sobre_del_verificador_se_arma_con_su_nonce()
    {
        var fila = Fila();

        Assert.Equal(fila.VerificadorNonce, fila.SobreDelVerificador.Nonce);
        Assert.Equal(fila.Verificador, fila.SobreDelVerificador.Cifrado);
    }
}
