using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class PuertosDeContenedoresTests
{
    private static ContainerInfo Contenedor(
        string nombre, string estado, params string[] puertos) =>
        new("id-" + nombre, nombre, "imagen", estado, estado == "running" ? "Up 3 minutes" : "Exited",
            puertos);

    [Theory]
    [InlineData("8080->80/tcp", new[] { 8080 })]
    [InlineData("0.0.0.0:8080->80/tcp", new[] { 8080 })]
    [InlineData("[::]:8080->80/tcp", new[] { 8080 })]
    [InlineData("127.0.0.1:5432->5432/tcp", new[] { 5432 })]
    [InlineData("53->53/udp", new[] { 53 })]
    public void Un_mapeo_simple_da_su_puerto_del_servidor(string mapeo, int[] esperado)
    {
        Assert.Equal(esperado, PuertosDeContenedores.PuertosDelServidor(mapeo));
    }

    [Fact]
    public void Un_rango_da_todos_sus_puertos()
    {
        var puertos = PuertosDeContenedores.PuertosDelServidor("8000-8003->8000-8003/tcp");

        Assert.Equal([8000, 8001, 8002, 8003], puertos);
    }

    [Theory]
    [InlineData("80/tcp")] 
    [InlineData("")]
    [InlineData("0->80/tcp")]
    [InlineData("99999->80/tcp")]
    [InlineData("8005-8000->8000-8005/tcp")]
    public void Un_mapeo_que_no_publica_nada_no_da_ningun_puerto(string mapeo)
    {
        Assert.Empty(PuertosDeContenedores.PuertosDelServidor(mapeo));
    }

    [Fact]
    public void Un_mapeo_cortado_igual_da_el_puerto_del_servidor()
    {
        Assert.Equal([8080], PuertosDeContenedores.PuertosDelServidor("8080->"));
    }

    [Fact]
    public void El_puerto_queda_a_nombre_del_contenedor_que_lo_publica()
    {
        var mapa = PuertosDeContenedores.PorPuertoDelServidor([
            Contenedor("web", "running", "8080->80/tcp"),
            Contenedor("base", "running", "5432->5432/tcp"),
        ]);

        Assert.Equal("web", mapa[8080]);
        Assert.Equal("base", mapa[5432]);
        Assert.False(mapa.ContainsKey(80));
    }

    [Fact]
    public void Ante_dos_contenedores_con_el_mismo_puerto_gana_el_que_corre()
    {
        var mapa = PuertosDeContenedores.PorPuertoDelServidor([
            Contenedor("viejo", "exited", "8080->80/tcp"),
            Contenedor("nuevo", "running", "8080->80/tcp"),
        ]);

        Assert.Equal("nuevo", mapa[8080]);

        var alRevés = PuertosDeContenedores.PorPuertoDelServidor([
            Contenedor("nuevo", "running", "8080->80/tcp"),
            Contenedor("viejo", "exited", "8080->80/tcp"),
        ]);

        Assert.Equal("nuevo", alRevés[8080]);
    }

    [Theory]
    [InlineData("docker-proxy", true)]
    [InlineData("docker-pr", true)]
    [InlineData("rootlesskit", true)]
    [InlineData("nginx", false)]
    [InlineData("postgres", false)]
    [InlineData("(sin permiso para verlo)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void El_reenviador_de_Docker_se_distingue_de_un_servicio_real(
        string? proceso, bool esperado)
    {
        Assert.Equal(esperado, PuertosDeContenedores.EsReenviadorDeDocker(proceso));
    }

    [Theory]
    [InlineData("d")]
    [InlineData("do")]
    [InlineData("dock")]
    public void Un_nombre_corto_no_pasa_por_el_reenviador_de_Docker(string proceso)
    {
        Assert.False(PuertosDeContenedores.EsReenviadorDeDocker(proceso));
    }
}
