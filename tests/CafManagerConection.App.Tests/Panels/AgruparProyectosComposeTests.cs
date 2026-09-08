using CafManagerConection.App.Panels;
using CafManagerConection.Platform;

namespace CafManagerConection.App.Tests.Panels;

public sealed class AgruparProyectosComposeTests
{
    private static ContainerInfo Contenedor(
        string nombre, string estado, string proyecto, string servicio) =>
        new("id-" + nombre, nombre, "img", estado, estado, [], proyecto, servicio);

    [Fact]
    public void Un_proyecto_sin_ningun_contenedor_igual_aparece_con_sus_servicios()
    {
        var todos = new List<ContainerInfo>();

        var proyectos = new List<ComposeProject>
        {
            new("miapp", "/srv/miapp/docker-compose.yml",
                [
                    new ComposeService("web", null, false),
                    new ComposeService("db", null, false),
                ]),
        };

        var agrupados = DockerPanel.Agrupar(todos, proyectos);

        var grupo = Assert.Single(agrupados);
        Assert.Equal("miapp", grupo.Nombre);
        Assert.Empty(grupo.Contenedores);
        Assert.Equal(["db", "web"], grupo.ServiciosSinContenedor.OrderBy(s => s));
    }

    [Fact]
    public void Un_servicio_borrado_aparece_junto_a_los_que_si_corren()
    {
        var todos = new List<ContainerInfo>
        {
            Contenedor("miapp-web-1", "running", "miapp", "web"),
        };

        var proyectos = new List<ComposeProject>
        {
            new("miapp", "/srv/miapp/docker-compose.yml",
                [
                    new ComposeService("web", "miapp-web-1", true),
                    new ComposeService("db", null, false),
                ]),
        };

        var agrupados = DockerPanel.Agrupar(todos, proyectos);

        var grupo = Assert.Single(agrupados);
        Assert.Single(grupo.Contenedores);
        Assert.Equal("db", Assert.Single(grupo.ServiciosSinContenedor));
    }

    [Fact]
    public void Sin_informacion_de_compose_agrupa_solo_por_contenedor_como_antes()
    {
        var todos = new List<ContainerInfo>
        {
            Contenedor("miapp-web-1", "running", "miapp", "web"),
            Contenedor("miapp-db-1", "exited", "miapp", "db"),
        };

        var agrupados = DockerPanel.Agrupar(todos, []);

        var grupo = Assert.Single(agrupados);
        Assert.Equal(2, grupo.Contenedores.Count);
        Assert.Empty(grupo.ServiciosSinContenedor);
    }

    [Fact]
    public void Los_contenedores_sueltos_no_forman_grupo()
    {
        var todos = new List<ContainerInfo>
        {
            new("id-1", "suelto", "img", "running", "running", []),
        };

        Assert.Empty(DockerPanel.Agrupar(todos, []));
    }

    [Fact]
    public void Los_proyectos_salen_ordenados_por_nombre()
    {
        var proyectos = new List<ComposeProject>
        {
            new("zeta", "/srv/zeta/compose.yml", [new ComposeService("web", null, false)]),
            new("alfa", "/srv/alfa/compose.yml", [new ComposeService("web", null, false)]),
        };

        var agrupados = DockerPanel.Agrupar([], proyectos);

        Assert.Equal(["alfa", "zeta"], agrupados.Select(g => g.Nombre));
    }
}
