using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using WF = System.Windows.Forms;

namespace CafManagerConection.Rdp.Tests;

[Trait("Categoria", "RdpActivacion")]
public sealed class HospedajeEnWpfTests
{
    /// <summary>Dirección de documentación (RFC 5737): no enruta a ninguna parte.</summary>
    private const string DestinoInalcanzable = "192.0.2.1";

    public enum Montaje
    {
        Directo,
        EnContenedor,
        EnContenedorConFoco,
        EnFormulario,
    }

    [Theory]
    [InlineData(Montaje.Directo)]
    [InlineData(Montaje.EnContenedor)]
    [InlineData(Montaje.EnContenedorConFoco)]
    [InlineData(Montaje.EnFormulario)]
    public void Que_montajes_hacen_arrancar_el_control(Montaje montaje)
    {
        if (!RdpClientHost.IsAvailable)
        {
            Assert.Fail("El control ActiveX de Escritorio remoto no está en este equipo.");
        }

        var (arranco, detalle) = EnSta(() => Probar(montaje, segundos: 10));

        Assert.True(
            arranco,
            $"El montaje «{montaje}» no hace arrancar el control: Connected nunca dejó de "
            + $"valer 0. {detalle}");
    }

    private static (bool Arranco, string Detalle) Probar(Montaje montaje, int segundos)
    {
        var ventana = new Window
        {
            Width = 900,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -3000,
            Top = -3000,
            ShowInTaskbar = false,
        };

        var host = new WindowsFormsHost();
        var control = new RdpClientHost { Dock = WF.DockStyle.Fill };

        WF.Control raiz = montaje switch
        {
            Montaje.Directo => control,

            Montaje.EnContenedor or Montaje.EnContenedorConFoco => Envolver(
                new WF.ContainerControl { Dock = WF.DockStyle.Fill }, control),

            Montaje.EnFormulario => Envolver(
                new WF.Form
                {
                    TopLevel = false,
                    FormBorderStyle = WF.FormBorderStyle.None,
                    Dock = WF.DockStyle.Fill,
                },
                control),

            _ => control,
        };

        host.Child = raiz;
        ventana.Content = host;

        short ultimo = -1;
        string extra = string.Empty;

        try
        {
            ventana.Show();
            Bombear(ventana);

            if (raiz is WF.Form formulario)
            {
                formulario.Show();
            }

            control.CreateControl();

            if (montaje == Montaje.EnContenedorConFoco && raiz is WF.ContainerControl cc)
            {
                cc.ActiveControl = control;
                control.Focus();
            }

            control.Set("Server", DestinoInalcanzable);
            control.Set("UserName", "prueba");

            var avanzado = control.GetObject("AdvancedSettings9")
                           ?? control.GetObject("AdvancedSettings8")
                           ?? control.GetObject("AdvancedSettings2");

            if (avanzado is not null)
            {
                RdpClientHost.TrySetOn(avanzado, "RDPPort", 3389);
                RdpClientHost.TrySetOn(avanzado, "overallConnectionTimeout", segundos);
            }

            control.Invoke("Connect");

            var limite = DateTime.UtcNow.AddSeconds(segundos);

            while (DateTime.UtcNow < limite)
            {
                Bombear(ventana);
                Thread.Sleep(50);

                ultimo = control.Get<short>("Connected");

                if (ultimo != 0)
                {
                    return (true, $"Connected llegó a {ultimo}.");
                }
            }
        }
        catch (Exception ex)
        {
            var raiz2 = ex.GetBaseException();
            extra = $" Excepción: {raiz2.GetType().Name}: {raiz2.Message}";
        }
        finally
        {
            try
            {
                control.Invoke("Disconnect");
            }
            catch (Exception)
            {
            }

            control.Dispose();
            ventana.Close();
        }

        return (false, $"Último Connected leído: {ultimo}.{extra}");
    }

    private static WF.Control Envolver(WF.Control contenedor, WF.Control hijo)
    {
        contenedor.Controls.Add(hijo);
        return contenedor;
    }

    private static void Bombear(Window ventana) =>
        ventana.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);

    private static (bool, string) EnSta(Func<(bool, string)> accion)
    {
        (bool, string) resultado = (false, "sin resultado");
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
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();

        if (!hilo.Join(TimeSpan.FromSeconds(60)))
        {
            return (false, "El hilo STA no terminó en 60 segundos.");
        }

        return fallo is null
            ? resultado
            : (false, $"{fallo.GetType().Name}: {fallo.Message}");
    }
}
