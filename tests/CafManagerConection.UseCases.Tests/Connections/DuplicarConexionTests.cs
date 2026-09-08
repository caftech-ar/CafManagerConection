using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Connections;

public sealed class DuplicarConexionTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    public DuplicarConexionTests()
    {
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder>());
    }

    private ConnectionService Servicio() => new(_conexiones, _carpetas, _credenciales);

    private void Registrar(ConnectionRecord registro) =>
        _conexiones.GetByIdAsync(registro.Connection.Id, Arg.Any<CancellationToken>())
            .Returns(registro);

    [Fact]
    public async Task Duplicar_le_escribe_a_la_copia_su_propia_contrasena()
    {
        var original = new Connection(Guid.NewGuid(), "Servidor PRD", Protocol.Ssh, "192.0.2.5")
        {
            TieneSecreto = true,
        };
        Registrar(new ConnectionRecord(original, Ssh: new SshSettings()));

        _credenciales
            .ReadAsync(
                ReferenciaDeSecreto.DeConexion(original.Id, Protocol.Ssh),
                Arg.Any<CancellationToken>())
            .Returns("hunter2".ToCharArray());

        ConnectionRecord? guardado = null;
        await _conexiones.AddAsync(Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        var r = await Servicio().DuplicateAsync(original.Id);

        Assert.True(r.Success);
        Assert.NotNull(guardado);
        Assert.True(guardado!.Connection.TieneSecreto);
        Assert.NotEqual(original.Id, guardado.Connection.Id);

        await _credenciales.Received(1).WriteAsync(
            ReferenciaDeSecreto.DeConexion(guardado.Connection.Id, Protocol.Ssh),
            Arg.Any<ReadOnlyMemory<char>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicar_copia_el_secreto_a_la_fila_de_la_copia()
    {
        var original = new Connection(Guid.NewGuid(), "Servidor PRD", Protocol.Ssh, "192.0.2.5")
        {
            TieneSecreto = true,
        };
        Registrar(new ConnectionRecord(original, Ssh: new SshSettings()));

        _credenciales
            .ReadAsync(
                ReferenciaDeSecreto.DeConexion(original.Id, Protocol.Ssh),
                Arg.Any<CancellationToken>())
            .Returns("hunter2".ToCharArray());

        ConnectionRecord? guardado = null;
        await _conexiones.AddAsync(Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        string? escrito = null;
        ReferenciaDeSecreto donde = default;
        await _credenciales.WriteAsync(
            Arg.Do<ReferenciaDeSecreto>(d => donde = d),
            Arg.Do<ReadOnlyMemory<char>>(m => escrito = new string(m.ToArray())),
            Arg.Any<CancellationToken>());

        var r = await Servicio().DuplicateAsync(original.Id);

        Assert.True(r.Success);
        Assert.True(guardado!.Connection.TieneSecreto);

        // Se captura en el momento de la llamada: el servicio pisa el arreglo apenas termina de
        // escribirlo, asi que un matcher evaluado despues lo ve en ceros.
        Assert.Equal("hunter2", escrito);
        Assert.Equal(
            ReferenciaDeSecreto.DeConexion(guardado.Connection.Id, Protocol.Ssh), donde);
    }

    [Fact]
    public async Task Duplicar_una_conexion_sin_contrasena_no_toca_el_almacen()
    {
        var original = new Connection(Guid.NewGuid(), "Servidor sin clave", Protocol.Ssh, "192.0.2.6");
        Registrar(new ConnectionRecord(original, Ssh: new SshSettings()));

        var r = await Servicio().DuplicateAsync(original.Id);

        Assert.True(r.Success);
        await _credenciales.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
        await _credenciales.DidNotReceiveWithAnyArgs().WriteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Duplicar_copia_los_campos_que_el_editor_muestra()
    {
        var tagId = Guid.NewGuid();
        var original = new Connection(Guid.NewGuid(), "Servidor PRD", Protocol.Ssh, "192.0.2.5")
        {
            Description = "Base de datos de producción",
            ClaveDeColor = "rojo",
            IsFavorite = true,
            TagId = tagId,
            DocumentationUrl = "https://wiki.interna/prd",
        };
        original.SetCustomField("Ticket", "OPS-123");
        Registrar(new ConnectionRecord(original, Ssh: new SshSettings()));

        ConnectionRecord? guardado = null;
        await _conexiones.AddAsync(Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        var r = await Servicio().DuplicateAsync(original.Id);

        Assert.True(r.Success);
        var copia = guardado!.Connection;
        Assert.Equal("Base de datos de producción", copia.Description);
        Assert.Equal("rojo", copia.ClaveDeColor);
        Assert.True(copia.IsFavorite);
        Assert.Equal(tagId, copia.TagId);
        Assert.Equal("https://wiki.interna/prd", copia.DocumentationUrl);
        Assert.Equal("OPS-123", copia.CustomFields["Ticket"]);
    }

    [Fact]
    public async Task Borrar_una_conexion_no_toca_la_contrasena_de_ninguna_otra()
    {
        // La contraseña es una columna de la conexión: no hay forma de que dos la compartan, y por
        // eso el borrado ya no tiene que averiguar si alguien más la usa.
        var otra = new Connection(Guid.NewGuid(), "Servidor PRD", Protocol.Ssh, "192.0.2.1")
        {
            TieneSecreto = true,
        };

        var duplicada = new Connection(
            Guid.NewGuid(), "Servidor PRD (copia)", Protocol.Ssh, "192.0.2.1")
        {
            TieneSecreto = true,
        };

        Registrar(new ConnectionRecord(duplicada));

        _conexiones.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Connection> { otra, duplicada });

        var r = await Servicio().DeleteAsync(duplicada.Id);

        Assert.True(r.Success);
        await _conexiones.Received(1).DeleteAsync(duplicada.Id, Arg.Any<CancellationToken>());
        await _credenciales.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }
}
