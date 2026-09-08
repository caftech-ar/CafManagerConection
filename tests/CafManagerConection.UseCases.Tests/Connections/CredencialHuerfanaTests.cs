using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute.ExceptionExtensions;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Connections;

/// <summary>
/// Si falla la persistencia en SQLite después de escribir la contraseña, esa contraseña no debe
/// quedar guardada sin que nada la referencie, ni destruir la que ya existía para la misma
/// conexión.
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
    public async Task Si_falla_el_alta_la_contrasena_ni_se_intenta_escribir()
    {
        // El secreto es una columna de la conexión, así que se escribe después del alta: si el
        // alta falla, no hay fila donde escribirlo y no queda nada que limpiar.
        var registro = NuevaConexion();
        _conexiones.AddAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().CreateAsync(registro, Credencial()));

        await _credenciales.DidNotReceiveWithAnyArgs().WriteAsync(default, default, default);
        await _credenciales.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Si_falla_el_guardado_de_la_contrasena_se_deshace_el_alta()
    {
        // Sin esto la conexión queda diciendo que tiene contraseña sin tenerla, y eso no se ve
        // hasta que falla la sesión.
        var registro = NuevaConexion();
        _credenciales
            .WriteAsync(
                Arg.Any<ReferenciaDeSecreto>(),
                Arg.Any<ReadOnlyMemory<char>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("el vault está cerrado"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().CreateAsync(registro, Credencial()));

        await _conexiones.Received(1).DeleteAsync(
            registro.Connection.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_el_alta_sale_bien_la_credencial_no_se_toca_dos_veces()
    {
        var registro = NuevaConexion();

        var resultado = await Servicio().CreateAsync(registro, Credencial());

        Assert.True(resultado.Success);
        await _credenciales.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Si_falla_la_edicion_y_la_credencial_es_nueva_se_borra()
    {
        var registro = NuevaConexion();
        _conexiones.GetByIdAsync(registro.Connection.Id, Arg.Any<CancellationToken>())
            .Returns(registro);

        var referencia = ReferenciaDeSecreto.DeConexion(registro.Connection.Id, Protocol.Ssh);
        _credenciales.ExistsAsync(referencia, Arg.Any<CancellationToken>()).Returns(false);

        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().UpdateAsync(registro, Credencial()));

        await _credenciales.Received(1).DeleteAsync(referencia, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_falla_la_edicion_y_la_contrasena_ya_existia_no_se_borra()
    {
        var registro = NuevaConexion();
        registro.Connection.TieneSecreto = true;

        var referencia = ReferenciaDeSecreto.DeConexion(registro.Connection.Id, Protocol.Ssh);
        _credenciales.ExistsAsync(referencia, Arg.Any<CancellationToken>()).Returns(true);

        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().UpdateAsync(registro, Credencial()));

        await _credenciales.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Si_falla_la_edicion_la_conexion_vuelve_a_declarar_que_no_tenia_contrasena()
    {
        var registro = NuevaConexion();

        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().UpdateAsync(registro, Credencial()));

        Assert.False(registro.Connection.TieneSecreto);
    }

    [Fact]
    public async Task Si_el_guardado_de_la_contrasena_se_cancela_el_alta_igual_se_deshace()
    {
        var registro = NuevaConexion();
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        _credenciales
            .WriteAsync(
                Arg.Any<ReferenciaDeSecreto>(),
                Arg.Any<ReadOnlyMemory<char>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cancelado.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Servicio().CreateAsync(registro, Credencial(), cancelado.Token));

        // Con CancellationToken.None: deshacer no se puede cancelar por el mismo motivo que
        // cancelo el guardado.
        await _conexiones.Received(1).DeleteAsync(
            registro.Connection.Id, CancellationToken.None);
    }

    [Fact]
    public async Task Si_la_edicion_se_cancela_la_credencial_nueva_igual_se_borra()
    {
        var registro = NuevaConexion();
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        var referencia = ReferenciaDeSecreto.DeConexion(registro.Connection.Id, Protocol.Ssh);
        _credenciales.ExistsAsync(referencia, Arg.Any<CancellationToken>()).Returns(false);

        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cancelado.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Servicio().UpdateAsync(registro, Credencial(), cancelado.Token));

        await _credenciales.Received(1).DeleteAsync(referencia, CancellationToken.None);
    }

    [Fact]
    public async Task Si_deshacer_el_alta_falla_sale_la_excepcion_del_guardado()
    {
        var registro = NuevaConexion();

        _credenciales
            .WriteAsync(
                Arg.Any<ReferenciaDeSecreto>(),
                Arg.Any<ReadOnlyMemory<char>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("el vault está cerrado"));

        _conexiones.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("la base rechazó el borrado"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().CreateAsync(registro, Credencial()));

        Assert.Equal("el vault está cerrado", ex.Message);
        Assert.Equal(
            registro.Connection.Id.ToString("D"),
            ex.Data[ConnectionService.DatoDeCredencialHuerfana]);
    }

    [Fact]
    public async Task El_dato_de_la_contrasena_huerfana_lleva_donde_estaba_y_no_el_secreto()
    {
        var registro = NuevaConexion();
        _conexiones.UpdateAsync(Arg.Any<ConnectionRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base no responde"));

        _credenciales.DeleteAsync(Arg.Any<ReferenciaDeSecreto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("el almacén rechazó el borrado"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().UpdateAsync(registro, Credencial()));

        var anotado = string.Join(
            '|', ex.Data.Values.Cast<object?>().Select(v => v?.ToString()));

        Assert.Contains("conexión", anotado, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-secreta", anotado, StringComparison.Ordinal);
    }

    // Exception.Data no lo lee nadie: ni la aplicación, ni Serilog, que imprime ToString().
    [Fact]
    public async Task El_alta_que_no_se_pudo_deshacer_llega_al_registro()
    {
        var registro = NuevaConexion();

        _credenciales
            .WriteAsync(
                Arg.Any<ReferenciaDeSecreto>(),
                Arg.Any<ReadOnlyMemory<char>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("el vault está cerrado"));

        _conexiones.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("la base rechazó el borrado"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().CreateAsync(registro, Credencial()));

        _bitacora.Received(1).TechnicalError(
            Arg.Is<string>(t =>
                t.Contains(registro.Connection.Id.ToString("D"), StringComparison.Ordinal)
                && !t.Contains("clave-secreta", StringComparison.Ordinal)),
            Arg.Any<UnauthorizedAccessException>());
    }
}
