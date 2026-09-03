using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Connections;

// FR-193a: antes toda conexión nueva o importada nacía con SortOrder 0 y saltaba al principio del árbol.
public sealed class OrdenDeAltaTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    private readonly List<List<Guid>> _reordenadas = [];

    public OrdenDeAltaTests()
    {
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder>());

        _conexiones
            .When(r => r.ReorderAsync(
                Arg.Any<Guid?>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()))
            .Do(ci => _reordenadas.Add([.. ci.ArgAt<IReadOnlyList<Guid>>(1)]));
    }

    private ConnectionService Servicio() => new(_conexiones, _carpetas, _credenciales);

    private void Existentes(params Connection[] conexiones) =>
        _conexiones.GetAllAsync(Arg.Any<CancellationToken>()).Returns(conexiones.ToList());

    private static Connection Conexion(string nombre, int orden = 0, Guid? carpeta = null) =>
        new(Guid.NewGuid(), nombre, Protocol.Ssh, "192.0.2.1")
        {
            FolderId = carpeta,
            SortOrder = orden,
        };

    [Fact]
    public async Task Una_conexion_nueva_nace_en_su_lugar_alfabetico_y_no_en_cero()
    {
        var alfa = Conexion("Alfa", 0);
        var charlie = Conexion("Charlie", 1);
        Existentes(alfa, charlie);

        var nueva = Conexion("Bravo");
        var r = await Servicio().CreateAsync(new ConnectionRecord(nueva, Ssh: new SshSettings()));

        Assert.True(r.Success);
        Assert.Equal(1, nueva.SortOrder);
        Assert.Equal([alfa.Id, nueva.Id, charlie.Id], _reordenadas.Single());
    }

    [Fact]
    public async Task Una_conexion_nueva_no_se_mete_entre_las_de_otra_carpeta()
    {
        var carpeta = Guid.NewGuid();
        var ajena = Conexion("Alfa", 0);
        var vecina = Conexion("Charlie", 0, carpeta);
        Existentes(ajena, vecina);

        var nueva = Conexion("Bravo", 0, carpeta);
        var r = await Servicio().CreateAsync(new ConnectionRecord(nueva, Ssh: new SshSettings()));

        Assert.True(r.Success);
        Assert.Equal(0, nueva.SortOrder);
        Assert.Equal([nueva.Id, vecina.Id], _reordenadas.Single());
    }

    [Fact]
    public async Task Una_conexion_nueva_al_final_del_alfabeto_va_al_final()
    {
        var alfa = Conexion("Alfa", 0);
        var bravo = Conexion("Bravo", 1);
        Existentes(alfa, bravo);

        var nueva = Conexion("Zeta");
        var r = await Servicio().CreateAsync(new ConnectionRecord(nueva, Ssh: new SshSettings()));

        Assert.True(r.Success);
        Assert.Equal(2, nueva.SortOrder);
        Assert.Equal([alfa.Id, bravo.Id, nueva.Id], _reordenadas.Single());
    }

    [Fact]
    public async Task Duplicar_sigue_poniendo_la_copia_junto_al_original()
    {
        var original = Conexion("Servidor PRD", 4);
        Existentes(original);

        _conexiones.GetByIdAsync(original.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(original, Ssh: new SshSettings()));

        ConnectionRecord? guardado = null;
        await _conexiones.AddAsync(
            Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        var r = await Servicio().DuplicateAsync(original.Id);

        Assert.True(r.Success);
        Assert.Equal(5, guardado!.Connection.SortOrder);
        Assert.Empty(_reordenadas);
    }

    [Fact]
    public async Task Mover_una_conexion_le_da_su_lugar_en_la_carpeta_de_destino()
    {
        var destino = Guid.NewGuid();
        var alfa = Conexion("Alfa", 0, destino);
        var charlie = Conexion("Charlie", 1, destino);
        var venida = Conexion("Bravo", 7);

        Existentes(alfa, charlie, venida);
        _conexiones.GetByIdAsync(venida.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(venida, Ssh: new SshSettings()));

        var r = await Servicio().MoveAsync(venida.Id, destino);

        Assert.True(r.Success);
        Assert.Equal(1, venida.SortOrder);
        Assert.Equal([alfa.Id, venida.Id, charlie.Id], _reordenadas.Single());
    }

    [Fact]
    public async Task Mover_una_conexion_a_la_posicion_pedida_la_deja_ahi()
    {
        var destino = Guid.NewGuid();
        var alfa = Conexion("Alfa", 0, destino);
        var charlie = Conexion("Charlie", 1, destino);
        var venida = Conexion("Zeta", 7);

        Existentes(alfa, charlie, venida);
        _conexiones.GetByIdAsync(venida.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(venida, Ssh: new SshSettings()));

        var r = await Servicio().MoveAsync(venida.Id, destino, posicion: 0);

        Assert.True(r.Success);
        Assert.Equal(0, venida.SortOrder);
        Assert.Equal([venida.Id, alfa.Id, charlie.Id], _reordenadas.Single());
    }
}
