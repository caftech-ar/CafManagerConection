using CafManagerConection.Ssh;

namespace CafManagerConection.Ssh.Tests;

public sealed class ListadoRemotoTests
{
    private static EntradaCruda Archivo(string nombre, bool enlace = false) =>
        new(nombre, "/srv/" + nombre, false, enlace, 10, DateTimeOffset.UnixEpoch);

    private static EntradaCruda Carpeta(string nombre, bool enlace = false) =>
        new(nombre, "/srv/" + nombre, true, enlace, 0, DateTimeOffset.UnixEpoch);

    [Fact]
    public void El_punto_y_el_doble_punto_no_llegan_al_listado()
    {
        var listado = ListadoRemoto.Filtrar([Carpeta("."), Carpeta(".."), Archivo("datos.txt")]);

        Assert.Equal(["datos.txt"], listado.Entries.Select(e => e.Name));
        Assert.Equal(0, listado.SymbolicLinksOmitted);
    }

    [Fact]
    public void Un_enlace_simbolico_a_archivo_se_omite_y_se_cuenta()
    {
        var listado = ListadoRemoto.Filtrar([Archivo("real.log"), Archivo("actual", enlace: true)]);

        Assert.Equal(["real.log"], listado.Entries.Select(e => e.Name));
        Assert.Equal(1, listado.SymbolicLinksOmitted);
    }

    [Fact]
    public void Un_enlace_simbolico_a_directorio_tambien_se_omite()
    {
        var listado = ListadoRemoto.Filtrar([Carpeta("etc"), Carpeta("alternativas", enlace: true)]);

        Assert.Equal(["etc"], listado.Entries.Select(e => e.Name));
        Assert.Equal(1, listado.SymbolicLinksOmitted);
    }

    [Fact]
    public void Las_carpetas_van_antes_que_los_archivos_y_cada_grupo_por_nombre()
    {
        var listado = ListadoRemoto.Filtrar(
            [Archivo("beta.txt"), Carpeta("zeta"), Archivo("Alfa.txt"), Carpeta("Apache")]);

        Assert.Equal(
            ["Apache", "zeta", "Alfa.txt", "beta.txt"],
            listado.Entries.Select(e => e.Name));
    }

    [Fact]
    public void Una_carpeta_no_declara_tamano()
    {
        var listado = ListadoRemoto.Filtrar(
            [new EntradaCruda("var", "/var", true, false, 4096, DateTimeOffset.UnixEpoch)]);

        Assert.Equal(0, Assert.Single(listado.Entries).SizeBytes);
    }

    [Fact]
    public void Un_listado_de_puros_enlaces_queda_vacio_y_los_declara_todos()
    {
        var listado = ListadoRemoto.Filtrar(
            [Archivo("uno", enlace: true), Carpeta("dos", enlace: true)]);

        Assert.Empty(listado.Entries);
        Assert.Equal(2, listado.SymbolicLinksOmitted);
    }
}
