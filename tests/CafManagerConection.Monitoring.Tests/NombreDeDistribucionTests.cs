namespace CafManagerConection.Monitoring.Tests;

public sealed class NombreDeDistribucionTests
{
    [Theory]
    [InlineData("Ubuntu 22.04.3 LTS", "Ubuntu 22.04")]
    [InlineData("Ubuntu 24.04 LTS", "Ubuntu 24.04")]
    [InlineData("Debian GNU/Linux 12 (bookworm)", "Debian 12")]
    [InlineData("Oracle Linux Server 8.9", "Oracle 8.9")]
    [InlineData("Red Hat Enterprise Linux 9.4 (Plow)", "Red 9.4")]
    [InlineData("Rocky Linux 9.3 (Blue Onyx)", "Rocky 9.3")]
    [InlineData("Alpine Linux v3.19", "Alpine 3.19")]
    public void El_nombre_largo_se_abrevia(string completo, string esperado) =>
        Assert.Equal(esperado, NombreDeDistribucion.Abreviar(completo));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_nombre_no_se_inventa_ninguno(string completo) =>
        Assert.Equal(string.Empty, NombreDeDistribucion.Abreviar(completo));

    [Fact]
    public void Una_distribucion_sin_version_conserva_su_nombre() =>
        Assert.Equal("Arch", NombreDeDistribucion.Abreviar("Arch Linux"));

    [Fact]
    public void Nunca_queda_mas_largo_que_el_original() =>
        Assert.True(
            NombreDeDistribucion.Abreviar("Oracle Linux Server 8.9").Length
            <= "Oracle Linux Server 8.9".Length);
}
