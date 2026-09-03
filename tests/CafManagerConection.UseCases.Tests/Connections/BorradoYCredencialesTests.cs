using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CafManagerConection.UseCases.Tests.Connections;

// FR-037, FR-038, FR-039.
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

    [Fact]
    public async Task Borrar_una_conexion_borra_tambien_su_credencial()
    {
        _conexion.CredentialKey = "cmc:ssh:x";

        var r = await Servicio().DeleteAsync(_conexion.Id);

        Assert.True(r.Success);
        await _credenciales.Received(1).DeleteAsync("cmc:ssh:x", Arg.Any<CancellationToken>());
        await _conexiones.Received(1).DeleteAsync(_conexion.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_falla_el_borrado_de_la_credencial_la_conexion_no_se_elimina()
    {
        _conexion.CredentialKey = "cmc:ssh:x";

        _credenciales.DeleteAsync("cmc:ssh:x", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("el almacén no responde"));

        var r = await Servicio().DeleteAsync(_conexion.Id);

        Assert.False(r.Success);
        await _conexiones.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task El_mensaje_de_error_explica_por_que_no_se_borro()
    {
        _conexion.CredentialKey = "cmc:ssh:x";

        _credenciales.DeleteAsync("cmc:ssh:x", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("el almacén no responde"));

        var r = await Servicio().DeleteAsync(_conexion.Id);

        Assert.Contains("credencial", r.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("el almacén no responde", r.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_conexion_sin_credencial_se_borra_sin_tocar_el_almacen()
    {
        var r = await Servicio().DeleteAsync(_conexion.Id);

        Assert.True(r.Success);
        await _credenciales.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task Borrar_una_conexion_que_ya_no_existe_falla_sin_romper()
    {
        var r = await Servicio().DeleteAsync(Guid.NewGuid());

        Assert.False(r.Success);
    }

    [Fact]
    public async Task Quitar_la_credencial_la_borra_del_almacen_y_de_la_conexion()
    {
        // FR-037: rotar una contraseña es quitarla y volver a cargarla.
        _conexion.CredentialKey = "cmc:ssh:x";

        var r = await Servicio().ClearCredentialAsync(_conexion.Id);

        Assert.True(r.Success);
        await _credenciales.Received(1).DeleteAsync("cmc:ssh:x", Arg.Any<CancellationToken>());
        Assert.Null(_conexion.CredentialKey);
    }

    [Fact]
    public async Task Quitar_la_credencial_de_una_conexion_que_no_la_tiene_no_es_un_error()
    {
        var r = await Servicio().ClearCredentialAsync(_conexion.Id);

        Assert.True(r.Success);
        Assert.Null(_conexion.CredentialKey);
    }

    [Fact]
    public async Task Sin_clave_guardada_no_hay_credencial()
    {
        Assert.False(await Servicio().HasStoredCredentialAsync(_conexion.Id));
    }

    [Fact]
    public async Task Con_clave_pero_sin_secreto_detras_tampoco_hay_credencial()
    {
        // Caso de una credencial borrada por fuera de la aplicación.
        _conexion.CredentialKey = "cmc:ssh:x";
        _credenciales.ExistsAsync("cmc:ssh:x", Arg.Any<CancellationToken>()).Returns(false);

        Assert.False(await Servicio().HasStoredCredentialAsync(_conexion.Id));
    }

    [Fact]
    public async Task Con_clave_y_secreto_si_hay_credencial()
    {
        _conexion.CredentialKey = "cmc:ssh:x";
        _credenciales.ExistsAsync("cmc:ssh:x", Arg.Any<CancellationToken>()).Returns(true);

        Assert.True(await Servicio().HasStoredCredentialAsync(_conexion.Id));
    }
}
