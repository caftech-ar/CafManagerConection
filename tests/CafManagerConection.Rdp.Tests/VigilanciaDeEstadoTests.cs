using System.Windows.Forms;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.Rdp;

namespace CafManagerConection.Rdp.Tests;

[Trait("Categoria", "RdpActivacion")]
public sealed class VigilanciaDeEstadoTests
{
    /// <summary>Dirección de documentación (RFC 5737): no enruta a ninguna parte.</summary>
    private const string DestinoInalcanzable = "192.0.2.1";

    private const string DestinoQueRechaza = "127.0.0.1";

    private const int PuertoSinNadieEscuchando = 59999;

    private static RdpSessionRequest Pedido(
        int timeoutSeconds,
        string host = DestinoInalcanzable,
        int puerto = 3389) => new(
        ConnectionId: Guid.NewGuid(),
        Host: host,
        Port: puerto,
        UserName: "prueba",
        Domain: null,
        ClipboardEnabled: false,
        FitToTab: true,
        IgnoreCertificateWarnings: true,
        TimeoutSeconds: timeoutSeconds);

    // Medido contra este control: apuntando a una dirección que no enruta, Connected vale 2 al
    // segundo y medio y cae a 0 a los diecisiete; el 2 es la negociación, no el éxito.
    [Fact]
    public void Un_servidor_que_no_contesta_termina_en_error_y_no_en_conectada()
    {
        if (!RdpSession.IsAvailable)
        {
            Assert.Fail("El control ActiveX de Escritorio remoto no está en este equipo.");
        }

        var resultado = EnSta(() =>
        {
            using var ventana = Ventana();
            var sesion = new RdpSession(Pedido(timeoutSeconds: 3));

            var control = sesion.CrearControl(null);
            Assert.NotNull(control);

            ventana.Controls.Add(control);
            ventana.Show();
            Application.DoEvents();

            sesion.PrepararYConectar(null);

            // El plazo real es max(TimeoutSeconds, 5) = 5 s. Se bombea bastante más para darle
            // lugar a que la vigilancia lo note.
            var limite = DateTime.UtcNow.AddSeconds(14);

            while (sesion.State is SessionState.Connecting && DateTime.UtcNow < limite)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            return (sesion.State, sesion.Failure);
        });

        Assert.Equal(SessionState.Error, resultado.State);
        Assert.NotNull(resultado.Failure);
    }

    [Fact]
    public void Un_rechazo_despues_de_haber_arrancado_se_informa()
    {
        if (!RdpSession.IsAvailable)
        {
            Assert.Fail("El control ActiveX de Escritorio remoto no está en este equipo.");
        }

        var resultado = EnSta(() =>
        {
            using var ventana = Ventana();

            var sesion = new RdpSession(Pedido(
                timeoutSeconds: 90,
                host: DestinoQueRechaza,
                puerto: PuertoSinNadieEscuchando));

            var control = sesion.CrearControl(null);
            Assert.NotNull(control);

            var host = (RdpClientHost)control;
            ventana.Controls.Add(control);
            ventana.Show();
            Application.DoEvents();

            sesion.PrepararYConectar(null);

            var limiteArranque = DateTime.UtcNow.AddSeconds(8);

            while (host.Get<short>("Connected") == 0 && DateTime.UtcNow < limiteArranque)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            var limiteRechazo = DateTime.UtcNow.AddSeconds(30);

            while (sesion.State is SessionState.Connecting or SessionState.Connected
                   && DateTime.UtcNow < limiteRechazo)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            return (sesion.State, sesion.Failure);
        });

        Assert.Equal(SessionState.Error, resultado.State);
        Assert.NotNull(resultado.Failure);
    }

    private static Form Ventana() => new()
    {
        Width = 800,
        Height = 600,
        StartPosition = FormStartPosition.Manual,
        Location = new System.Drawing.Point(-3000, -3000),
        ShowInTaskbar = false,
    };

    private static T EnSta<T>(Func<T> accion)
    {
        T? resultado = default;
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                resultado = accion();
            }
            catch (Exception ex)
            {
                fallo = ex;
            }
        });

        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();

        Assert.True(hilo.Join(TimeSpan.FromSeconds(120)), "El hilo STA no terminó en 120 segundos.");

        if (fallo is not null)
        {
            throw new Xunit.Sdk.XunitException($"{fallo.GetType().Name}: {fallo.Message}");
        }

        return resultado!;
    }
}
