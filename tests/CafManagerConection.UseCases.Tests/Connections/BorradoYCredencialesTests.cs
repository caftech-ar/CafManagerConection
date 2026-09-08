using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Connections;

public sealed class BorradoYCredencialesTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    private readonly Connection _conexion =
        new(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.207");

    public BorradoYCredencialesTests()
    {
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder>());

        _conexiones.GetByIdAsync(_conexion.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(_conexion, Ssh: new SshSettings()));
    }

    private ConnectionService Servicio() => new(_conexiones, _carpetas, _credenciales);

    private ReferenciaDeSecreto Suya => ReferenciaDeSecreto.DeConexion(_conexion.Id, Protocol.Ssh);

    [Fact]
    public async Task Borrar_una_conexion_se_lleva_su_contrasena_con_la_fila()
    {
        // El secreto es una columna de la conexión: la cascada de la base lo borra, y por eso el
        // borrado ya no puede fallar a medias ni dejar nada huérfano.
        _conexion.TieneSecreto = true;

        var r = await Servicio().DeleteAsync(_conexion.Id);

        Assert.True(r.Success);
        await _conexiones.Received(1).DeleteAsync(_conexion.Id, Arg.Any<CancellationToken>());
        await _credenciales.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Una_conexion_sin_contrasena_se_borra_igual()
    {
        var r = await Servicio().DeleteAsync(_conexion.Id);

        Assert.True(r.Success);
        await _conexiones.Received(1).DeleteAsync(_conexion.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Borrar_una_conexion_que_ya_no_existe_falla_sin_romper()
    {
        var r = await Servicio().DeleteAsync(Guid.NewGuid());

        Assert.False(r.Success);
    }

    [Fact]
    public async Task Quitar_la_contrasena_la_borra_de_la_fila_de_la_conexion()
    {
        // Rotar una contraseña es quitarla y volver a cargarla.
        _conexion.TieneSecreto = true;

        var r = await Servicio().ClearCredentialAsync(_conexion.Id);

        Assert.True(r.Success);
        await _credenciales.Received(1).DeleteAsync(Suya, Arg.Any<CancellationToken>());
        Assert.False(_conexion.TieneSecreto);
    }

    [Fact]
    public async Task Quitar_la_contrasena_de_una_conexion_que_no_la_tiene_no_es_un_error()
    {
        var r = await Servicio().ClearCredentialAsync(_conexion.Id);

        Assert.True(r.Success);
        Assert.False(_conexion.TieneSecreto);
    }

    [Fact]
    public async Task Sin_contrasena_guardada_no_hay_credencial()
    {
        Assert.False(await Servicio().HasStoredCredentialAsync(_conexion.Id));
    }

    [Fact]
    public async Task Con_la_marca_puesta_pero_sin_secreto_detras_tampoco_hay_credencial()
    {
        // Caso de una contraseña borrada por fuera de la aplicación.
        _conexion.TieneSecreto = true;
        _credenciales.ExistsAsync(Suya, Arg.Any<CancellationToken>()).Returns(false);

        Assert.False(await Servicio().HasStoredCredentialAsync(_conexion.Id));
    }

    [Fact]
    public async Task Con_la_marca_y_el_secreto_si_hay_credencial()
    {
        _conexion.TieneSecreto = true;
        _credenciales.ExistsAsync(Suya, Arg.Any<CancellationToken>()).Returns(true);

        Assert.True(await Servicio().HasStoredCredentialAsync(_conexion.Id));
    }
}
