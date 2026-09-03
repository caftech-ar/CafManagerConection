using CafManagerConection.Ssh;

namespace CafManagerConection.Ssh.Tests;

public sealed class HostKeyPolicyTests
{
    private const string Huella = "SHA256:CWssi821D0fiPfWBwerVnZKDHwiGni/kzdyXdPzTyE8";

    [Fact]
    public void La_misma_clave_se_reconoce_y_no_hay_que_preguntar()
    {
        Assert.True(HostKeyPolicy.YaEsConocida(Huella, Huella));
    }

    [Fact]
    public void Una_clave_distinta_no_se_reconoce()
    {
        const string otra = "SHA256:PRJO4wILjxSeTcS4DyKudjXQNwjXelQMys1uqlTA6CI";

        Assert.False(HostKeyPolicy.YaEsConocida(Huella, otra));
    }

    [Fact]
    public void Sin_clave_guardada_hay_que_preguntar()
    {
        Assert.False(HostKeyPolicy.YaEsConocida(Huella, null));
        Assert.False(HostKeyPolicy.YaEsConocida(Huella, string.Empty));
        Assert.False(HostKeyPolicy.YaEsConocida(Huella, "   "));
    }

    [Fact]
    public void Sin_clave_presentada_no_se_reconoce_nada()
    {
        Assert.False(HostKeyPolicy.YaEsConocida(string.Empty, Huella));
        Assert.False(HostKeyPolicy.YaEsConocida("   ", Huella));
    }

    [Fact]
    public void Los_espacios_alrededor_no_impiden_reconocerla()
    {
        Assert.True(HostKeyPolicy.YaEsConocida(Huella, "  " + Huella + "  "));
        Assert.True(HostKeyPolicy.YaEsConocida(" " + Huella, Huella));
    }

    [Fact]
    public void La_comparacion_distingue_mayusculas()
    {
        var enMinusculas = Huella.ToLowerInvariant();

        Assert.False(HostKeyPolicy.YaEsConocida(enMinusculas, Huella));
    }

    [Fact]
    public void Una_clave_sin_el_prefijo_no_se_da_por_equivalente()
    {
        var sinPrefijo = Huella["SHA256:".Length..];

        Assert.False(HostKeyPolicy.YaEsConocida(Huella, sinPrefijo));
        Assert.False(HostKeyPolicy.YaEsConocida(sinPrefijo, Huella));
    }

    [Fact]
    public void Una_clave_que_es_prefijo_de_la_otra_no_se_reconoce()
    {
        Assert.False(HostKeyPolicy.YaEsConocida(Huella[..30], Huella));
        Assert.False(HostKeyPolicy.YaEsConocida(Huella, Huella[..30]));
    }
}
