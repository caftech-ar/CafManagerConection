using System.Windows.Forms;
using CafManagerConection.Rdp;

namespace CafManagerConection.Rdp.Tests;

[Trait("Categoria", "RdpActivacion")]
public sealed class ActivacionDelControlTests
{
    /// <summary>Dirección de documentación (RFC 5737): no enruta a ninguna parte.</summary>
    private const string DestinoInalcanzable = "192.0.2.1";

    private sealed record Resultado(bool Arranco, short UltimoEstado, string? Error);

    [Fact]
    public void El_control_arranca_al_pedirle_conectar()
    {
        if (!RdpClientHost.IsAvailable)
        {
            Assert.Fail(
                "El control ActiveX de Escritorio remoto no está disponible en este equipo. "
                + "Sin él, RDP no puede funcionar y el problema no está en la aplicación.");
        }

        var r = EjecutarEnSta(() => Intentar(segundos: 10));

        Assert.True(
            r.Arranco,
            "El control aceptó la configuración y la llamada a Connect, pero su propiedad "
            + $"Connected nunca dejó de valer 0 (último valor leído: {r.UltimoEstado}). "
            + "Eso significa que no llega a intentar la conexión: el problema está en la "
            + $"activación del control, no en el servidor ni en las credenciales. {r.Error}");
    }

    private static Resultado Intentar(int segundos)
    {
        using var ventana = new Form
        {
            Width = 800,
            Height = 600,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-3000, -3000),
            ShowInTaskbar = false,
        };

        var host = new RdpClientHost { Dock = DockStyle.Fill };
        ventana.Controls.Add(host);

        short ultimo = -1;
        string? error = null;
        var arranco = false;

        try
        {
            ventana.Show();
            Application.DoEvents();

            host.CreateControl();
            host.Set("Server", DestinoInalcanzable);
            host.Set("UserName", "prueba");

            var avanzado = host.GetObject("AdvancedSettings9")
                           ?? host.GetObject("AdvancedSettings8")
                           ?? host.GetObject("AdvancedSettings2");

            if (avanzado is not null)
            {
                RdpClientHost.TrySetOn(avanzado, "RDPPort", 3389);
                RdpClientHost.TrySetOn(avanzado, "overallConnectionTimeout", segundos);
            }

            host.Invoke("Connect");

            var limite = DateTime.UtcNow.AddSeconds(segundos);

            while (DateTime.UtcNow < limite)
            {
                Application.DoEvents();
                Thread.Sleep(50);

                ultimo = host.Get<short>("Connected");

                if (ultimo != 0)
                {
                    arranco = true;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            error = $"Excepción: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            try
            {
                host.Invoke("Disconnect");
            }
            catch (Exception)
            {
            }

            ventana.Controls.Remove(host);
            host.Dispose();
        }

        return new Resultado(arranco, ultimo, error);
    }

    private static Resultado EjecutarEnSta(Func<Resultado> accion)
    {
        Resultado? resultado = null;
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

        if (!hilo.Join(TimeSpan.FromSeconds(60)))
        {
            return new Resultado(false, -1, "El hilo STA no terminó en 60 segundos.");
        }

        return fallo is not null
            ? new Resultado(false, -1, $"{fallo.GetType().Name}: {fallo.Message}")
            : resultado ?? new Resultado(false, -1, "Sin resultado.");
    }
}
