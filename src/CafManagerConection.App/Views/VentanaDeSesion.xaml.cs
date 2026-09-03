using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;

namespace CafManagerConection.App.Views;

/// <summary>Ventana propia de una sesión RDP; cerrarla devuelve la sesión a su pestaña en lugar de cortarla (FR-187).</summary>
[SupportedOSPlatform("windows")]
public partial class VentanaDeSesion : Window
{
    private System.Windows.Forms.Control? _contenido;
    private bool _devuelveAlCerrar = true;

    public VentanaDeSesion(string nombre)
    {
        InitializeComponent();

        Title = $"{nombre} — Escritorio remoto";
    }

    /// <summary>La ventana ya soltó el control y la sesión puede volver a su pestaña.</summary>
    public event EventHandler? Devolvio;

    /// <summary>Se aloja después de mostrar la ventana: el <c>WindowsFormsHost</c> necesita su propia ventana antes de recibir el control.</summary>
    public void Alojar(System.Windows.Forms.Control contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        _contenido = contenido;
        _host.Child = contenido;
        contenido.Focus();
    }

    /// <summary>Suelta el control y cierra sin devolver nada: la sesión se está desarmando de todos modos.</summary>
    public void SoltarYCerrar()
    {
        _devuelveAlCerrar = false;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        var devolver = _devuelveAlCerrar && _contenido is not null;

        // El control se suelta antes de que se destruya la ventana que lo aloja: si no, su HWND se va con ella.
        _host.Child = null;
        _contenido = null;

        if (devolver)
        {
            Devolvio?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _host.Dispose();
    }
}
