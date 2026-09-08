using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Tests.Connections;

public class FolderTests
{
    [Fact]
    public void Crear_recorta_el_nombre()
    {
        var folder = new Folder(Guid.NewGuid(), "  Producción  ");

        Assert.Equal("Producción", folder.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_rechaza_un_nombre_vacio(string name)
    {
        Assert.Throws<ArgumentException>(() => new Folder(Guid.NewGuid(), name));
    }

    [Fact]
    public void Crear_rechaza_un_nombre_demasiado_largo()
    {
        var largo = new string('a', Folder.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => new Folder(Guid.NewGuid(), largo));
    }

    [Fact]
    public void Crear_acepta_el_nombre_de_longitud_maxima()
    {
        var limite = new string('a', Folder.MaxNameLength);

        var folder = new Folder(Guid.NewGuid(), limite);

        Assert.Equal(Folder.MaxNameLength, folder.Name.Length);
    }

    [Fact]
    public void MoveTo_rechaza_que_la_carpeta_sea_su_propio_padre()
    {
        var id = Guid.NewGuid();
        var folder = new Folder(id, "Producción");

        var ex = Assert.Throws<InvalidOperationException>(() => folder.MoveTo(id));

        Assert.Contains("propia carpeta", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MoveTo_a_la_raiz_deja_el_padre_nulo()
    {
        var folder = new Folder(Guid.NewGuid(), "Producción", parentId: Guid.NewGuid());

        folder.MoveTo(null);

        Assert.Null(folder.ParentId);
    }

    [Fact]
    public void Rename_actualiza_la_fecha_de_modificacion()
    {
        var folder = new Folder(Guid.NewGuid(), "Antes")
        {
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        var antes = folder.UpdatedAt;

        Thread.Sleep(2);
        folder.Rename("Después");

        Assert.Equal("Después", folder.Name);
        Assert.True(folder.UpdatedAt > antes);
    }

    [Fact]
    public void Una_carpeta_nueva_no_define_ningun_valor_heredable()
    {
        var folder = new Folder(Guid.NewGuid(), "Producción");

        Assert.True(folder.Settings.IsEmpty);
    }
}
