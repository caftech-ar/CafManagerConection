using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Folders;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Folders;

// Cuántas conexiones descendientes cambian su valor efectivo al modificar la configuración
// heredable de una carpeta (FR-063). Contar también las que tienen valor propio convertiría la
// advertencia en ruido -siempre "todas las de abajo"- y nadie volvería a mirarla.
public sealed class FolderUpdateImpactTests
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

    private static Connection EnCarpeta(Guid carpeta, string? usuario = null) =>
        new(Guid.NewGuid(), "Servidor", Protocol.Ssh, "192.0.2.1")
        {
            FolderId = carpeta,
            UserName = usuario,
        };

    [Fact]
    public async Task Cuenta_una_conexion_que_hereda_el_usuario_nuevo()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        var conexion = EnCarpeta(carpeta.Id);
        Arbol([carpeta], [conexion]);

        var propuesta = new FolderSettings { UserName = "root" };

        var impacto = await Servicio().GetUpdateImpactAsync(carpeta.Id, propuesta);

        Assert.Equal(1, impacto);
    }

    [Fact]
    public async Task No_cuenta_una_conexion_con_usuario_propio()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        var conexion = EnCarpeta(carpeta.Id, usuario: "admin");
        Arbol([carpeta], [conexion]);

        var propuesta = new FolderSettings { UserName = "root" };

        var impacto = await Servicio().GetUpdateImpactAsync(carpeta.Id, propuesta);

        Assert.Equal(0, impacto);
    }

    [Fact]
    public async Task No_cuenta_una_conexion_fuera_de_la_rama()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        var ajena = new Folder(Guid.NewGuid(), "Otra");
        Arbol([carpeta, ajena], [EnCarpeta(ajena.Id)]);

        var propuesta = new FolderSettings { UserName = "root" };

        var impacto = await Servicio().GetUpdateImpactAsync(carpeta.Id, propuesta);

        Assert.Equal(0, impacto);
    }

    [Fact]
    public async Task Cuenta_las_de_las_subcarpetas()
    {
        var abuela = new Folder(Guid.NewGuid(), "Trabajo");
        var madre = new Folder(Guid.NewGuid(), "Norte", abuela.Id);
        Arbol([abuela, madre], [EnCarpeta(madre.Id), EnCarpeta(madre.Id, usuario: "propio")]);

        var propuesta = new FolderSettings { UserName = "root" };

        var impacto = await Servicio().GetUpdateImpactAsync(abuela.Id, propuesta);

        Assert.Equal(1, impacto);
    }

    [Fact]
    public async Task No_cuenta_nada_si_el_valor_efectivo_no_cambia()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Producción");
        carpeta.Settings.UserName = "root";
        var conexion = EnCarpeta(carpeta.Id);
        Arbol([carpeta], [conexion]);

        var propuesta = new FolderSettings { UserName = "root" };

        var impacto = await Servicio().GetUpdateImpactAsync(carpeta.Id, propuesta);

        Assert.Equal(0, impacto);
    }

    [Fact]
    public async Task Una_carpeta_vacia_no_cuenta_nada()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Sola");
        Arbol([carpeta], []);

        var impacto = await Servicio().GetUpdateImpactAsync(
            carpeta.Id, new FolderSettings { UserName = "root" });

        Assert.Equal(0, impacto);
    }
}
