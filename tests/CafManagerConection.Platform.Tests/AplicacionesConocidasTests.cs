using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class AplicacionesConocidasTests
{
    [Theory]
    [InlineData("nginx", "nginx")]
    [InlineData("sshd", "OpenSSH")]
    [InlineData("dockerd", "Docker")]
    [InlineData("supervisord", "supervisord")]
    public void Reconoce_los_procesos_tal_como_vienen(string proceso, string esperado) =>
        Assert.Equal(esperado, AplicacionesConocidas.Reconocer(proceso)?.Nombre);

    [Theory]
    [InlineData("1234/nginx: master", "nginx")]
    [InlineData("python3.11", "Aplicación Python")]
    [InlineData("redis-server", "Redis")]
    [InlineData("mysqld", "MySQL")]
    public void Reconoce_los_nombres_con_sufijos_y_adornos(string proceso, string esperado) =>
        Assert.Equal(esperado, AplicacionesConocidas.Reconocer(proceso)?.Nombre);

    [Theory]
    [InlineData("postmaster", "PostgreSQL")]
    [InlineData("docker-proxy", "Docker (publicación de puerto)")]
    [InlineData("containerd", "containerd")]
    public void Gana_la_coincidencia_mas_larga(string proceso, string esperado) =>
        Assert.Equal(esperado, AplicacionesConocidas.Reconocer(proceso)?.Nombre);

    [Theory]
    [InlineData("NGINX", "nginx")]
    [InlineData("Postgres", "PostgreSQL")]
    public void No_distingue_mayusculas(string proceso, string esperado) =>
        Assert.Equal(esperado, AplicacionesConocidas.Reconocer(proceso)?.Nombre);

    [Theory]
    [InlineData("nginx", ClaseDeAplicacion.ServidorWeb)]
    [InlineData("postgres", ClaseDeAplicacion.BaseDeDatos)]
    [InlineData("mongod", ClaseDeAplicacion.BaseDeDatos)]
    [InlineData("dockerd", ClaseDeAplicacion.Contenedor)]
    [InlineData("sshd", ClaseDeAplicacion.AccesoRemoto)]
    [InlineData("supervisord", ClaseDeAplicacion.SupervisionDeProcesos)]
    [InlineData("gunicorn", ClaseDeAplicacion.Aplicacion)]
    [InlineData("rabbitmq-server", ClaseDeAplicacion.Mensajeria)]
    [InlineData("chronyd", ClaseDeAplicacion.ServicioDelSistema)]
    public void Clasifica_por_familia(string proceso, ClaseDeAplicacion esperada) =>
        Assert.Equal(esperada, AplicacionesConocidas.Reconocer(proceso)?.Clase);

    [Theory]
    [InlineData("un-binario-propio")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Lo_que_no_conoce_no_lo_inventa(string? proceso) =>
        Assert.Null(AplicacionesConocidas.Reconocer(proceso));

    [Fact]
    public void El_texto_de_falta_de_permiso_no_se_confunde_con_una_aplicacion() =>
        Assert.Null(AplicacionesConocidas.Reconocer("(sin permiso para verlo)"));
}
