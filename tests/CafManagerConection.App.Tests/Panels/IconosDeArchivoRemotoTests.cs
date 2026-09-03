using CafManagerConection.App.Panels;

namespace CafManagerConection.App.Tests.Panels;

public sealed class IconosDeArchivoRemotoTests
{
    [Fact]
    public void Una_carpeta_es_carpeta_aunque_el_nombre_parezca_un_archivo()
    {
        Assert.Equal(
            TipoDeArchivoRemoto.Carpeta,
            IconosDeArchivoRemoto.Clasificar("backup.tar.gz", esCarpeta: true));
    }

    [Theory]
    [InlineData("notas.txt")]
    [InlineData("LEEME.md")]
    [InlineData("datos.CSV")]
    [InlineData("README")]
    public void Los_archivos_de_texto_se_reconocen(string nombre)
    {
        Assert.Equal(TipoDeArchivoRemoto.Texto, IconosDeArchivoRemoto.Clasificar(nombre, false));
    }

    [Theory]
    [InlineData("respaldo.zip")]
    [InlineData("fuentes.tar.gz")]
    [InlineData("fuentes.tgz")]
    [InlineData("volcado.sql.bz2")]
    [InlineData("paquete.deb")]
    public void Los_comprimidos_se_reconocen(string nombre)
    {
        Assert.Equal(
            TipoDeArchivoRemoto.Comprimido, IconosDeArchivoRemoto.Clasificar(nombre, false));
    }

    [Theory]
    [InlineData("arranque.sh")]
    [InlineData("instalar.ps1")]
    [InlineData("servidor.exe")]
    [InlineData("migrar.py")]
    public void Los_ejecutables_se_reconocen(string nombre)
    {
        Assert.Equal(
            TipoDeArchivoRemoto.Ejecutable, IconosDeArchivoRemoto.Clasificar(nombre, false));
    }

    [Theory]
    [InlineData("logo.png")]
    [InlineData("captura.JPEG")]
    [InlineData("diagrama.svg")]
    public void Las_imagenes_se_reconocen(string nombre)
    {
        Assert.Equal(TipoDeArchivoRemoto.Imagen, IconosDeArchivoRemoto.Clasificar(nombre, false));
    }

    [Theory]
    [InlineData("acceso.log")]
    [InlineData("salida.out")]
    [InlineData("error.err")]
    public void Los_registros_se_reconocen(string nombre)
    {
        Assert.Equal(TipoDeArchivoRemoto.Registro, IconosDeArchivoRemoto.Clasificar(nombre, false));
    }

    // La rotación de logrotate deja syslog.1, syslog.2.gz: el tipo lo decide el nombre sin el
    // número, no el número.
    [Theory]
    [InlineData("syslog.1")]
    [InlineData("acceso.log.7")]
    public void Un_registro_rotado_sigue_siendo_un_registro(string nombre)
    {
        Assert.Equal(TipoDeArchivoRemoto.Registro, IconosDeArchivoRemoto.Clasificar(nombre, false));
    }

    [Theory]
    [InlineData("nginx.conf")]
    [InlineData("compose.yml")]
    [InlineData("ajustes.JSON")]
    [InlineData("pom.xml")]
    [InlineData("app.ini")]
    public void Las_configuraciones_se_reconocen(string nombre)
    {
        Assert.Equal(
            TipoDeArchivoRemoto.Configuracion, IconosDeArchivoRemoto.Clasificar(nombre, false));
    }

    [Theory]
    [InlineData("volcado.bin")]
    [InlineData("sin-extension-conocida.xyz")]
    public void Lo_que_no_encaja_cae_en_el_generico(string nombre)
    {
        Assert.Equal(TipoDeArchivoRemoto.Generico, IconosDeArchivoRemoto.Clasificar(nombre, false));
    }

    [Fact]
    public void Cada_tipo_tiene_su_propio_icono_y_su_propio_color()
    {
        var tipos = Enum.GetValues<TipoDeArchivoRemoto>();

        var iconos = tipos.Select(IconosDeArchivoRemoto.ClaveDeIcono).ToList();
        var pinceles = tipos.Select(IconosDeArchivoRemoto.ClaveDePincel).ToList();

        Assert.All(iconos, i => Assert.StartsWith("Icono", i, StringComparison.Ordinal));
        Assert.All(pinceles, p => Assert.StartsWith("Icono", p, StringComparison.Ordinal));
        Assert.Equal(tipos.Length, pinceles.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(tipos.Length, iconos.Distinct(StringComparer.Ordinal).Count());
    }
}
