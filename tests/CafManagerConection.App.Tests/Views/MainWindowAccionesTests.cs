using CafManagerConection.App.Views;
using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.App.Tests.Views;

public sealed class MainWindowAccionesTests
{
    private static Connection Conexion() => new(Guid.NewGuid(), "Panel", Protocol.Web, "panel.ejemplo.com");

    [Fact]
    public void Con_entrada_web_copia_la_url_completa_y_no_solo_el_host()
    {
        var conexion = Conexion();
        var web = new WebSettings
        {
            ConnectionId = conexion.Id,
            Url = "https://panel.ejemplo.com/admin/dashboard?tab=2",
        };
        var registro = new ConnectionRecord(conexion, Web: web);

        var resultado = MainWindow.DireccionParaCopiar(registro, conexion.Host);

        Assert.Equal("https://panel.ejemplo.com/admin/dashboard?tab=2", resultado);
    }

    [Fact]
    public void Sin_detalle_disponible_recae_en_el_host()
    {
        var resultado = MainWindow.DireccionParaCopiar(null, "panel.ejemplo.com");

        Assert.Equal("panel.ejemplo.com", resultado);
    }

    [Fact]
    public void Con_url_vacia_tambien_recae_en_el_host()
    {
        var conexion = Conexion();
        var web = new WebSettings { ConnectionId = conexion.Id, Url = string.Empty };
        var registro = new ConnectionRecord(conexion, Web: web);

        var resultado = MainWindow.DireccionParaCopiar(registro, conexion.Host);

        Assert.Equal(conexion.Host, resultado);
    }
}
