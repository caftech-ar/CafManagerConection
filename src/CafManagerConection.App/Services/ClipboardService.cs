using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using CafManagerConection.UseCases.Abstractions;
using DomainDefaults = CafManagerConection.Domain.Settings.Defaults;

namespace CafManagerConection.App.Services;

/// <summary>Copia al portapapeles, con borrado diferido para los secretos (FR-121 a FR-124).</summary>
[SupportedOSPlatform("windows")]
public sealed class ClipboardService : IClipboardService, IDisposable
{
    private DispatcherTimer? _reloj;
    private string? _pendiente;

    /// <summary>Avisa qué se copió, para que la interfaz lo informe en la barra de estado.</summary>
    public event EventHandler<string>? Copied;

    public void CopyText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (Escribir(text))
        {
            Copied?.Invoke(this, "Copiado al portapapeles");
        }
    }

    public void CopySecret(string secret)
    {
        if (string.IsNullOrEmpty(secret) || !Escribir(secret))
        {
            return;
        }

        _pendiente = secret;

        _reloj?.Stop();

        _reloj = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(DomainDefaults.ClipboardClearSeconds),
        };

        _reloj.Tick += (_, _) =>
        {
            _reloj?.Stop();
            Limpiar();
        };

        _reloj.Start();

        Copied?.Invoke(
            this,
            $"Contraseña copiada · se borra en {DomainDefaults.ClipboardClearSeconds} s");
    }

    /// <summary>Borra el secreto sólo si sigue siendo lo último que se copió.</summary>
    private void Limpiar()
    {
        if (_pendiente is null)
        {
            return;
        }

        try
        {
            if (Clipboard.ContainsText() && Clipboard.GetText() == _pendiente)
            {
                Clipboard.Clear();
                Copied?.Invoke(this, "Portapapeles limpiado");
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            _pendiente = null;
        }
    }

    /// <summary>Escribe en el portapapeles tolerando que otro proceso lo tenga tomado.</summary>
    private static bool Escribir(string texto)
    {
        for (var intento = 0; intento < 2; intento++)
        {
            try
            {
                Clipboard.SetText(texto);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(60);
            }
        }

        return false;
    }

    public void Dispose()
    {
        _reloj?.Stop();
        _reloj = null;

        Limpiar();
    }
}
