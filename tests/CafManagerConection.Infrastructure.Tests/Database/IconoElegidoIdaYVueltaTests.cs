using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Tests.Database;

// La 006 creó icon_key en las dos tablas; lo que se prueba acá es que los repositorios la escriban
// y la lean, que es lo que faltaba.
public class IconoElegidoIdaYVueltaTests
{
    private static async Task<TempDatabase> CrearAsync()
    {
        var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();
        return db;
    }

    [Fact]
    public async Task Una_carpeta_guarda_y_recupera_su_clave_de_icono()
    {
        using var db = await CrearAsync();
        var repo = new FolderRepository(db.Factory);
        var carpeta = new Folder(Guid.NewGuid(), "Bases") { ClaveDeIcono = "base-de-datos" };

        await repo.AddAsync(carpeta);
        var recuperada = await repo.GetByIdAsync(carpeta.Id);

        Assert.Equal("base-de-datos", recuperada!.ClaveDeIcono);
    }

    [Fact]
    public async Task Una_carpeta_sin_icono_elegido_vuelve_en_nulo()
    {
        using var db = await CrearAsync();
        var repo = new FolderRepository(db.Factory);
        var carpeta = new Folder(Guid.NewGuid(), "Sin icono");

        await repo.AddAsync(carpeta);
        var recuperada = await repo.GetByIdAsync(carpeta.Id);

        Assert.Null(recuperada!.ClaveDeIcono);
    }

    [Fact]
    public async Task Guardar_de_nuevo_una_carpeta_no_pierde_el_icono()
    {
        using var db = await CrearAsync();
        var repo = new FolderRepository(db.Factory);
        var carpeta = new Folder(Guid.NewGuid(), "Bases") { ClaveDeIcono = "base-de-datos" };
        await repo.AddAsync(carpeta);

        carpeta.Rename("Bases de datos");
        await repo.UpdateAsync(carpeta);

        var recuperada = await repo.GetByIdAsync(carpeta.Id);

        Assert.Equal("Bases de datos", recuperada!.Name);
        Assert.Equal("base-de-datos", recuperada.ClaveDeIcono);
    }

    [Fact]
    public async Task Una_carpeta_puede_dejar_de_tener_icono()
    {
        using var db = await CrearAsync();
        var repo = new FolderRepository(db.Factory);
        var carpeta = new Folder(Guid.NewGuid(), "Bases") { ClaveDeIcono = "base-de-datos" };
        await repo.AddAsync(carpeta);

        carpeta.ClaveDeIcono = null;
        await repo.UpdateAsync(carpeta);

        Assert.Null((await repo.GetByIdAsync(carpeta.Id))!.ClaveDeIcono);
    }

    [Fact]
    public async Task El_icono_y_el_color_de_una_carpeta_se_guardan_por_separado()
    {
        using var db = await CrearAsync();
        var repo = new FolderRepository(db.Factory);
        var carpeta = new Folder(Guid.NewGuid(), "Correo")
        {
            ClaveDeColor = "violeta",
            ClaveDeIcono = "correo",
        };

        await repo.AddAsync(carpeta);
        var recuperada = await repo.GetByIdAsync(carpeta.Id);

        Assert.Equal("violeta", recuperada!.ClaveDeColor);
        Assert.Equal("correo", recuperada.ClaveDeIcono);
    }

    [Fact]
    public async Task Una_conexion_guarda_y_recupera_su_clave_de_icono()
    {
        using var db = await CrearAsync();
        var repo = new ConnectionRepository(db.Factory);
        var c = new Connection(Guid.NewGuid(), "Respaldos", Protocol.Ssh, "192.0.2.9")
        {
            ClaveDeIcono = "respaldo",
        };

        await repo.AddAsync(new ConnectionRecord(c));
        var recuperada = await repo.GetByIdAsync(c.Id);

        Assert.Equal("respaldo", recuperada!.Connection.ClaveDeIcono);
    }

    [Fact]
    public async Task Una_conexion_sin_icono_elegido_vuelve_en_nulo()
    {
        using var db = await CrearAsync();
        var repo = new ConnectionRepository(db.Factory);
        var c = new Connection(Guid.NewGuid(), "Sin icono", Protocol.Rdp, "192.0.2.8");

        await repo.AddAsync(new ConnectionRecord(c));
        var recuperada = await repo.GetByIdAsync(c.Id);

        Assert.Null(recuperada!.Connection.ClaveDeIcono);
    }

    [Fact]
    public async Task Actualizar_una_conexion_conserva_el_icono()
    {
        using var db = await CrearAsync();
        var repo = new ConnectionRepository(db.Factory);
        var c = new Connection(Guid.NewGuid(), "Cortafuegos", Protocol.Ssh, "192.0.2.7")
        {
            ClaveDeIcono = "cortafuegos",
        };
        await repo.AddAsync(new ConnectionRecord(c));

        c.ChangeHost("192.0.2.70");
        await repo.UpdateAsync(new ConnectionRecord(c));

        var recuperada = await repo.GetByIdAsync(c.Id);

        Assert.Equal("192.0.2.70", recuperada!.Connection.Host);
        Assert.Equal("cortafuegos", recuperada.Connection.ClaveDeIcono);
    }

    [Fact]
    public async Task El_icono_y_el_color_de_una_conexion_se_guardan_por_separado()
    {
        using var db = await CrearAsync();
        var repo = new ConnectionRepository(db.Factory);
        var c = new Connection(Guid.NewGuid(), "Monitoreo", Protocol.Web, "grafana.local")
        {
            ClaveDeColor = "lima",
            ClaveDeIcono = "monitoreo",
        };

        await repo.AddAsync(new ConnectionRecord(c));
        var recuperada = await repo.GetByIdAsync(c.Id);

        Assert.Equal("lima", recuperada!.Connection.ClaveDeColor);
        Assert.Equal("monitoreo", recuperada.Connection.ClaveDeIcono);
    }

    [Fact]
    public async Task La_lista_completa_de_conexiones_tambien_trae_el_icono()
    {
        using var db = await CrearAsync();
        var repo = new ConnectionRepository(db.Factory);
        var c = new Connection(Guid.NewGuid(), "Contenedores", Protocol.Ssh, "192.0.2.6")
        {
            ClaveDeIcono = "contenedor",
        };
        await repo.AddAsync(new ConnectionRecord(c));

        var todas = await repo.GetAllAsync();

        Assert.Equal("contenedor", todas.Single(x => x.Id == c.Id).ClaveDeIcono);
    }

    [Fact]
    public async Task Toda_clave_del_juego_sobrevive_la_ida_y_vuelta()
    {
        using var db = await CrearAsync();
        var repo = new FolderRepository(db.Factory);

        foreach (var icono in JuegoDeIconos.Iconos)
        {
            var carpeta = new Folder(Guid.NewGuid(), icono.Nombre) { ClaveDeIcono = icono.Clave };
            await repo.AddAsync(carpeta);

            Assert.Equal(icono.Clave, (await repo.GetByIdAsync(carpeta.Id))!.ClaveDeIcono);
        }
    }
}
