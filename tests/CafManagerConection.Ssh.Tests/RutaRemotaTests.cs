using CafManagerConection.Ssh;

namespace CafManagerConection.Ssh.Tests;

public sealed class RutaRemotaTests
{
    [Theory]
    [InlineData("/", "etc", "/etc")]
    [InlineData("/var", "log", "/var/log")]
    [InlineData("/var/", "log", "/var/log")]
    [InlineData("/var/log", "syslog.1", "/var/log/syslog.1")]
    public void Combinar_no_duplica_ni_pierde_la_barra(string carpeta, string nombre, string esperado)
    {
        Assert.Equal(esperado, RutaRemota.Combinar(carpeta, nombre));
    }

    [Theory]
    [InlineData("/var/log/syslog", "/var/log")]
    [InlineData("/var/log", "/var")]
    [InlineData("/var", "/")]
    [InlineData("/", "/")]
    [InlineData("/var/log/", "/var")]
    public void Padre_sube_un_nivel_y_se_detiene_en_la_raiz(string ruta, string esperado)
    {
        Assert.Equal(esperado, RutaRemota.Padre(ruta));
    }

    [Theory]
    [InlineData("/var/log/syslog", "syslog")]
    [InlineData("/var/log/", "log")]
    [InlineData("/var", "var")]
    [InlineData("/", "/")]
    public void Nombre_devuelve_el_ultimo_segmento(string ruta, string esperado)
    {
        Assert.Equal(esperado, RutaRemota.Nombre(ruta));
    }

    [Fact]
    public void La_ruta_de_un_archivo_de_Windows_se_traduce_a_la_del_servidor()
    {
        Assert.Equal(
            "/srv/app/config/base.yml",
            RutaRemota.Combinar("/srv/app", "config/base.yml"));
    }

    [Fact]
    public void Una_ruta_local_relativa_se_convierte_en_segmentos_remotos()
    {
        Assert.Equal(
            "/srv/app/config/base.yml",
            RutaRemota.Combinar("/srv/app", RutaRemota.DesdeRutaLocal(@"config\base.yml")));
    }
}
