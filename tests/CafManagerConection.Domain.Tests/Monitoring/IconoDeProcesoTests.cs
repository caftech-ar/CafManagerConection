using CafManagerConection.Domain.Monitoring;
using Xunit;

namespace CafManagerConection.Domain.Tests.Monitoring;

public sealed class IconoDeProcesoTests
{
    [Theory]
    [InlineData("dockerd", "IconoPanelDocker")]
    [InlineData("nginx", "IconoPanelNginx")]
    [InlineData("supervisord", "IconoPanelSupervisor")]
    [InlineData("sshd", "IconoSsh")]
    [InlineData("postgres", "IconoBaseDeDatos")]
    [InlineData("php-fpm", "IconoWeb")]
    [InlineData("bash", "IconoTerminalExterna")]
    [InlineData("dotnet", "IconoAplicacion")]
    [InlineData("java", "IconoAplicacion")]
    [InlineData("javaw", "IconoAplicacion")]
    public void Los_conocidos_tienen_su_icono(string nombre, string esperada) =>
        Assert.Equal(esperada, IconoDeProceso.ClaveDeIcono(nombre));

    // Las dos formas en que aparecen de verdad en /proc: el nombre del binario a secas, y con
    // la version pegada de un paquete distribuido asi.
    [Theory]
    [InlineData("dotnet", "IconoAplicacion")]
    [InlineData("dotnet-8", "IconoAplicacion")]
    [InlineData("java-17", "IconoAplicacion")]
    public void Dotnet_y_java_se_reconocen_con_o_sin_version(string nombre, string esperada) =>
        Assert.Equal(esperada, IconoDeProceso.ClaveDeIcono(nombre));

    [Theory]
    [InlineData("DOCKERD")]
    [InlineData("Nginx")]
    public void No_distingue_mayusculas(string nombre) =>
        Assert.NotNull(IconoDeProceso.ClaveDeIcono(nombre));

    // «nginx: worker process» y «php-fpm: pool www» son el mismo binario con el rol pegado.
    [Theory]
    [InlineData("nginx: worker process", "IconoPanelNginx")]
    [InlineData("php-fpm: pool www", "IconoWeb")]
    [InlineData("postgres: checkpointer", "IconoBaseDeDatos")]
    public void El_rol_pegado_al_nombre_no_lo_esconde(string nombre, string esperada) =>
        Assert.Equal(esperada, IconoDeProceso.ClaveDeIcono(nombre));

    [Theory]
    [InlineData("python3.11", "IconoAplicacion")]
    [InlineData("postgres-16", "IconoBaseDeDatos")]
    public void La_version_pegada_al_nombre_no_lo_esconde(string nombre, string esperada) =>
        Assert.Equal(esperada, IconoDeProceso.ClaveDeIcono(nombre));

    [Theory]
    [InlineData("kworker/0:1")]
    [InlineData("un-binario-propio")]
    [InlineData("ksoftirqd")]
    public void Lo_que_no_se_reconoce_no_lleva_icono(string nombre)
    {
        // Un icono genérico para todo hace que ninguno signifique nada.
        Assert.Null(IconoDeProceso.ClaveDeIcono(nombre));
        Assert.False(IconoDeProceso.EsConocido(nombre));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_nombre_no_hay_icono(string? nombre) =>
        Assert.Null(IconoDeProceso.ClaveDeIcono(nombre));

    [Fact]
    public void El_nombre_llega_con_espacios_y_se_reconoce_igual() =>
        Assert.Equal("IconoPanelDocker", IconoDeProceso.ClaveDeIcono("  dockerd  "));

    [Fact]
    public void No_entra_en_recursion_infinita_con_dos_puntos_al_principio() =>
        Assert.Null(IconoDeProceso.ClaveDeIcono(":algo"));

    [Fact]
    public void No_entra_en_recursion_infinita_con_solo_dos_puntos() =>
        Assert.Null(IconoDeProceso.ClaveDeIcono(":"));
}
