using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Connections;

// FR-149.
public sealed class ConexionRapidaTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    public ConexionRapidaTests() =>
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder>());

    private ConnectionService Servicio() => new(_conexiones, _carpetas, _credenciales);

    [Fact]
    public async Task Crear_una_conexion_rapida_la_persiste_para_poder_abrir_sesion()
    {
        ConnectionRecord? guardado = null;

        await _conexiones.AddAsync(
            Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        var r = await Servicio().CreateQuickAsync("root", "192.0.2.10", 22);

        Assert.True(r.Success);
        Assert.NotNull(guardado);
        Assert.Equal(r.Value, guardado!.Connection.Id);
        Assert.Equal("root", guardado.Connection.UserName);
        Assert.Equal("192.0.2.10", guardado.Connection.Host);
        Assert.Equal(Protocol.Ssh, guardado.Connection.Protocol);
    }

    [Fact]
    public async Task El_puerto_por_omision_no_se_fija_para_no_tapar_la_herencia()
    {
        ConnectionRecord? guardado = null;

        await _conexiones.AddAsync(
            Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        await Servicio().CreateQuickAsync("root", "192.0.2.10", 22);

        Assert.Null(guardado!.Connection.Port);
    }

    [Fact]
    public async Task Un_puerto_distinto_del_de_omision_si_se_fija()
    {
        ConnectionRecord? guardado = null;

        await _conexiones.AddAsync(
            Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        await Servicio().CreateQuickAsync("root", "192.0.2.10", 2222);

        Assert.Equal(2222, guardado!.Connection.Port);
    }

    [Fact]
    public async Task Una_conexion_rapida_no_aparece_en_el_arbol()
    {
        ConnectionRecord? guardado = null;

        await _conexiones.AddAsync(
            Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        var otraGuardada = new Connection(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.207");

        await Servicio().CreateQuickAsync("root", "192.0.2.10", 22);

        _conexiones.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Connection> { guardado!.Connection, otraGuardada });

        var arbol = await Servicio().GetTreeAsync();

        Assert.Single(arbol);
        Assert.Equal(otraGuardada.Id, arbol[0].Id);
    }

    [Fact]
    public async Task Una_conexion_rapida_tampoco_aparece_al_buscar()
    {
        ConnectionRecord? guardado = null;

        await _conexiones.AddAsync(
            Arg.Do<ConnectionRecord>(r => guardado = r), Arg.Any<CancellationToken>());

        await Servicio().CreateQuickAsync("root", "192.0.2.10", 22);

        _conexiones.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Connection> { guardado!.Connection });

        var resultado = await Servicio().SearchAsync("192.0.2.10");

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Crear_una_conexion_rapida_no_escribe_ninguna_credencial()
    {
        // FR-149.
        await Servicio().CreateQuickAsync("root", "192.0.2.10", 22);

        await _credenciales.DidNotReceiveWithAnyArgs().WriteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Guardar_de_verdad_una_conexion_rapida_la_hace_aparecer_en_el_arbol()
    {
        var rapida = new Connection(Guid.NewGuid(), "root@192.0.2.10", Protocol.Ssh, "192.0.2.10")
        {
            UserName = "root",
        };
        rapida.SetCustomField("cmc:conexionRapida", bool.TrueString);

        _conexiones.GetByIdAsync(rapida.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(rapida));

        var r = await Servicio().MarkAsSavedAsync(rapida.Id);

        Assert.True(r.Success);
        Assert.DoesNotContain("cmc:conexionRapida", rapida.CustomFields.Keys);
        await _conexiones.Received(1).UpdateAsync(
            Arg.Is<ConnectionRecord>(x => x.Connection.Id == rapida.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_una_conexion_rapida_que_ya_no_existe_falla_sin_romper()
    {
        var r = await Servicio().MarkAsSavedAsync(Guid.NewGuid());

        Assert.False(r.Success);
    }

    [Fact]
    public async Task Borrar_una_conexion_rapida_usa_el_mismo_camino_que_borrar_cualquier_conexion()
    {
        // FR-149: DeleteAsync ya borra la credencial junto con la conexión (BorradoYCredencialesTests).
        var rapida = new Connection(Guid.NewGuid(), "root@192.0.2.10", Protocol.Ssh, "192.0.2.10")
        {
            CredentialKey = "cmc:ssh:x",
        };

        _conexiones.GetByIdAsync(rapida.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(rapida));

        var r = await Servicio().DeleteAsync(rapida.Id);

        Assert.True(r.Success);
        await _credenciales.Received(1).DeleteAsync("cmc:ssh:x", Arg.Any<CancellationToken>());
        await _conexiones.Received(1).DeleteAsync(rapida.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_limpieza_del_arranque_borra_las_rapidas_que_quedaron_colgadas()
    {
        var rapida = Rapida("colgada");
        var normal = new Connection(Guid.NewGuid(), "Servidor de siempre", Protocol.Ssh, "192.0.2.2");

        _conexiones.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Connection> { rapida, normal });

        _conexiones.GetByIdAsync(rapida.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(rapida));

        var borradas = await Servicio().LimpiarConexionesRapidasAsync();

        Assert.Equal(1, borradas);
        await _conexiones.Received(1).DeleteAsync(rapida.Id, Arg.Any<CancellationToken>());
        await _conexiones.DidNotReceive().DeleteAsync(normal.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_limpieza_no_toca_una_rapida_que_el_usuario_ya_guardo()
    {
        var guardada = Rapida("ya guardada");
        guardada.SetCustomField("cmc:conexionRapida", null);

        _conexiones.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Connection> { guardada });

        var borradas = await Servicio().LimpiarConexionesRapidasAsync();

        Assert.Equal(0, borradas);
        await _conexiones.DidNotReceive()
            .DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static Connection Rapida(string nombre)
    {
        var c = new Connection(Guid.NewGuid(), nombre, Protocol.Ssh, "192.0.2.1");
        c.SetCustomField("cmc:conexionRapida", bool.TrueString);
        return c;
    }
}
