using System.Runtime.InteropServices;
using System.Windows.Forms;
using CafManagerConection.Rdp;

namespace CafManagerConection.Rdp.Tests;

[Trait("Categoria", "RdpActivacion")]
public sealed class DesmontajeDeSesionTests
{
    /// <summary>Dirección de documentación (RFC 5737): no enruta a ninguna parte.</summary>
    private const string DestinoInalcanzable = "192.0.2.1";

    private static RdpSessionRequest Pedido() => new(
        ConnectionId: Guid.NewGuid(),
        Host: DestinoInalcanzable,
        Port: 3389,
        UserName: "prueba",
        Domain: null,
        ClipboardEnabled: false,
        FitToTab: true,
        IgnoreCertificateWarnings: true,
        TimeoutSeconds: 5);

    [Fact]
    public void Desmontar_una_sesion_conectando_no_lanza()
    {
        if (!RdpSession.IsAvailable)
        {
            Assert.Fail("El control ActiveX de Escritorio remoto no está en este equipo.");
        }

        var fallo = EnSta(() =>
        {
            using var ventana = Ventana();
            var sesion = new RdpSession(Pedido());

            var control = sesion.CrearControl(null);
            Assert.NotNull(control);

            ventana.Controls.Add(control);
            ventana.Show();
            Application.DoEvents();

            sesion.PrepararYConectar(null);

            for (var i = 0; i < 8; i++)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            return Capturar(sesion.Dispose);
        });

        Assert.Null(fallo);
    }

    [Fact]
    public void Desmontar_despues_de_destruir_la_ventana_no_lanza()
    {
        if (!RdpSession.IsAvailable)
        {
            Assert.Fail("El control ActiveX de Escritorio remoto no está en este equipo.");
        }

        var fallo = EnSta(() =>
        {
            var ventana = Ventana();
            var sesion = new RdpSession(Pedido());

            var control = sesion.CrearControl(null);
            ventana.Controls.Add(control!);
            ventana.Show();
            Application.DoEvents();

            sesion.PrepararYConectar(null);

            for (var i = 0; i < 8; i++)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            ventana.Dispose();
            Application.DoEvents();

            return Capturar(sesion.Dispose);
        });

        Assert.Null(fallo);
    }

    [Fact]
    public void Desmontar_dos_veces_no_lanza()
    {
        if (!RdpSession.IsAvailable)
        {
            Assert.Fail("El control ActiveX de Escritorio remoto no está en este equipo.");
        }

        var fallo = EnSta(() =>
        {
            using var ventana = Ventana();
            var sesion = new RdpSession(Pedido());

            ventana.Controls.Add(sesion.CrearControl(null)!);
            ventana.Show();
            Application.DoEvents();

            sesion.PrepararYConectar(null);
            Application.DoEvents();

            sesion.Dispose();
            return Capturar(sesion.Dispose);
        });

        Assert.Null(fallo);
    }

    private static Form Ventana() => new()
    {
        Width = 800,
        Height = 600,
        StartPosition = FormStartPosition.Manual,
        Location = new System.Drawing.Point(-3000, -3000),
        ShowInTaskbar = false,
    };

    private static string? Capturar(Action accion)
    {
        try
        {
            accion();
            return null;
        }
        catch (COMException ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }
        catch (InvalidComObjectException ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string? EnSta(Func<string?> accion)
    {
        string? resultado = null;
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

        Assert.True(hilo.Join(TimeSpan.FromSeconds(60)), "El hilo STA no terminó en 60 segundos.");

        if (fallo is not null)
        {
            throw new Xunit.Sdk.XunitException($"{fallo.GetType().Name}: {fallo.Message}");
        }

        return resultado;
    }
}
