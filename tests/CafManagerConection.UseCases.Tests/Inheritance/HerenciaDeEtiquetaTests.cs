using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Tests.Inheritance;

// La etiqueta se hereda como el resto de la configuración (FR-130). Estas pruebas venían del
// entorno fijo que la etiqueta reemplazó: se conservan porque el mecanismo de herencia y quién
// gana no cambió, sólo cambió qué se hereda.
public sealed class HerenciaDeEtiquetaTests
{
    private static readonly Guid Produccion = Guid.Parse("11111111-0000-4000-8000-000000000001");
    private static readonly Guid Laboratorio = Guid.Parse("11111111-0000-4000-8000-000000000004");

    private static Folder Carpeta(Guid id, Guid? etiqueta, Guid? padre = null) =>
        new(id, "Carpeta", padre)
        {
            Settings = new FolderSettings { TagId = etiqueta },
        };

    private static Connection Conexion(Guid? carpeta) =>
        new(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.1") { FolderId = carpeta };

    [Fact]
    public void Sin_nadie_que_lo_defina_queda_sin_etiqueta()
    {
        var resolver = new SettingsResolver([]);

        var efectivo = resolver.Resolve(Conexion(null));

        Assert.False(efectivo.TagId.IsDefined);
    }

    [Fact]
    public void La_conexion_hereda_el_etiqueta_de_su_carpeta()
    {
        var id = Guid.NewGuid();
        var resolver = new SettingsResolver([Carpeta(id, Produccion)]);

        var efectivo = resolver.Resolve(Conexion(id));

        Assert.Equal(Produccion, efectivo.TagId.Value);
        Assert.True(efectivo.TagId.IsInherited);
    }

    [Fact]
    public void El_etiqueta_propio_gana_sobre_el_de_la_carpeta()
    {
        var id = Guid.NewGuid();
        var resolver = new SettingsResolver([Carpeta(id, Produccion)]);

        var c = Conexion(id);
        c.TagId = Laboratorio;

        var efectivo = resolver.Resolve(c);

        Assert.Equal(Laboratorio, efectivo.TagId.Value);
        Assert.False(efectivo.TagId.IsInherited);
    }

    [Fact]
    public void La_herencia_atraviesa_varios_niveles_de_carpetas()
    {
        var abuela = Guid.NewGuid();
        var madre = Guid.NewGuid();

        var resolver = new SettingsResolver([
            Carpeta(abuela, Produccion),
            Carpeta(madre, etiqueta: null, padre: abuela),
        ]);

        var efectivo = resolver.Resolve(Conexion(madre));

        Assert.Equal(Produccion, efectivo.TagId.Value);
    }

    [Fact]
    public void Gana_la_carpeta_mas_cercana_que_lo_defina()
    {
        var abuela = Guid.NewGuid();
        var madre = Guid.NewGuid();

        var resolver = new SettingsResolver([
            Carpeta(abuela, Produccion),
            Carpeta(madre, Laboratorio, padre: abuela),
        ]);

        var efectivo = resolver.Resolve(Conexion(madre));

        Assert.Equal(Laboratorio, efectivo.TagId.Value);
    }
}
