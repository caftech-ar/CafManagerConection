using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Connections;

// FR-125, FR-127.
public sealed class ColgarConexionTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    private ConnectionService Servicio() => new(_conexiones, _carpetas, _credenciales);

    private static Connection Conexion(string nombre) =>
        new(Guid.NewGuid(), nombre, Protocol.Ssh, "192.0.2.1");

    private void Registrar(params Connection[] conexiones)
    {
        foreach (var c in conexiones)
        {
            _conexiones.GetByIdAsync(c.Id, Arg.Any<CancellationToken>())
                .Returns(new ConnectionRecord(c));
        }

        _conexiones.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(conexiones.ToList());
    }

    [Fact]
    public async Task Colgar_de_una_conexion_suelta_funciona()
    {
        var servidor = Conexion("Aplicaciones");
        var servicio = Conexion("Portainer");
        Registrar(servidor, servicio);

        var r = await Servicio().SetParentAsync(servicio.Id, servidor.Id);

        Assert.True(r.Success);
        Assert.Equal(servidor.Id, servicio.ParentConnectionId);
    }

    [Fact]
    public async Task La_hija_se_muda_a_la_carpeta_del_padre()
    {
        var carpeta = Guid.NewGuid();
        var servidor = Conexion("Aplicaciones");
        servidor.FolderId = carpeta;

        var servicio = Conexion("Portainer");
        servicio.FolderId = Guid.NewGuid();

        Registrar(servidor, servicio);

        await Servicio().SetParentAsync(servicio.Id, servidor.Id);

        Assert.Equal(carpeta, servicio.FolderId);
    }

    [Fact]
    public async Task No_se_puede_colgar_de_una_que_ya_cuelga()
    {
        var abuelo = Conexion("Servidor");
        var padre = Conexion("Portainer");
        padre.ParentConnectionId = abuelo.Id;
        var nieto = Conexion("Otro");

        Registrar(abuelo, padre, nieto);

        var r = await Servicio().SetParentAsync(nieto.Id, padre.Id);

        Assert.False(r.Success);
        Assert.Null(nieto.ParentConnectionId);
    }

    [Fact]
    public async Task Una_conexion_con_servicios_no_puede_pasar_a_colgar_de_otra()
    {
        var otro = Conexion("Otro servidor");
        var servidor = Conexion("Aplicaciones");
        var servicio = Conexion("Portainer");
        servicio.ParentConnectionId = servidor.Id;

        Registrar(otro, servidor, servicio);

        var r = await Servicio().SetParentAsync(servidor.Id, otro.Id);

        Assert.False(r.Success);
        Assert.Null(servidor.ParentConnectionId);
    }

    [Fact]
    public async Task Quitar_el_padre_deja_la_conexion_suelta()
    {
        var servidor = Conexion("Aplicaciones");
        var servicio = Conexion("Portainer");
        servicio.ParentConnectionId = servidor.Id;

        Registrar(servidor, servicio);

        var r = await Servicio().SetParentAsync(servicio.Id, null);

        Assert.True(r.Success);
        Assert.Null(servicio.ParentConnectionId);
    }

    [Fact]
    public async Task Mover_a_una_carpeta_la_descuelga_de_su_padre()
    {
        var servidor = Conexion("Aplicaciones");
        var servicio = Conexion("Portainer");
        servicio.ParentConnectionId = servidor.Id;

        Registrar(servidor, servicio);
        var destino = Guid.NewGuid();

        await Servicio().MoveAsync(servicio.Id, destino);

        Assert.Null(servicio.ParentConnectionId);
        Assert.Equal(destino, servicio.FolderId);
    }

    [Fact]
    public async Task Colgar_de_una_conexion_que_ya_no_existe_falla_sin_romper()
    {
        var servicio = Conexion("Portainer");
        Registrar(servicio);

        var r = await Servicio().SetParentAsync(servicio.Id, Guid.NewGuid());

        Assert.False(r.Success);
        Assert.Null(servicio.ParentConnectionId);
    }
}
