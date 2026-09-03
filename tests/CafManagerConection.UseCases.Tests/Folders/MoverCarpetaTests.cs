using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Folders;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Folders;

// FR-193 y FR-193b: la carpeta se mueve, cae donde se la soltó, y el ciclo se rechaza antes de tocar la base.
public sealed class MoverCarpetaTests
{
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    private readonly List<(Guid? Padre, List<Guid> Orden)> _reordenadas = [];

    public MoverCarpetaTests() =>
        _carpetas
            .When(r => r.ReorderAsync(
                Arg.Any<Guid?>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()))
            .Do(ci => _reordenadas.Add(
                (ci.ArgAt<Guid?>(0), [.. ci.ArgAt<IReadOnlyList<Guid>>(1)])));

    private FolderService Servicio() => new(_carpetas, _conexiones, _credenciales);

    private void Arbol(params Folder[] carpetas) =>
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(carpetas.ToList());

    [Fact]
    public async Task Una_carpeta_no_se_puede_mover_dentro_de_si_misma()
    {
        var sola = new Folder(Guid.NewGuid(), "Trabajo");
        Arbol(sola);

        var r = await Servicio().MoveAsync(sola.Id, sola.Id);

        Assert.False(r.Success);
        await _carpetas.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Una_carpeta_no_se_puede_mover_dentro_de_su_hija()
    {
        var madre = new Folder(Guid.NewGuid(), "Trabajo");
        var hija = new Folder(Guid.NewGuid(), "Vial", madre.Id);
        Arbol(madre, hija);

        var r = await Servicio().MoveAsync(madre.Id, hija.Id);

        Assert.False(r.Success);
        Assert.Contains("subcarpetas", r.ErrorMessage);
        await _carpetas.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Una_carpeta_no_se_puede_mover_dentro_de_su_nieta()
    {
        var abuela = new Folder(Guid.NewGuid(), "Trabajo");
        var madre = new Folder(Guid.NewGuid(), "Vial", abuela.Id);
        var nieta = new Folder(Guid.NewGuid(), "Producción", madre.Id);
        Arbol(abuela, madre, nieta);

        var r = await Servicio().MoveAsync(abuela.Id, nieta.Id);

        Assert.False(r.Success);
        await _carpetas.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Mover_una_carpeta_a_la_posicion_pedida_la_deja_ahi()
    {
        var alfa = new Folder(Guid.NewGuid(), "Alfa", null, 0);
        var bravo = new Folder(Guid.NewGuid(), "Bravo", null, 1);
        var venida = new Folder(Guid.NewGuid(), "Zeta", Guid.NewGuid(), 7);
        Arbol(alfa, bravo, venida);

        var r = await Servicio().MoveAsync(venida.Id, null, posicion: 1);

        Assert.True(r.Success);
        Assert.Equal(1, venida.SortOrder);
        Assert.Null(venida.ParentId);
        Assert.Equal([alfa.Id, venida.Id, bravo.Id], _reordenadas.Single().Orden);
    }

    [Fact]
    public async Task Mover_una_carpeta_sin_posicion_la_deja_en_su_lugar_alfabetico()
    {
        var destino = new Folder(Guid.NewGuid(), "Trabajo");
        var alfa = new Folder(Guid.NewGuid(), "Alfa", destino.Id, 0);
        var charlie = new Folder(Guid.NewGuid(), "Charlie", destino.Id, 1);
        var bravo = new Folder(Guid.NewGuid(), "Bravo", null, 9);
        Arbol(destino, alfa, charlie, bravo);

        var r = await Servicio().MoveAsync(bravo.Id, destino.Id);

        Assert.True(r.Success);
        Assert.Equal(1, bravo.SortOrder);
        Assert.Equal([alfa.Id, bravo.Id, charlie.Id], _reordenadas.Single().Orden);
    }

    [Fact]
    public async Task Una_posicion_mas_alla_del_final_deja_la_carpeta_al_final()
    {
        var alfa = new Folder(Guid.NewGuid(), "Alfa", null, 0);
        var venida = new Folder(Guid.NewGuid(), "Zeta", Guid.NewGuid(), 3);
        Arbol(alfa, venida);

        var r = await Servicio().MoveAsync(venida.Id, null, posicion: 99);

        Assert.True(r.Success);
        Assert.Equal([alfa.Id, venida.Id], _reordenadas.Single().Orden);
    }
}
