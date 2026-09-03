using CafManagerConection.Domain.Connections;
using CafManagerConection.Infrastructure.Credentials;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Folders;

namespace CafManagerConection.Infrastructure.Tests.Database;

// FR-193 y FR-194: hasta ahora IFolderRepository no tenía ReorderAsync y las carpetas no se reordenaban.
public class FolderRepositoryReorderTests
{
    private static async Task<(TempDatabase Db, FolderRepository Carpetas, FolderService Servicio)>
        CrearAsync()
    {
        var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        var carpetas = new FolderRepository(db.Factory);
        var conexiones = new ConnectionRepository(db.Factory);

        return (db, carpetas, new FolderService(carpetas, conexiones, new WindowsCredentialStore()));
    }

    private static async Task<Folder> AgregarAsync(
        FolderRepository repo, string nombre, Guid? padre, int orden)
    {
        var carpeta = new Folder(Guid.NewGuid(), nombre, padre, orden);
        await repo.AddAsync(carpeta);
        return carpeta;
    }

    [Fact]
    public async Task Reordenar_hermanas_cambia_el_orden_que_devuelve_la_base()
    {
        var (db, carpetas, _) = await CrearAsync();
        using var _d = db;

        var alfa = await AgregarAsync(carpetas, "Alfa", null, 0);
        var bravo = await AgregarAsync(carpetas, "Bravo", null, 1);
        var charlie = await AgregarAsync(carpetas, "Charlie", null, 2);

        await carpetas.ReorderAsync(null, [charlie.Id, alfa.Id, bravo.Id]);

        var leidas = await carpetas.GetAllAsync();

        Assert.Equal(
            ["Charlie", "Alfa", "Bravo"],
            leidas.Where(f => f.ParentId is null).Select(f => f.Name));
    }

    [Fact]
    public async Task Un_identificador_que_no_existe_no_rompe_el_reordenamiento()
    {
        var (db, carpetas, _) = await CrearAsync();
        using var _d = db;

        var alfa = await AgregarAsync(carpetas, "Alfa", null, 0);
        var bravo = await AgregarAsync(carpetas, "Bravo", null, 1);

        await carpetas.ReorderAsync(null, [alfa.Id, Guid.NewGuid(), bravo.Id]);

        var leidas = await carpetas.GetAllAsync();

        Assert.Equal(0, leidas.Single(f => f.Name == "Alfa").SortOrder);
        Assert.Equal(2, leidas.Single(f => f.Name == "Bravo").SortOrder);
    }

    [Fact]
    public async Task Mover_una_carpeta_a_otra_le_deja_la_posicion_que_se_pidio()
    {
        var (db, carpetas, servicio) = await CrearAsync();
        using var _d = db;

        var destino = await AgregarAsync(carpetas, "Trabajo", null, 0);
        await AgregarAsync(carpetas, "Alfa", destino.Id, 0);
        await AgregarAsync(carpetas, "Charlie", destino.Id, 1);
        var venida = await AgregarAsync(carpetas, "Zeta", null, 5);

        var r = await servicio.MoveAsync(venida.Id, destino.Id, posicion: 1);

        Assert.True(r.Success);

        var leidas = await carpetas.GetAllAsync();

        Assert.Equal(
            ["Alfa", "Zeta", "Charlie"],
            leidas.Where(f => f.ParentId == destino.Id).Select(f => f.Name));
    }

    [Fact]
    public async Task Mover_una_carpeta_dentro_de_su_nieta_se_rechaza_y_no_toca_la_base()
    {
        var (db, carpetas, servicio) = await CrearAsync();
        using var _d = db;

        var abuela = await AgregarAsync(carpetas, "Trabajo", null, 0);
        var madre = await AgregarAsync(carpetas, "Vial", abuela.Id, 0);
        var nieta = await AgregarAsync(carpetas, "Producción", madre.Id, 0);

        var r = await servicio.MoveAsync(abuela.Id, nieta.Id);

        Assert.False(r.Success);

        var leida = await carpetas.GetByIdAsync(abuela.Id);

        Assert.Null(leida!.ParentId);
    }

    [Fact]
    public async Task El_orden_de_las_carpetas_sobrevive_a_volver_a_leer_la_base()
    {
        var (db, carpetas, _) = await CrearAsync();
        using var _d = db;

        var alfa = await AgregarAsync(carpetas, "Alfa", null, 0);
        var bravo = await AgregarAsync(carpetas, "Bravo", null, 1);

        await carpetas.ReorderAsync(null, [bravo.Id, alfa.Id]);

        var otra = new FolderRepository(db.Factory);
        var leidas = await otra.GetAllAsync();

        Assert.Equal(["Bravo", "Alfa"], leidas.Select(f => f.Name));
    }
}
