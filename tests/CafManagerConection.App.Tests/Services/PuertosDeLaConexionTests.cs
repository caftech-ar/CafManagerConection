using CafManagerConection.App.Services;
using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.App.Tests.Services;

public sealed class PuertosDeLaConexionTests
{
    private static ConnectionSummary Conexion(
        Protocol protocolo = Protocol.Ssh, int puerto = 22, string host = "servidor") =>
        new(Guid.NewGuid(), null, "prueba", protocolo, host, puerto, null, null, 0);

    private static SshTunnel Tunel(string nombre, string host, int remoto) =>
        new(Guid.NewGuid(), Guid.NewGuid(), nombre, 15000, host, remoto);

    [Fact]
    public void Una_conexion_ssh_sola_aporta_su_puerto()
    {
        var puertos = PuertosDeLaConexion.De(Conexion());

        Assert.Equal(22, Assert.Single(puertos).Puerto);
    }

    [Fact]
    public void Los_tuneles_aportan_su_extremo_remoto()
    {
        var puertos = PuertosDeLaConexion.De(
            Conexion(),
            tuneles: [Tunel("base", "interno", 5432), Tunel("panel", "interno", 8080)]);

        Assert.Equal(3, puertos.Count);
        Assert.Contains(puertos, p => p.Puerto == 5432 && p.Host == "interno");
        Assert.Contains(puertos, p => p.Puerto == 8080);
    }

    [Fact]
    public void Una_entrada_web_aporta_el_puerto_de_su_direccion()
    {
        var conexion = Conexion(Protocol.Web, 443, "servidor");

        var detalle = new ConnectionRecord(
            new Connection(conexion.Id, "prueba", Protocol.Web, "servidor"),
            Web: new WebSettings { Url = "https://servidor:8443/panel" });

        var puertos = PuertosDeLaConexion.De(conexion, detalle);

        Assert.Equal(8443, Assert.Single(puertos).Puerto);
    }

    [Fact]
    public void Una_direccion_sin_puerto_toma_el_del_esquema()
    {
        var conexion = Conexion(Protocol.Web, 443, "servidor");

        var detalle = new ConnectionRecord(
            new Connection(conexion.Id, "prueba", Protocol.Web, "servidor"),
            Web: new WebSettings { Url = "https://servidor/panel" });

        Assert.Equal(443, Assert.Single(PuertosDeLaConexion.De(conexion, detalle)).Puerto);
    }

    [Fact]
    public void No_se_repite_un_puerto_del_mismo_host()
    {
        var puertos = PuertosDeLaConexion.De(
            Conexion(),
            tuneles: [Tunel("uno", "servidor", 22), Tunel("otro", "servidor", 22)]);

        Assert.Single(puertos);
    }

    [Fact]
    public void Un_puerto_fuera_de_rango_no_entra()
    {
        var puertos = PuertosDeLaConexion.De(Conexion(puerto: 0));

        Assert.Empty(puertos);
    }

    [Fact]
    public void Cada_puerto_dice_de_donde_salio()
    {
        var puertos = PuertosDeLaConexion.De(
            Conexion(), tuneles: [Tunel("base", "interno", 5432)]);

        Assert.Contains(puertos, p => p.Descripcion.Contains("SSH", StringComparison.Ordinal));
        Assert.Contains(puertos, p => p.Descripcion.Contains("base", StringComparison.Ordinal));
    }

    [Fact]
    public void No_se_inventan_puertos_que_nadie_configuro()
    {
        var puertos = PuertosDeLaConexion.De(
            Conexion(), tuneles: [Tunel("base", "interno", 5432)]);

        Assert.Equal(2, puertos.Count);
    }
}
