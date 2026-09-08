namespace CafManagerConection.Domain.Tests;

// Los defectos de version, tarea y atajo que tenia el README anterior, sembrados a proposito. Usa
// los extractores del guardian y no una copia de sus patrones: dos regex que tienen que coincidir
// se desfasan.
public sealed class ReadmeDetectaLosDefectosTests
{
    [Fact]
    public void Una_version_que_no_es_la_del_proyecto_se_extrae() =>
        Assert.Equal(
            ["0.1.2"],
            ReadmeComprobableTests.VersionesCitadasEn("Ya hay binario de la 0.1.2 publicado."));

    // net10.0 y las medidas con coma no son versiones del producto.
    [Fact]
    public void Lo_que_no_es_una_version_de_tres_partes_no_se_extrae() =>
        Assert.Empty(ReadmeComprobableTests.VersionesCitadasEn(
            "Compila para net10.0-windows y mide 0,05 % de una CPU."));

    [Fact]
    public void Una_tarea_inexistente_se_extrae() =>
        Assert.Equal(
            ["publicar", "test"],
            ReadmeComprobableTests.TareasCitadasEn(
                "Corré `task publicar` y después `task test`."));

    [Theory]
    [InlineData("Buscar con `Ctrl+F`.", "Ctrl+F")]
    [InlineData("Maximizar con `F11`.", "F11")]
    [InlineData("Alternar con `Ctrl+Tab`.", "Ctrl+Tab")]
    [InlineData("Editar con `F2`.", "F2")]
    [InlineData("Eliminar con `Delete`.", "Delete")]
    public void Un_atajo_entre_acentos_graves_se_extrae(string texto, string esperado) =>
        Assert.Equal([esperado], ReadmeComprobableTests.AtajosCitadosEn(texto));

    // Los dos falsos positivos que aparecieron al escribir el README: el prefijo F1 de las
    // geometrias de Fluent y la letra suelta del ejemplo de base64.
    [Theory]
    [InlineData("La versión F11 del control no existe; Ctrl+F tampoco.")]
    [InlineData("En base64 la `a` y la `A` son valores distintos.")]
    [InlineData("El `path` va con el prefijo F1 adelante.")]
    public void Lo_que_no_es_un_atajo_no_se_extrae(string texto) =>
        Assert.Empty(ReadmeComprobableTests.AtajosCitadosEn(texto));

    [Fact]
    public void Un_atajo_omitido_se_detecta_por_diferencia()
    {
        var enElCodigo = new HashSet<string>(["Ctrl+F", "Ctrl+K", "F12"], StringComparer.Ordinal);
        var enElReadme = new HashSet<string>(["Ctrl+F"], StringComparer.Ordinal);

        Assert.Equal(["Ctrl+K", "F12"], enElCodigo.Except(enElReadme).Order());
    }

    [Fact]
    public void Un_atajo_inventado_se_detecta_por_diferencia()
    {
        var enElCodigo = new HashSet<string>(["Ctrl+F"], StringComparer.Ordinal);
        var enElReadme = new HashSet<string>(["Ctrl+F", "Ctrl+P"], StringComparer.Ordinal);

        Assert.Equal(["Ctrl+P"], enElReadme.Except(enElCodigo).Order());
    }
}
