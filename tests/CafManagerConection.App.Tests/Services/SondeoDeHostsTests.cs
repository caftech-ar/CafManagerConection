using CafManagerConection.App.Services;
using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.App.Tests.Services;

public sealed class SondeoDeHostsTests
{
    private static ConnectionSummary Conexion(
        Protocol protocolo = Protocol.Ssh, string host = "servidor", int puerto = 22) =>
        new(Guid.NewGuid(), null, "prueba", protocolo, host, puerto, null, null, 0);

    [Theory]
    [InlineData(RespuestaIcmp.Responde, true, EstadoDeHost.Vivo)]
    [InlineData(RespuestaIcmp.NoResponde, true, EstadoDeHost.Vivo)]
    [InlineData(RespuestaIcmp.NoConcluyente, true, EstadoDeHost.Vivo)]
    [InlineData(RespuestaIcmp.Responde, false, EstadoDeHost.SinServicio)]
    [InlineData(RespuestaIcmp.NoResponde, false, EstadoDeHost.SinRespuesta)]
    [InlineData(RespuestaIcmp.SinResolver, false, EstadoDeHost.SinResolver)]
    public void Las_dos_pruebas_se_combinan_en_un_estado(
        RespuestaIcmp icmp, bool puerto, EstadoDeHost esperado) =>
        Assert.Equal(esperado, SondeoDeHosts.Combinar(icmp, puerto));

    [Fact]
    public void El_puerto_abierto_alcanza_aunque_no_pinguee() =>
        Assert.Equal(
            EstadoDeHost.Vivo, SondeoDeHosts.Combinar(RespuestaIcmp.NoResponde, puerto: true));

    [Fact]
    public void El_ping_solo_no_alcanza_para_decir_que_hay_servicio() =>
        Assert.Equal(
            EstadoDeHost.SinServicio, SondeoDeHosts.Combinar(RespuestaIcmp.Responde, puerto: false));

    [Fact]
    public void Un_icmp_no_concluyente_deja_decidir_al_puerto()
    {
        Assert.Equal(
            EstadoDeHost.Vivo, SondeoDeHosts.Combinar(RespuestaIcmp.NoConcluyente, puerto: true));

        Assert.Equal(
            EstadoDeHost.SinRespuesta,
            SondeoDeHosts.Combinar(RespuestaIcmp.NoConcluyente, puerto: false));
    }

    [Theory]
    [InlineData(Protocol.Ssh, true)]
    [InlineData(Protocol.Rdp, true)]
    [InlineData(Protocol.Web, false)]
    public void Las_conexiones_web_no_se_sondean(Protocol protocolo, bool sondeable) =>
        Assert.Equal(sondeable, SondeoDeHosts.SePuedeSondear(Conexion(protocolo)));

    [Fact]
    public void Sin_host_no_hay_nada_que_sondear() =>
        Assert.False(SondeoDeHosts.SePuedeSondear(Conexion(host: "  ")));

    [Fact]
    public async Task Una_conexion_web_devuelve_que_no_se_sondea()
    {
        var fila = await SondeoDeHosts.SondearAsync(Conexion(Protocol.Web));

        Assert.Equal(EstadoDeHost.NoSeSondea, fila.Estado);
        Assert.Null(fila.Icmp);
        Assert.Null(fila.Puerto);
    }

    [Fact]
    public async Task El_lote_informa_una_fila_por_conexion()
    {
        var conexiones = Enumerable.Range(0, 5).Select(_ => Conexion(Protocol.Web)).ToList();
        var llegaron = new List<FilaDeSondeo>();

        await SondeoDeHosts.SondearLoteAsync(conexiones, llegaron.Add);

        Assert.Equal(conexiones.Count, llegaron.Count);
    }

    [Fact]
    public async Task Un_nombre_que_no_resuelve_no_cuenta_como_caida()
    {
        var fila = await SondeoDeHosts.SondearAsync(
            Conexion(host: "no-existe.invalid"), TimeSpan.FromMilliseconds(400));

        Assert.Equal(EstadoDeHost.SinResolver, fila.Estado);
    }

    [Fact]
    public void Cada_estado_tiene_su_texto()
    {
        foreach (var estado in Enum.GetValues<EstadoDeHost>())
        {
            Assert.False(string.IsNullOrWhiteSpace(SondeoDeHosts.Titulo(estado)));
        }
    }

    [Fact]
    public void El_detalle_nombra_el_puerto_que_se_probo()
    {
        var fila = new FilaDeSondeo(
            Conexion(puerto: 2222), EstadoDeHost.SinServicio, RespuestaIcmp.Responde, false);

        Assert.Contains("2222", fila.Detalle, StringComparison.Ordinal);
        Assert.Contains("Responde al ping", fila.Detalle, StringComparison.Ordinal);
    }
}
