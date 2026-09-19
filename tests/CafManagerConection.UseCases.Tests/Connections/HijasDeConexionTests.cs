using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Connections;

/// <summary>Una conexión hija hereda por la carpeta de su padre, así que las dos tienen que viajar juntas.</summary>
public sealed class HijasDeConexionTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    private readonly Guid _produccion = Guid.NewGuid();
    private readonly Guid _pruebas = Guid.NewGuid();

    public HijasDeConexionTests() =>
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder>
        {
            new(Guid.NewGuid(), "Producción"),
            new(Guid.NewGuid(), "Pruebas"),
        });

    [Fact]
    public async Task Mover_un_padre_de_carpeta_arrastra_a_sus_hijas()
    {
        var padre = Conexion("Padre", _produccion);
        var hija = Conexion("Hija", _produccion, padre.Id);

        Registrar(padre, hija);

        var r = await Servicio().MoveAsync(padre.Id, _pruebas);

        Assert.True(r.Success);
        Assert.Equal(_pruebas, padre.FolderId);
        Assert.Equal(_pruebas, hija.FolderId);
    }

    [Fact]
    public async Task Mover_una_conexion_sin_hijas_no_toca_a_nadie_mas()
    {
        var sola = Conexion("Sola", _produccion);
        var ajena = Conexion("Ajena", _produccion);

        Registrar(sola, ajena);

        await Servicio().MoveAsync(sola.Id, _pruebas);

        Assert.Equal(_produccion, ajena.FolderId);
    }

    [Fact]
    public async Task Elegir_un_padre_pone_la_conexion_en_la_carpeta_del_padre()
    {
        var padre = Conexion("Padre", _produccion);
        var suelta = Conexion("Suelta", _pruebas);

        Registrar(padre, suelta);

        var r = await Servicio().SetParentAsync(suelta.Id, padre.Id);

        Assert.True(r.Success);
        Assert.Equal(_produccion, suelta.FolderId);
        Assert.Equal(padre.Id, suelta.ParentConnectionId);
    }

    private ConnectionService Servicio() => new(_conexiones, _carpetas, _credenciales);

    private static Connection Conexion(string nombre, Guid carpeta, Guid? padre = null) =>
        new(Guid.NewGuid(), nombre, Protocol.Ssh, "192.0.2.1")
        {
            FolderId = carpeta,
            ParentConnectionId = padre,
        };

    private void Registrar(params Connection[] conexiones)
    {
        _conexiones.GetAllAsync(Arg.Any<CancellationToken>()).Returns([.. conexiones]);

        foreach (var c in conexiones)
        {
            _conexiones.GetByIdAsync(c.Id, Arg.Any<CancellationToken>())
                .Returns(new ConnectionRecord(c));
        }
    }
}
