using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Credentials;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Credentials;

public sealed class CredentialProviderTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _almacen = Substitute.For<ICredentialStore>();
    private readonly ICredentialPrompt _pregunta = Substitute.For<ICredentialPrompt>();

    private readonly Connection _conexion =
        new(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.207");

    public CredentialProviderTests()
    {
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder>());

        _conexiones.GetByIdAsync(_conexion.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(_conexion, Ssh: new SshSettings()));
    }

    private ReferenciaDeSecreto Propia =>
        ReferenciaDeSecreto.DeConexion(_conexion.Id, Protocol.Ssh);

    private CredentialProvider Proveedor(bool conPregunta = true) =>
        new(_conexiones, _carpetas, _almacen, conPregunta ? _pregunta : null);

    [Fact]
    public async Task Devuelve_la_credencial_guardada_sin_preguntar_nada()
    {
        _conexion.TieneSecreto = true;
        _conexion.UserName = "root";
        _almacen.ReadAsync(Propia, Arg.Any<CancellationToken>())
            .Returns("secreto".ToCharArray());

        var r = await Proveedor().GetForConnectionAsync(_conexion.Id);

        Assert.Equal("root", r!.UserName);
        Assert.Equal("secreto", r.RevealSecret());

        await _pregunta.DidNotReceiveWithAnyArgs()
            .RequestAsync(default!, default, default, default);
    }

    [Fact]
    public async Task Sin_credencial_la_pide_en_lugar_de_fallar()
    {
        _pregunta.RequestAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new CredentialPromptResult("root", null, "tecleada", Remember: false));

        var r = await Proveedor().GetForConnectionAsync(_conexion.Id);

        Assert.Equal("root", r!.UserName);
        Assert.Equal("tecleada", r.RevealSecret());
    }

    [Fact]
    public async Task Si_el_usuario_cancela_no_devuelve_credencial()
    {
        _pregunta.RequestAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CredentialPromptResult?)null);

        Assert.Null(await Proveedor().GetForConnectionAsync(_conexion.Id));
    }

    [Fact]
    public async Task Una_marca_sin_secreto_detras_se_vuelve_a_pedir()
    {
        _conexion.TieneSecreto = true;
        _almacen.ReadAsync(Propia, Arg.Any<CancellationToken>()).Returns((char[]?)null);

        _pregunta.RequestAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new CredentialPromptResult("root", null, "otra", Remember: false));

        var r = await Proveedor().GetForConnectionAsync(_conexion.Id);

        Assert.Equal("otra", r!.RevealSecret());
    }

    [Fact]
    public async Task Con_recordar_se_guarda_en_la_fila_de_la_conexion()
    {
        _pregunta.RequestAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new CredentialPromptResult("root", null, "s", Remember: true));

        await Proveedor().GetForConnectionAsync(_conexion.Id);

        await _almacen.Received(1).WriteAsync(
            Propia, Arg.Any<ReadOnlyMemory<char>>(), Arg.Any<CancellationToken>());

        Assert.True(_conexion.TieneSecreto);
    }

    [Fact]
    public async Task Sin_recordar_no_se_guarda_nada()
    {
        _pregunta.RequestAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new CredentialPromptResult("root", null, "s", Remember: false));

        await Proveedor().GetForConnectionAsync(_conexion.Id);

        await _almacen.DidNotReceiveWithAnyArgs().WriteAsync(default, default, default);

        Assert.False(_conexion.TieneSecreto);
    }

    [Fact]
    public async Task La_contrasena_de_la_carpeta_sirve_para_lo_que_cuelga_de_ella()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Trabajo")
        {
            Settings = new FolderSettings { SshTieneSecreto = true, UserName = "heredado" },
        };

        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder> { carpeta });
        _conexion.FolderId = carpeta.Id;

        _almacen
            .ReadAsync(
                ReferenciaDeSecreto.DeCarpeta(carpeta.Id, Protocol.Ssh),
                Arg.Any<CancellationToken>())
            .Returns("s".ToCharArray());

        var r = await Proveedor().GetForConnectionAsync(_conexion.Id);

        Assert.Equal("heredado", r!.UserName);
    }

    [Fact]
    public async Task Sin_manera_de_preguntar_devuelve_null_en_vez_de_romper()
    {
        Assert.Null(await Proveedor(conPregunta: false).GetForConnectionAsync(_conexion.Id));
    }

    [Fact]
    public async Task Una_conexion_que_ya_no_existe_devuelve_null()
    {
        Assert.Null(await Proveedor().GetForConnectionAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task A_una_conexion_ssh_por_clave_privada_no_se_le_pide_contrasena()
    {
        _conexiones.GetByIdAsync(_conexion.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(
                _conexion,
                Ssh: new SshSettings { AuthMethod = SshAuthMethod.PrivateKey }));

        Assert.Null(await Proveedor().GetForConnectionAsync(_conexion.Id));

        await _pregunta.DidNotReceiveWithAnyArgs()
            .RequestAsync(default!, default, default, default);
    }

    [Fact]
    public async Task A_una_entrada_web_no_se_le_pide_contrasena()
    {
        var web = new Connection(Guid.NewGuid(), "Kuma", Protocol.Web, "192.0.2.207");

        _conexiones.GetByIdAsync(web.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(web, Web: new WebSettings()));

        Assert.Null(await Proveedor().GetForConnectionAsync(web.Id));

        await _pregunta.DidNotReceiveWithAnyArgs()
            .RequestAsync(default!, default, default, default);
    }

    [Fact]
    public async Task A_una_conexion_rdp_se_le_pide_tambien_el_dominio()
    {
        var rdp = new Connection(Guid.NewGuid(), "Pivote", Protocol.Rdp, "192.0.2.5");

        _conexiones.GetByIdAsync(rdp.Id, Arg.Any<CancellationToken>())
            .Returns(new ConnectionRecord(rdp, Rdp: new RdpSettings()));

        _pregunta.RequestAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new CredentialPromptResult("admin", "CORP", "s", Remember: false));

        await Proveedor().GetForConnectionAsync(rdp.Id);

        await _pregunta.Received(1).RequestAsync(
            "Pivote", Arg.Any<string?>(), needsDomain: true, Arg.Any<CancellationToken>());
    }
}
