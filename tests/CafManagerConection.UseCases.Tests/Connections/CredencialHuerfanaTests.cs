using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CafManagerConection.UseCases.Tests.Connections;

/// <summary>
/// Si falla la persistencia en SQLite después de escribir la credencial en el Administrador de
/// credenciales de Windows, esa credencial no debe quedar huérfana (ni destruir una que ya
/// existía para la misma conexión).
/// </summary>
public sealed class CredencialHuerfanaTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    public CredencialHuerfanaTests() =>
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder>());

    private readonly IAppLogger _bitacora = Substitute.For<IAppLogger>();

    private ConnectionService Servicio() =>
        new(_conexiones, _carpetas, _credenciales, null, _bitacora);

    private static ConnectionRecord NuevaConexion() =>
        new(new Connection(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.207"));

    private static CredentialPromptResult Credencial() =>
        new("admin", null, "clave-secreta", Remember: true);

    [Fact]
    public async Task Si_falla_el_alta_se_borra_la_credencial_recien_escrita()
    {
        var registro = NuevaConexion();
        _conexiones.AddAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        var clave = CredentialKey.ForConnection(registro.Connection.Id, Protocol.Ssh).Value;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().CreateAsync(registro, Credencial()));

        await _credenciales.Received(1).DeleteAsync(clave, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_el_alta_sale_bien_la_credencial_no_se_toca_dos_veces()
    {
        var registro = NuevaConexion();

        var resultado = await Servicio().CreateAsync(registro, Credencial());

        Assert.True(resultado.Success);
        await _credenciales.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task Si_falla_la_edicion_y_la_credencial_es_nueva_se_borra()
    {
        var registro = NuevaConexion();
        _conexiones.GetByIdAsync(registro.Connection.Id, Arg.Any<CancellationToken>())
            .Returns(registro);

        var clave = CredentialKey.ForConnection(registro.Connection.Id, Protocol.Ssh).Value;
        _credenciales.ExistsAsync(clave, Arg.Any<CancellationToken>()).Returns(false);

        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().UpdateAsync(registro, Credencial()));

        await _credenciales.Received(1).DeleteAsync(clave, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_falla_la_edicion_y_la_credencial_ya_existia_no_se_borra()
    {
        var registro = NuevaConexion();
        registro.Connection.CredentialKey =
            CredentialKey.ForConnection(registro.Connection.Id, Protocol.Ssh).Value;

        var clave = registro.Connection.CredentialKey;
        _credenciales.ExistsAsync(clave, Arg.Any<CancellationToken>()).Returns(true);

        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().UpdateAsync(registro, Credencial()));

        await _credenciales.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task Si_falla_la_edicion_la_conexion_vuelve_a_apuntar_a_la_clave_anterior()
    {
        var registro = NuevaConexion();
        registro.Connection.CredentialKey = "cmc:ssh:otra-conexion-vieja";

        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().UpdateAsync(registro, Credencial()));

        Assert.Equal("cmc:ssh:otra-conexion-vieja", registro.Connection.CredentialKey);
    }

    [Fact]
    public async Task Si_el_alta_se_cancela_la_credencial_igual_se_borra()
    {
        var registro = NuevaConexion();
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        _conexiones.AddAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cancelado.Token));

        var clave = CredentialKey.ForConnection(registro.Connection.Id, Protocol.Ssh).Value;

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Servicio().CreateAsync(registro, Credencial(), cancelado.Token));

        await _credenciales.Received(1).DeleteAsync(clave, CancellationToken.None);
    }

    [Fact]
    public async Task Si_la_edicion_se_cancela_la_credencial_nueva_igual_se_borra()
    {
        var registro = NuevaConexion();
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        var clave = CredentialKey.ForConnection(registro.Connection.Id, Protocol.Ssh).Value;
        _credenciales.ExistsAsync(clave, Arg.Any<CancellationToken>()).Returns(false);

        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cancelado.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Servicio().UpdateAsync(registro, Credencial(), cancelado.Token));

        await _credenciales.Received(1).DeleteAsync(clave, CancellationToken.None);
    }

    [Fact]
    public async Task Si_el_borrado_compensatorio_falla_sale_la_excepcion_de_la_persistencia()
    {
        var registro = NuevaConexion();
        _conexiones.AddAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        _credenciales.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("el almacén rechazó el borrado"));

        var clave = CredentialKey.ForConnection(registro.Connection.Id, Protocol.Ssh).Value;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().CreateAsync(registro, Credencial()));

        Assert.Equal("la base no responde", ex.Message);
        Assert.Equal(clave, ex.Data[ConnectionService.DatoDeCredencialHuerfana]);
    }

    [Fact]
    public async Task El_dato_de_la_credencial_huerfana_lleva_la_clave_y_no_el_secreto()
    {
        var registro = NuevaConexion();
        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        _credenciales.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("el almacén rechazó el borrado"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().UpdateAsync(registro, Credencial()));

        var anotado = string.Join(
            '|', ex.Data.Values.Cast<object?>().Select(v => v?.ToString()));

        Assert.Contains("cmc:", anotado, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-secreta", anotado, StringComparison.Ordinal);
    }

    // Exception.Data no lo lee nadie: ni la aplicación, ni Serilog, que imprime ToString().
    [Fact]
    public async Task La_credencial_huerfana_que_no_se_pudo_borrar_llega_al_registro()
    {
        var registro = NuevaConexion();
        _conexiones.AddAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        _credenciales.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("el almacén rechazó el borrado"));

        var clave = CredentialKey.ForConnection(registro.Connection.Id, Protocol.Ssh).Value;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().CreateAsync(registro, Credencial()));

        _bitacora.Received(1).TechnicalError(
            Arg.Is<string>(t => t.Contains(clave, StringComparison.Ordinal)
                                && !t.Contains("clave-secreta", StringComparison.Ordinal)),
            Arg.Any<UnauthorizedAccessException>());
    }
}
