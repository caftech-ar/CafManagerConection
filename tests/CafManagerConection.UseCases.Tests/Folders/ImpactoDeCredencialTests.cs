using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Folders;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Folders;

/// <summary>
/// La clave de la credencial de carpeta es siempre "cmc:folder:{id}:{protocolo}": rotar sólo la
/// contraseña no la cambia, así que el conteo de impacto tiene que enterarse por otro lado (FR-063,
/// FR-064a).
/// </summary>
public sealed class ImpactoDeCredencialTests
{
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    private FolderService Servicio() => new(_carpetas, _conexiones, _credenciales);

    private void Arbol(List<Folder> carpetas, List<Connection> conexiones)
    {
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(carpetas);
        _conexiones.GetAllAsync(Arg.Any<CancellationToken>()).Returns(conexiones);

        foreach (var conexion in conexiones)
        {
            _conexiones.GetByIdAsync(conexion.Id, Arg.Any<CancellationToken>())
                .Returns(new ConnectionRecord(conexion));
        }
    }

    private static Connection EnCarpeta(Guid carpeta) =>
        new(Guid.NewGuid(), "Servidor", Protocol.Ssh, "192.0.2.1") { FolderId = carpeta };

    [Fact]
    public async Task Rotar_la_contrasena_de_la_carpeta_cuenta_a_quien_la_hereda()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        carpeta.Settings.SshCredentialKey = "cmc:folder:x:ssh";
        var conexion = EnCarpeta(carpeta.Id);
        Arbol([carpeta], [conexion]);

        var propuesta = new FolderSettings { SshCredentialKey = "cmc:folder:x:ssh" };

        var impacto = await Servicio().GetUpdateImpactAsync(
            carpeta.Id, propuesta, new HashSet<Protocol> { Protocol.Ssh });

        Assert.Equal(1, impacto);
    }

    [Fact]
    public async Task Sin_avisar_que_cambio_la_credencial_el_impacto_es_cero()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        carpeta.Settings.SshCredentialKey = "cmc:folder:x:ssh";
        var conexion = EnCarpeta(carpeta.Id);
        Arbol([carpeta], [conexion]);

        var propuesta = new FolderSettings { SshCredentialKey = "cmc:folder:x:ssh" };

        var impacto = await Servicio().GetUpdateImpactAsync(carpeta.Id, propuesta);

        Assert.Equal(0, impacto);
    }

    [Fact]
    public async Task No_cuenta_una_conexion_que_ya_tiene_credencial_propia()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        carpeta.Settings.SshCredentialKey = "cmc:folder:x:ssh";
        var conexion = EnCarpeta(carpeta.Id);
        conexion.CredentialKey = "cmc:ssh:propia";
        Arbol([carpeta], [conexion]);

        var propuesta = new FolderSettings { SshCredentialKey = "cmc:folder:x:ssh" };

        var impacto = await Servicio().GetUpdateImpactAsync(
            carpeta.Id, propuesta, new HashSet<Protocol> { Protocol.Ssh });

        Assert.Equal(0, impacto);
    }

    [Fact]
    public async Task No_cuenta_una_conexion_que_hereda_la_credencial_de_otra_carpeta_mas_cercana()
    {
        var abuela = new Folder(Guid.NewGuid(), "Trabajo");
        abuela.Settings.SshCredentialKey = "cmc:folder:abuela:ssh";
        var madre = new Folder(Guid.NewGuid(), "Vial", abuela.Id);
        madre.Settings.SshCredentialKey = "cmc:folder:madre:ssh";
        var conexion = EnCarpeta(madre.Id);
        Arbol([abuela, madre], [conexion]);

        var propuesta = new FolderSettings { SshCredentialKey = abuela.Settings.SshCredentialKey };

        var impacto = await Servicio().GetUpdateImpactAsync(
            abuela.Id, propuesta, new HashSet<Protocol> { Protocol.Ssh });

        Assert.Equal(0, impacto);
    }

    [Fact]
    public async Task Rotar_la_credencial_de_otro_protocolo_no_afecta_una_conexion_ssh()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        carpeta.Settings.SshCredentialKey = "cmc:folder:x:ssh";
        var conexion = EnCarpeta(carpeta.Id);
        Arbol([carpeta], [conexion]);

        var propuesta = new FolderSettings { SshCredentialKey = "cmc:folder:x:ssh" };

        var impacto = await Servicio().GetUpdateImpactAsync(
            carpeta.Id, propuesta, new HashSet<Protocol> { Protocol.Rdp });

        Assert.Equal(0, impacto);
    }
}
