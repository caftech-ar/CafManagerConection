using CafManagerConection.App.Services;

namespace CafManagerConection.App.Tests.Services;

public sealed class LineaDeTunelTests
{
    [Fact]
    public void Con_el_puerto_habitual_de_ssh_no_se_escribe_el_puerto()
    {
        Assert.Equal(
            "ssh -N -L 15432:localhost:5432 operador@servidor.interno",
            LineaDeTunel.Armar(15432, "localhost", 5432, "operador", "servidor.interno", 22));
    }

    [Fact]
    public void Con_otro_puerto_de_ssh_se_escribe()
    {
        Assert.Equal(
            "ssh -N -p 2222 -L 15432:localhost:5432 operador@servidor.interno",
            LineaDeTunel.Armar(15432, "localhost", 5432, "operador", "servidor.interno", 2222));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_usuario_no_queda_una_arroba_suelta(string usuario)
    {
        var linea = LineaDeTunel.Armar(8080, "localhost", 80, usuario, "servidor", 22);

        Assert.Equal("ssh -N -L 8080:localhost:80 servidor", linea);
        Assert.DoesNotContain("@", linea);
    }

    [Fact]
    public void El_host_remoto_puede_no_ser_localhost()
    {
        Assert.Equal(
            "ssh -N -L 9000:192.0.2.5:9000 root@salto",
            LineaDeTunel.Armar(9000, "192.0.2.5", 9000, "root", "salto", 22));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void Un_puerto_de_ssh_imposible_no_se_escribe(int puertoSsh)
    {
        var linea = LineaDeTunel.Armar(8080, "localhost", 80, "u", "h", puertoSsh);

        Assert.Equal("ssh -N -L 8080:localhost:80 u@h", linea);
        Assert.DoesNotContain("-p", linea);
    }
}
