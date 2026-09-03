using System.Net;
using System.Net.Sockets;
using CafManagerConection.App.Services;

namespace CafManagerConection.App.Tests.Services;

public sealed class SondaDePuertoTests
{
    [Fact]
    public async Task Un_puerto_con_alguien_escuchando_responde()
    {
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();

        try
        {
            var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;

            Assert.True(await SondaDePuerto.RespondeAsync(puerto));
        }
        finally
        {
            escucha.Stop();
        }
    }

    [Fact]
    public async Task Un_puerto_sin_nadie_no_responde()
    {
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();

        Assert.False(await SondaDePuerto.RespondeAsync(puerto, TimeSpan.FromMilliseconds(300)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(70000)]
    public async Task Un_puerto_imposible_no_responde_y_no_lanza(int puerto)
    {
        Assert.False(await SondaDePuerto.RespondeAsync(puerto));
    }

    [Fact]
    public async Task Una_cancelacion_de_afuera_devuelve_que_no_responde()
    {
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();

        try
        {
            var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;

            Assert.False(await SondaDePuerto.RespondeAsync(puerto, ct: cancelado.Token));
        }
        finally
        {
            escucha.Stop();
        }
    }
}
