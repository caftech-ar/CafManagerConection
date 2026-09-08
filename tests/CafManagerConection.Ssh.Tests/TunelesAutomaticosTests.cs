using System.Net;
using System.Net.Sockets;
using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Ssh.Tests;

// La version anterior vivia en SessionView con un solo try alrededor del foreach: el primer tunel
// que fallaba se llevaba puestos a los que venian atras, y el usuario no se enteraba de ninguno.
public sealed class TunelesAutomaticosTests
{
    [Fact]
    public async Task Un_tunel_que_falla_no_impide_levantar_los_demas()
    {
        using var primero = PuertoTomado();
        using var segundo = PuertoTomado();
        using var tercero = PuertoTomado();

        await using var anfitrion = Anfitrion();

        var errores = await anfitrion.StartAutoAsync(
        [
            Automatico("base", Puerto(primero)),
            Automatico("panel", Puerto(segundo)),
            Automatico("metricas", Puerto(tercero)),
        ]);

        Assert.Equal(3, errores.Count);
        Assert.Contains(errores, e => e.StartsWith("base:", StringComparison.Ordinal));
        Assert.Contains(errores, e => e.StartsWith("panel:", StringComparison.Ordinal));
        Assert.Contains(errores, e => e.StartsWith("metricas:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task El_error_nombra_al_tunel_y_dice_por_que()
    {
        using var tomado = PuertoTomado();

        await using var anfitrion = Anfitrion();

        var errores = await anfitrion.StartAutoAsync([Automatico("base", Puerto(tomado))]);

        var error = Assert.Single(errores);
        Assert.StartsWith("base:", error, StringComparison.Ordinal);
        Assert.Contains(Puerto(tomado).ToString(), error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_que_no_estan_marcados_no_se_tocan()
    {
        using var delAutomatico = PuertoTomado();
        using var delManual = PuertoTomado();

        await using var anfitrion = Anfitrion();

        var manual = Automatico("manual", Puerto(delManual));
        manual.AutoStart = false;

        var errores = await anfitrion.StartAutoAsync(
            [Automatico("automatico", Puerto(delAutomatico)), manual]);

        var error = Assert.Single(errores);
        Assert.StartsWith("automatico:", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_ninguno_marcado_no_se_informa_nada()
    {
        using var tomado = PuertoTomado();

        await using var anfitrion = Anfitrion();

        var manual = Automatico("manual", Puerto(tomado));
        manual.AutoStart = false;

        Assert.Empty(await anfitrion.StartAutoAsync([manual]));
    }

    private static TunnelHost Anfitrion() => new(
        new SshSessionRequest(
            Guid.NewGuid(),
            "192.0.2.1",
            22,
            "prueba",
            SshAuthMethod.Password,
            null,
            null,
            0,
            80,
            24,
            5),
        new ServidorDePrueba.AceptaTodo(),
        null);

    private static SshTunnel Automatico(string nombre, int puertoLocal) =>
        new(Guid.NewGuid(), Guid.NewGuid(), nombre, puertoLocal, "localhost", 5432)
        {
            AutoStart = true,
        };

    // Se ocupa el puerto de verdad: es lo que hace fallar a StartAsync antes de tocar la red, y por
    // eso estas pruebas no necesitan servidor.
    private static TcpListener PuertoTomado()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return listener;
    }

    private static int Puerto(TcpListener listener) =>
        ((IPEndPoint)listener.LocalEndpoint).Port;
}
