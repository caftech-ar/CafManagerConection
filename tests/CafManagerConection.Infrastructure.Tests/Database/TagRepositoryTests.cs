using CafManagerConection.Domain.Connections;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <remarks><see cref="TagRepository.GetAllAsync"/> fallaba con una excepción de Dapper para toda la tabla.</remarks>
public class TagRepositoryTests
{
    private static async Task<(TempDatabase Db, TagRepository Repo)> CrearAsync()
    {
        var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();
        return (db, new TagRepository(db.Factory));
    }

    /// <remarks>El record posicional tenía un parámetro <c>int</c>, y un INTEGER de SQLite llega como <c>Int64</c>.</remarks>
    [Fact]
    public async Task Lee_las_etiquetas_que_siembra_la_migracion()
    {
        var (db, repo) = await CrearAsync();
        using var _ = db;

        var etiquetas = await repo.GetAllAsync();

        Assert.Equal(EtiquetasDeFabrica.Cantidad, etiquetas.Count);

        Assert.Equal(
            EtiquetasDeFabrica.Crear().Select(e => (e.Codigo, e.Nombre, e.ClaveDeColor, e.Orden)),
            etiquetas.OrderBy(e => e.Orden).Select(e => (e.Codigo, e.Nombre, e.ClaveDeColor, e.Orden)));
    }

    [Fact]
    public async Task Guarda_y_recupera_una_etiqueta_nueva()
    {
        var (db, repo) = await CrearAsync();
        using var _ = db;
        var nueva = new Etiqueta(Guid.NewGuid(), "hom", "Homologación", "violeta", 9);

        await repo.AddAsync(nueva);
        var recuperada = (await repo.GetAllAsync()).Single(e => e.Id == nueva.Id);

        Assert.Equal("HOM", recuperada.Codigo);
        Assert.Equal("Homologación", recuperada.Nombre);
        Assert.Equal("violeta", recuperada.ClaveDeColor);
        Assert.Equal(9, recuperada.Orden);
    }

    [Fact]
    public async Task Actualiza_codigo_nombre_clave_de_color_y_orden()
    {
        var (db, repo) = await CrearAsync();
        using var _ = db;
        var etiqueta = (await repo.GetAllAsync()).First(e => e.Codigo == "DESA");

        etiqueta.Renombrar("lab", "Laboratorio", "cyan");
        etiqueta.Orden = 42;
        await repo.UpdateAsync(etiqueta);

        var recuperada = (await repo.GetAllAsync()).Single(e => e.Id == etiqueta.Id);

        Assert.Equal("LAB", recuperada.Codigo);
        Assert.Equal("Laboratorio", recuperada.Nombre);
        Assert.Equal("cyan", recuperada.ClaveDeColor);
        Assert.Equal(42, recuperada.Orden);
    }

    [Fact]
    public async Task Devuelve_las_etiquetas_en_el_orden_guardado()
    {
        var (db, repo) = await CrearAsync();
        using var _ = db;

        var todas = await repo.GetAllAsync();
        var primera = todas[0];
        var segunda = todas[1];

        (primera.Orden, segunda.Orden) = (segunda.Orden, primera.Orden);
        await repo.UpdateAsync(primera);
        await repo.UpdateAsync(segunda);

        var reordenadas = await repo.GetAllAsync();

        Assert.Equal(segunda.Id, reordenadas[0].Id);
        Assert.Equal(primera.Id, reordenadas[1].Id);
    }

    /// <remarks>La clave foránea es <c>ON DELETE SET NULL</c> a propósito, no <c>CASCADE</c>.</remarks>
    [Fact]
    public async Task Borrar_una_etiqueta_deja_la_conexion_sin_etiqueta_pero_no_la_borra()
    {
        var (db, repo) = await CrearAsync();
        using var _ = db;
        var conexiones = new ConnectionRepository(db.Factory);
        var produccion = (await repo.GetAllAsync()).First(e => e.Codigo == "PRD");

        var conexion = new Connection(Guid.NewGuid(), "web-01", Protocol.Ssh, "192.0.2.1")
        {
            TagId = produccion.Id,
        };
        await conexiones.AddAsync(new ConnectionRecord(
            conexion, Ssh: new SshSettings { ConnectionId = conexion.Id }));

        Assert.Equal(1, await repo.CountUsagesAsync(produccion.Id));

        await repo.DeleteAsync(produccion.Id);

        var sobreviviente = await conexiones.GetByIdAsync(conexion.Id);

        Assert.NotNull(sobreviviente);
        Assert.Null(sobreviviente.Connection.TagId);
        Assert.DoesNotContain(await repo.GetAllAsync(), e => e.Id == produccion.Id);
    }

    [Fact]
    public async Task Cuenta_los_usos_de_conexiones_y_de_carpetas()
    {
        var (db, repo) = await CrearAsync();
        using var _ = db;
        var conexiones = new ConnectionRepository(db.Factory);
        var carpetas = new FolderRepository(db.Factory);
        var etiqueta = (await repo.GetAllAsync()).First(e => e.Codigo == "PRE");

        var conexion = new Connection(Guid.NewGuid(), "a", Protocol.Ssh, "192.0.2.1")
        {
            TagId = etiqueta.Id,
        };
        await conexiones.AddAsync(new ConnectionRecord(
            conexion, Ssh: new SshSettings { ConnectionId = conexion.Id }));

        var carpeta = new Folder(Guid.NewGuid(), "Preproducción");
        carpeta.Settings.TagId = etiqueta.Id;
        await carpetas.AddAsync(carpeta);

        Assert.Equal(2, await repo.CountUsagesAsync(etiqueta.Id));
    }
}
