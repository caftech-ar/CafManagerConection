using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Folders;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Folders;

// Alimenta la confirmación previa al borrado: un número menor que el real es peor que no
// mostrar ninguno, porque es lo único que le dice al usuario qué está por perder.
public sealed class FolderDeletionImpactTests
{
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    private FolderService Servicio() => new(_carpetas, _conexiones, _credenciales);

    private void Arbol(List<Folder> carpetas, List<Connection> conexiones)
    {
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(carpetas);
        _conexiones.GetAllAsync(Arg.Any<CancellationToken>()).Returns(conexiones);
    }

    private static Connection EnCarpeta(Guid carpeta) =>
        new(Guid.NewGuid(), "Servidor", Protocol.Ssh, "192.0.2.1") { FolderId = carpeta };

    [Fact]
    public async Task Una_carpeta_vacia_no_arrastra_nada_mas_que_a_si_misma()
    {
        var sola = new Folder(Guid.NewGuid(), "Vacía");
        Arbol([sola], []);

        var impacto = await Servicio().GetDeletionImpactAsync(sola.Id);

        Assert.Equal(1, impacto.FolderCount);
        Assert.Equal(0, impacto.ConnectionCount);
    }

    [Fact]
    public async Task Cuenta_las_conexiones_que_contiene()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Trabajo");
        Arbol([carpeta], [EnCarpeta(carpeta.Id), EnCarpeta(carpeta.Id)]);

        var impacto = await Servicio().GetDeletionImpactAsync(carpeta.Id);

        Assert.Equal(2, impacto.ConnectionCount);
    }

    [Fact]
    public async Task Cuenta_tambien_lo_que_hay_en_las_subcarpetas()
    {
        var abuela = new Folder(Guid.NewGuid(), "Trabajo");
        var madre = new Folder(Guid.NewGuid(), "Norte", abuela.Id);
        var nieta = new Folder(Guid.NewGuid(), "Producción", madre.Id);

        Arbol(
            [abuela, madre, nieta],
            [EnCarpeta(abuela.Id), EnCarpeta(madre.Id), EnCarpeta(nieta.Id), EnCarpeta(nieta.Id)]);

        var impacto = await Servicio().GetDeletionImpactAsync(abuela.Id);

        Assert.Equal(3, impacto.FolderCount);
        Assert.Equal(4, impacto.ConnectionCount);
    }

    [Fact]
    public async Task No_cuenta_lo_que_esta_fuera_de_la_rama()
    {
        var objetivo = new Folder(Guid.NewGuid(), "Trabajo");
        var ajena = new Folder(Guid.NewGuid(), "Otra");

        Arbol([objetivo, ajena], [EnCarpeta(objetivo.Id), EnCarpeta(ajena.Id)]);

        var impacto = await Servicio().GetDeletionImpactAsync(objetivo.Id);

        Assert.Equal(1, impacto.FolderCount);
        Assert.Equal(1, impacto.ConnectionCount);
    }

    [Fact]
    public async Task No_cuenta_las_conexiones_de_la_raiz()
    {
        var carpeta = new Folder(Guid.NewGuid(), "Trabajo");
        var suelta = new Connection(Guid.NewGuid(), "Suelta", Protocol.Ssh, "192.0.2.9");

        Arbol([carpeta], [suelta]);

        var impacto = await Servicio().GetDeletionImpactAsync(carpeta.Id);

        Assert.Equal(0, impacto.ConnectionCount);
    }

    [Fact]
    public async Task Una_carpeta_que_ya_no_existe_no_arrastra_nada()
    {
        Arbol([new Folder(Guid.NewGuid(), "Trabajo")], []);

        var impacto = await Servicio().GetDeletionImpactAsync(Guid.NewGuid());

        Assert.Equal(0, impacto.ConnectionCount);
    }
}
