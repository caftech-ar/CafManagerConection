using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Tests.Connections;

// Estos campos existen para que agregar un dato más adelante no obligue a migrar la base otra
// vez (FR-129 a FR-134).
public sealed class ConexionCatalogoTests
{
    private static Connection Nueva() =>
        new(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.207");

    [Fact]
    public void Una_conexion_nace_sin_padre()
    {
        Assert.Null(Nueva().ParentConnectionId);
    }

    [Fact]
    public void Una_conexion_no_puede_ser_su_propio_padre()
    {
        var c = Nueva();

        Assert.Throws<ArgumentException>(() => c.ParentConnectionId = c.Id);
    }

    [Fact]
    public void Quitar_el_padre_es_valido()
    {
        var c = Nueva();
        c.ParentConnectionId = Guid.NewGuid();

        c.ParentConnectionId = null;

        Assert.Null(c.ParentConnectionId);
    }

    [Fact]
    public void La_descripcion_es_corta_y_distinta_de_las_notas()
    {
        // La descripción se muestra en el árbol y las notas se abren a propósito (FR-131).
        var c = Nueva();

        c.Description = new string('a', Connection.MaxDescriptionLength);

        Assert.Throws<ArgumentException>(
            () => c.Description = new string('a', Connection.MaxDescriptionLength + 1));
    }

    [Fact]
    public void Una_descripcion_en_blanco_queda_nula()
    {
        // Que "sin descripción" tenga una sola representación evita comparar contra "" y null.
        var c = Nueva();

        c.Description = "   ";

        Assert.Null(c.Description);
    }

    [Fact]
    public void La_documentacion_acepta_una_direccion_web_absoluta()
    {
        var c = Nueva();

        c.DocumentationUrl = "https://wiki.interno/aplicaciones";

        Assert.Equal("https://wiki.interno/aplicaciones", c.DocumentationUrl);
    }

    [Theory]
    [InlineData("wiki.interno/aplicaciones")]
    [InlineData("no es una url")]
    [InlineData("file:///c:/secretos.txt")]
    public void La_documentacion_rechaza_lo_que_no_sea_http(string valor)
    {
        // Se restringe a http y https a propósito: abrir un `file://` o un esquema arbitrario
        // desde un campo guardado es una forma de ejecutar algo sin quererlo.
        var c = Nueva();

        Assert.Throws<ArgumentException>(() => c.DocumentationUrl = valor);
    }

    [Fact]
    public void Una_conexion_nace_sin_campos_propios()
    {
        Assert.Empty(Nueva().CustomFields);
    }

    [Fact]
    public void Un_campo_propio_se_guarda_y_se_lee_por_nombre()
    {
        var c = Nueva();

        c.SetCustomField("responsable", "Infraestructura");

        Assert.Equal("Infraestructura", c.CustomFields["responsable"]);
    }

    [Fact]
    public void El_nombre_de_un_campo_propio_no_distingue_mayusculas()
    {
        var c = Nueva();

        c.SetCustomField("Responsable", "Infra");
        c.SetCustomField("responsable", "Redes");

        Assert.Single(c.CustomFields);
        Assert.Equal("Redes", c.CustomFields["RESPONSABLE"]);
    }

    [Fact]
    public void Asignar_null_borra_el_campo_propio()
    {
        var c = Nueva();
        c.SetCustomField("responsable", "Infra");

        c.SetCustomField("responsable", null);

        Assert.Empty(c.CustomFields);
    }

    [Fact]
    public void Un_campo_propio_sin_nombre_se_rechaza()
    {
        var c = Nueva();

        Assert.Throws<ArgumentException>(() => c.SetCustomField("  ", "algo"));
    }

    [Fact]
    public void Una_conexion_nace_sin_ser_favorita()
    {
        Assert.False(Nueva().IsFavorite);
    }
}
