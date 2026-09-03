using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CafManagerConection.Rdp.Tests;

// Medido: el control filtra 3 descriptores de USER por Connect, lineal a 50 y 200 vueltas
// (42→192, 42→642), sin relación con Disconnect/RequestClose; es de mstscax.dll. Con el
// límite de Windows de 10.000 por proceso son ~3.300 conexiones antes de fallar.
[Trait("Categoria", "RdpLifecycle")]
public sealed class CicloDeVidaTests
{
    /// <summary>Dirección de documentación (RFC 5737): no enruta a ninguna parte.</summary>
    private const string DestinoInalcanzable = "192.0.2.1";

    private const int Calentamiento = 5;

    private const int Vueltas = 50;

    // Medido: 3 por sesión, del propio control; margen hasta 5 para absorber ruido.
    private const double PresupuestoUsuarioPorSesion = 5;

    private const double PresupuestoGdiPorSesion = 0.5;

    [Fact]
    public void Abrir_y_cerrar_cincuenta_sesiones_no_deja_recursos_colgados()
    {
        if (!RdpClientHost.IsAvailable)
        {
            Assert.Fail("El control ActiveX de Escritorio remoto no está en este equipo.");
        }

        var informe = EnSta(Medicion);

        Assert.True(
            informe.UsuarioPorSesion <= PresupuestoUsuarioPorSesion,
            $"Cada sesión deja {informe.UsuarioPorSesion:F1} descriptores de USER "
            + $"({informe.UsuarioBase} → {informe.UsuarioFinal} en {Vueltas} sesiones), por "
            + $"encima de los {PresupuestoUsuarioPorSesion} presupuestados. El control filtra "
            + "tres por sí solo; más que eso significa que el desmontaje se rompió.");

        Assert.True(
            informe.GdiPorSesion <= PresupuestoGdiPorSesion,
            $"Cada sesión deja {informe.GdiPorSesion:F1} descriptores de GDI "
            + $"({informe.GdiBase} → {informe.GdiFinal} en {Vueltas} sesiones). Los de GDI "
            + "tienen que quedar planos: hay objetos de dibujo sin liberar.");
    }

    private static Informe Medicion()
    {
        using var ventana = new Form
        {
            Width = 800,
            Height = 600,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-3000, -3000),
            ShowInTaskbar = false,
        };

        ventana.Show();
        Application.DoEvents();

        for (var i = 0; i < Calentamiento; i++)
        {
            UnaVuelta(ventana);
        }

        var (usuarioBase, gdiBase) = Medir();

        for (var i = 0; i < Vueltas; i++)
        {
            UnaVuelta(ventana);
        }

        var (usuarioFinal, gdiFinal) = Medir();

        return new Informe(usuarioBase, usuarioFinal, gdiBase, gdiFinal);
    }

    private static void UnaVuelta(Form ventana)
    {
        var host = new RdpClientHost { Dock = DockStyle.Fill };
        ventana.Controls.Add(host);

        try
        {
            host.CreateControl();
            host.Set("Server", DestinoInalcanzable);
            host.Set("UserName", "prueba");
            host.Invoke("Connect");

            for (var i = 0; i < 4; i++)
            {
                Application.DoEvents();
            }
        }
        catch (Exception)
        {
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

            host.Dispose();
            Application.DoEvents();
        }
    }

    private sealed record Informe(int UsuarioBase, int UsuarioFinal, int GdiBase, int GdiFinal)
    {
        public double UsuarioPorSesion => (double)(UsuarioFinal - UsuarioBase) / Vueltas;

        public double GdiPorSesion => (double)(GdiFinal - GdiBase) / Vueltas;
    }

    private static (int Usuario, int Gdi) Medir()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Application.DoEvents();

        var proceso = Process.GetCurrentProcess().Handle;
        return (GetGuiResources(proceso, RecursosDeUsuario), GetGuiResources(proceso, RecursosGdi));
    }

    private const uint RecursosGdi = 0;
    private const uint RecursosDeUsuario = 1;

    [DllImport("user32.dll")]
    private static extern int GetGuiResources(IntPtr proceso, uint bandera);

    private static Informe EnSta(Func<Informe> accion)
    {
        Informe? resultado = null;
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

        Assert.True(hilo.Join(TimeSpan.FromMinutes(3)), "El hilo STA no terminó en 3 minutos.");

        if (fallo is not null)
        {
            throw new Xunit.Sdk.XunitException($"{fallo.GetType().Name}: {fallo.Message}");
        }

        return resultado ?? throw new Xunit.Sdk.XunitException("Sin resultado.");
    }
}
