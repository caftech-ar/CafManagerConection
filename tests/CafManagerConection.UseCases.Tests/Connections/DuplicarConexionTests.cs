using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Connections;

// FR-053.
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
    public async Task Duplicar_no_deja_la_copia_apuntando_a_la_credencial_del_original()
    {
        var original = new Connection(Guid.NewGuid(), "Servidor PRD", Protocol.Ssh, "192.0.2.5")
        {
            CredentialKey = CredentialKey.ForConnection(Guid.NewGuid(), Protocol.Ssh).Value,
        };
        Registrar(new ConnectionRecord(original, Ssh: new SshSettings()));

        using var secreto = new StoredCredential("admin", null, "hunter2");
        _credenciales.ReadAsync(original.CredentialKey!, Arg.Any<CancellationToken>())
            .Returns(secreto);

        ConnectionRecord? guardado = null;
        await _conexiones.AddAsync(Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        var r = await Servicio().DuplicateAsync(original.Id);

        Assert.True(r.Success);
        Assert.NotNull(guardado);
        Assert.NotEqual(original.CredentialKey, guardado!.Connection.CredentialKey);
    }

    [Fact]
    public async Task Duplicar_copia_el_secreto_a_la_clave_nueva_de_la_copia()
    {
        var original = new Connection(Guid.NewGuid(), "Servidor PRD", Protocol.Ssh, "192.0.2.5")
        {
            CredentialKey = CredentialKey.ForConnection(Guid.NewGuid(), Protocol.Ssh).Value,
        };
        Registrar(new ConnectionRecord(original, Ssh: new SshSettings()));

        using var secreto = new StoredCredential("admin", "DOMINIO", "hunter2");
        _credenciales.ReadAsync(original.CredentialKey!, Arg.Any<CancellationToken>())
            .Returns(secreto);

        ConnectionRecord? guardado = null;
        await _conexiones.AddAsync(Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        var r = await Servicio().DuplicateAsync(original.Id);

        Assert.True(r.Success);
        Assert.NotNull(guardado!.Connection.CredentialKey);
        await _credenciales.Received(1).WriteAsync(
            guardado.Connection.CredentialKey!,
            Arg.Is<StoredCredential>(s => s.UserName == "admin" && s.Domain == "DOMINIO"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicar_una_conexion_sin_credencial_no_toca_el_almacen()
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
    public async Task Borrar_no_se_lleva_una_credencial_que_otra_conexion_todavia_usa()
    {
        var compartida = "cmc:ssh:11111111-1111-4111-8111-111111111111";

        var vieja = new Connection(Guid.NewGuid(), "Servidor PRD", Protocol.Ssh, "192.0.2.1")
        {
            CredentialKey = compartida,
        };

        var duplicadaAntes = new Connection(
            Guid.NewGuid(), "Servidor PRD (copia)", Protocol.Ssh, "192.0.2.1")
        {
            CredentialKey = compartida,
        };

        Registrar(new ConnectionRecord(duplicadaAntes));

        _conexiones.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Connection> { vieja, duplicadaAntes });

        var r = await Servicio().DeleteAsync(duplicadaAntes.Id);

        Assert.True(r.Success);
        await _conexiones.Received(1).DeleteAsync(duplicadaAntes.Id, Arg.Any<CancellationToken>());
        await _credenciales.DidNotReceive().DeleteAsync(compartida, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Borrar_si_se_lleva_la_credencial_cuando_no_la_usa_nadie_mas()
    {
        var propia = "cmc:ssh:22222222-2222-4222-8222-222222222222";

        var unica = new Connection(Guid.NewGuid(), "Servidor único", Protocol.Ssh, "192.0.2.9")
        {
            CredentialKey = propia,
        };

        Registrar(new ConnectionRecord(unica));

        _conexiones.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Connection> { unica });

        var r = await Servicio().DeleteAsync(unica.Id);

        Assert.True(r.Success);
        await _credenciales.Received(1).DeleteAsync(propia, Arg.Any<CancellationToken>());
    }
}
