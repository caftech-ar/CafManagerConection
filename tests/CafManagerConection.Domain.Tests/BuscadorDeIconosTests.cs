using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

public sealed class BuscadorDeIconosTests
{
    [Fact]
    public void Sin_criterios_devuelve_todo_lo_que_el_selector_ofrece()
    {
        var resultado = BuscadorDeIconos.Filtrar();

        Assert.All(resultado, icono => Assert.True(icono.EnElSelector));
        Assert.Equal(
            CatalogoDeIconos.Iconos.Count(icono => icono.EnElSelector),
            resultado.Count);
    }

    [Fact]
    public void Los_grupos_de_la_interfaz_no_salen_en_el_selector()
    {
        Assert.Empty(BuscadorDeIconos.Filtrar("filtrar"));
        Assert.Empty(BuscadorDeIconos.Filtrar("minimizar"));

        Assert.Contains(
            BuscadorDeIconos.Filtrar("filtrar", soloDelSelector: false),
            icono => icono.Clave == "filter");
    }

    [Fact]
    public void Busca_por_etiqueta()
    {
        Assert.Contains(BuscadorDeIconos.Filtrar("servidor"), icono => icono.Clave == "server");
    }

    [Fact]
    public void Busca_por_nombre_de_archivo()
    {
        Assert.Contains(BuscadorDeIconos.Filtrar("terminal-2"), icono => icono.Clave == "terminal-2");
    }

    [Theory]
    [InlineData("maquina virtual")]
    [InlineData("máquina virtual")]
    [InlineData("MAQUINA VIRTUAL")]
    [InlineData("Máquina Virtual")]
    public void No_distingue_acentos_ni_mayusculas(string escrito)
    {
        Assert.Contains(BuscadorDeIconos.Filtrar(escrito), icono => icono.Clave == "cube");
    }

    [Theory]
    [InlineData("vm", "cube")]
    [InlineData("db", "database")]
    [InlineData("fw", "wall")]
    [InlineData("rdp", "device-desktop-share")]
    [InlineData("ssh", "terminal-2")]
    [InlineData("sftp", "transfer")]
    public void Busca_por_sinonimo(string escrito, string esperado)
    {
        Assert.Contains(BuscadorDeIconos.Filtrar(escrito), icono => icono.Clave == esperado);
    }

    [Fact]
    public void Acota_por_grupo()
    {
        var resultado = BuscadorDeIconos.Filtrar(grupo: CatalogoDeIconos.Red);

        Assert.NotEmpty(resultado);
        Assert.All(resultado, icono => Assert.Equal(CatalogoDeIconos.Red, icono.Grupo));
    }

    [Fact]
    public void Acota_por_familia()
    {
        var logos = BuscadorDeIconos.Filtrar(familia: FamiliaDeIcono.Logo);

        Assert.NotEmpty(logos);
        Assert.All(logos, icono => Assert.Equal(FamiliaDeIcono.Logo, icono.Grupo.Familia));
    }

    [Fact]
    public void El_filtro_de_grupo_se_combina_con_lo_escrito()
    {
        var resultado = BuscadorDeIconos.Filtrar("sql", grupo: CatalogoDeIconos.MotoresDeDatos);

        Assert.NotEmpty(resultado);
        Assert.All(resultado, icono => Assert.Equal(CatalogoDeIconos.MotoresDeDatos, icono.Grupo));
        Assert.DoesNotContain(resultado, icono => icono.Grupo == CatalogoDeIconos.TerminalYAcceso);
    }

    [Fact]
    public void Lo_que_no_coincide_con_nada_devuelve_vacio()
    {
        Assert.Empty(BuscadorDeIconos.Filtrar("zzzzz"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_buscador_vacio_no_filtra(string? escrito)
    {
        Assert.Equal(BuscadorDeIconos.Filtrar().Count, BuscadorDeIconos.Filtrar(escrito).Count);
    }

    [Fact]
    public void El_resultado_conserva_el_orden_del_catalogo()
    {
        var resultado = BuscadorDeIconos.Filtrar(familia: FamiliaDeIcono.Logo);
        var esperado = CatalogoDeIconos.Iconos
            .Where(icono => icono.EnElSelector && icono.Grupo.Familia == FamiliaDeIcono.Logo)
            .ToList();

        Assert.Equal(esperado, resultado);
    }
}
