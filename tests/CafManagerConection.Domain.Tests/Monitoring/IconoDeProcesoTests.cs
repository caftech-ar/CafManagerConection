using CafManagerConection.Domain.Monitoring;
using CafManagerConection.Domain.Settings;
using Xunit;

namespace CafManagerConection.Domain.Tests.Monitoring;

public sealed class IconoDeProcesoTests
{
    [Theory]
    [InlineData("dockerd", "brand-docker")]
    [InlineData("nginx", "nginx")]
    [InlineData("supervisord", "settings-automation")]
    [InlineData("sshd", "terminal-2")]
    [InlineData("postgres", "postgresql")]
    [InlineData("php-fpm", "brand-php")]
    [InlineData("bash", "gnubash")]
    [InlineData("dotnet", "dotnet")]
    [InlineData("java", "openjdk")]
    [InlineData("javaw", "openjdk")]
    public void Los_conocidos_tienen_su_icono(string nombre, string esperada) =>
        Assert.Equal(esperada, IconoDeProceso.ClaveDeIcono(nombre));

    // Las dos formas en que aparecen de verdad en /proc: el nombre del binario a secas, y con
    // la version pegada de un paquete distribuido asi.
    [Theory]
    [InlineData("dotnet", "dotnet")]
    [InlineData("dotnet-8", "dotnet")]
    [InlineData("java-17", "openjdk")]
    public void Dotnet_y_java_se_reconocen_con_o_sin_version(string nombre, string esperada) =>
        Assert.Equal(esperada, IconoDeProceso.ClaveDeIcono(nombre));

    [Theory]
    [InlineData("DOCKERD")]
    [InlineData("Nginx")]
    public void No_distingue_mayusculas(string nombre) =>
        Assert.NotNull(IconoDeProceso.ClaveDeIcono(nombre));

    // «nginx: worker process» y «php-fpm: pool www» son el mismo binario con el rol pegado.
    [Theory]
    [InlineData("nginx: worker process", "nginx")]
    [InlineData("php-fpm: pool www", "brand-php")]
    [InlineData("postgres: checkpointer", "postgresql")]
    public void El_rol_pegado_al_nombre_no_lo_esconde(string nombre, string esperada) =>
        Assert.Equal(esperada, IconoDeProceso.ClaveDeIcono(nombre));

    [Theory]
    [InlineData("python3.11", "brand-python")]
    [InlineData("postgres-16", "postgresql")]
    public void La_version_pegada_al_nombre_no_lo_esconde(string nombre, string esperada) =>
        Assert.Equal(esperada, IconoDeProceso.ClaveDeIcono(nombre));

    // Devolver null desarmaba la columna: el control se colapsaba y el nombre saltaba a la
    // izquierda. Lo que evita que el icono se vuelva textura es el enfasis, y para eso esta
    // EsConocido: quien dibuja pinta el genérico más apagado.
    [Theory]
    [InlineData("kworker/0:1")]
    [InlineData("un-binario-propio")]
    [InlineData("ksoftirqd")]
    public void Lo_que_no_se_reconoce_lleva_el_generico(string nombre)
    {
        Assert.Equal(IconosPorOmision.Desconocido, IconoDeProceso.ClaveDeIcono(nombre));
        Assert.False(IconoDeProceso.EsConocido(nombre));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_nombre_tambien_lleva_el_generico(string? nombre)
    {
        Assert.Equal(IconosPorOmision.Desconocido, IconoDeProceso.ClaveDeIcono(nombre));
        Assert.False(IconoDeProceso.EsConocido(nombre));
    }

    [Fact]
    public void Un_proceso_reconocido_se_distingue_de_uno_que_no()
    {
        Assert.True(IconoDeProceso.EsConocido("nginx"));
        Assert.False(IconoDeProceso.EsConocido("un-binario-propio"));
    }

    [Fact]
    public void Toda_clave_que_devuelve_existe_en_el_catalogo()
    {
        foreach (var nombre in new[] { "nginx", "postgres", "un-binario-propio", "", "kworker/0:1" })
        {
            var clave = IconoDeProceso.ClaveDeIcono(nombre);

            Assert.True(CatalogoDeIconos.EsValido(clave), $"«{nombre}» devolvió «{clave}».");
        }
    }

    [Fact]
    public void El_nombre_llega_con_espacios_y_se_reconoce_igual() =>
        Assert.Equal("brand-docker", IconoDeProceso.ClaveDeIcono("  dockerd  "));

    [Fact]
    public void No_entra_en_recursion_infinita_con_dos_puntos_al_principio() =>
        Assert.Equal(IconosPorOmision.Desconocido, IconoDeProceso.ClaveDeIcono(":algo"));

    [Fact]
    public void No_entra_en_recursion_infinita_con_solo_dos_puntos() =>
        Assert.Equal(IconosPorOmision.Desconocido, IconoDeProceso.ClaveDeIcono(":"));
}
