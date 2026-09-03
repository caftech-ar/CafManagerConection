using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Folders;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Folders;

// FR-193a y FR-193c: el lugar alfabético de un alta y el «ordenar alfabéticamente» de una carpeta.
public sealed class OrdenAlfabeticoTests
{
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    private readonly List<(Guid? Padre, List<Guid> Orden)> _carpetasReordenadas = [];
    private readonly List<(Guid? Padre, List<Guid> Orden)> _conexionesReordenadas = [];

    public OrdenAlfabeticoTests()
    {
        _carpetas
            .When(r => r.ReorderAsync(
                Arg.Any<Guid?>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()))
            .Do(ci => _carpetasReordenadas.Add(
                (ci.ArgAt<Guid?>(0), [.. ci.ArgAt<IReadOnlyList<Guid>>(1)])));

        _conexiones
            .When(r => r.ReorderAsync(
                Arg.Any<Guid?>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()))
            .Do(ci => _conexionesReordenadas.Add(
                (ci.ArgAt<Guid?>(0), [.. ci.ArgAt<IReadOnlyList<Guid>>(1)])));
    }

    private FolderService Servicio() => new(_carpetas, _conexiones, _credenciales);

    private void Arbol(List<Folder> carpetas, List<Connection> conexiones)
    {
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(carpetas);
        _conexiones.GetAllAsync(Arg.Any<CancellationToken>()).Returns(conexiones);
    }

    private static Connection En(Guid? carpeta, string nombre, int orden) =>
        new(Guid.NewGuid(), nombre, Protocol.Ssh, "192.0.2.1")
        {
            FolderId = carpeta,
            SortOrder = orden,
        };

    [Fact]
    public void Un_nombre_entra_donde_lo_pondria_el_alfabeto()
    {
        Assert.Equal(0, OrdenAlfabetico.Posicion(["Bravo", "Charlie"], "Alfa"));
        Assert.Equal(1, OrdenAlfabetico.Posicion(["Bravo", "Charlie"], "Bruno"));
        Assert.Equal(2, OrdenAlfabetico.Posicion(["Bravo", "Charlie"], "Delta"));
    }

    [Fact]
    public void El_acento_no_manda_el_nombre_al_final()
    {
        Assert.Equal(0, OrdenAlfabetico.Posicion(["Bravo", "Charlie"], "Álvarez"));
        Assert.Equal(2, OrdenAlfabetico.Posicion(["Alfa", "Bravo"], "Ñandú"));
    }

    [Fact]
    public void Las_mayusculas_no_cambian_el_lugar()
    {
        Assert.Equal(
            OrdenAlfabetico.Posicion(["alfa", "charlie"], "BRAVO"),
            OrdenAlfabetico.Posicion(["ALFA", "CHARLIE"], "bravo"));
    }

    [Fact]
    public void Un_nombre_vacio_de_hermanos_va_al_lugar_cero() =>
        Assert.Equal(0, OrdenAlfabetico.Posicion([], "Cualquiera"));

    [Fact]
    public async Task Ordenar_una_carpeta_deja_sus_conexiones_por_nombre()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        var zeta = En(carpeta.Id, "Zeta", 0);
        var alfa = En(carpeta.Id, "Alfa", 1);
        var bravo = En(carpeta.Id, "Bravo", 2);

        Arbol([carpeta], [zeta, alfa, bravo]);

        await Servicio().OrdenarHijosAsync(carpeta.Id);

        Assert.Equal(
            [alfa.Id, bravo.Id, zeta.Id],
            _conexionesReordenadas.Single(r => r.Padre == carpeta.Id).Orden);
    }

    [Fact]
    public async Task Ordenar_una_carpeta_deja_sus_subcarpetas_por_nombre()
    {
        var raiz = new Folder(Guid.NewGuid(), "Trabajo");
        var vial = new Folder(Guid.NewGuid(), "Vial", raiz.Id, 0);
        var apuestas = new Folder(Guid.NewGuid(), "Apuestas", raiz.Id, 1);

        Arbol([raiz, vial, apuestas], []);

        await Servicio().OrdenarHijosAsync(raiz.Id);

        Assert.Equal(
            [apuestas.Id, vial.Id],
            _carpetasReordenadas.Single(r => r.Padre == raiz.Id).Orden);
    }

    [Fact]
    public async Task Ordenar_una_carpeta_no_toca_el_contenido_de_sus_subcarpetas()
    {
        var raiz = new Folder(Guid.NewGuid(), "Trabajo");
        var interna = new Folder(Guid.NewGuid(), "Vial", raiz.Id);

        Arbol(
            [raiz, interna],
            [En(raiz.Id, "Zeta", 0), En(interna.Id, "Zeta", 0), En(interna.Id, "Alfa", 1)]);

        await Servicio().OrdenarHijosAsync(raiz.Id);

        Assert.DoesNotContain(_conexionesReordenadas, r => r.Padre == interna.Id);
        Assert.DoesNotContain(_carpetasReordenadas, r => r.Padre == interna.Id);
    }

    [Fact]
    public async Task Ordenar_no_arrastra_las_conexiones_que_cuelgan_de_otra()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        var madre = En(carpeta.Id, "Bravo", 0);
        var servicio = En(carpeta.Id, "Alfa", 1);
        servicio.ParentConnectionId = madre.Id;

        Arbol([carpeta], [madre, servicio]);

        await Servicio().OrdenarHijosAsync(carpeta.Id);

        Assert.Equal(
            [madre.Id],
            _conexionesReordenadas.Single(r => r.Padre == carpeta.Id).Orden);
    }

    [Fact]
    public async Task Ordenar_una_carpeta_con_un_solo_hijo_lo_deja_donde_esta()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        var sola = En(carpeta.Id, "Alfa", 0);

        Arbol([carpeta], [sola]);

        await Servicio().OrdenarHijosAsync(carpeta.Id);

        Assert.Equal([sola.Id], _conexionesReordenadas.Single(r => r.Padre == carpeta.Id).Orden);
    }

    [Fact]
    public async Task Una_carpeta_nueva_nace_en_su_lugar_alfabetico_y_no_en_cero()
    {
        var alfa = new Folder(Guid.NewGuid(), "Alfa", null, 0);
        var charlie = new Folder(Guid.NewGuid(), "Charlie", null, 1);

        Arbol([alfa, charlie], []);

        var creada = await Servicio().CreateAsync("Bravo", null);

        Assert.True(creada.Success);
        Assert.Equal(1, creada.Value!.SortOrder);
        Assert.Equal(
            [alfa.Id, creada.Value.Id, charlie.Id],
            _carpetasReordenadas.Single().Orden);
    }
}
