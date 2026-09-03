using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using CafManagerConection.Infrastructure;

namespace CafManagerConection.App.Services;

/// <summary>Abre una conexión SSH en una herramienta de terceros, como proceso externo (FR-143).</summary>
[SupportedOSPlatform("windows")]
public static class LanzadorExterno
{
    public static string Nombre(HerramientaExterna herramienta) => herramienta switch
    {
        HerramientaExterna.Putty => "PuTTY",
        HerramientaExterna.FileZilla => "FileZilla",
        HerramientaExterna.WinScp => "WinSCP",
        _ => herramienta.ToString(),
    };

    /// <summary>Lanza la herramienta. Devuelve el error a mostrar, o null si salió bien.</summary>
    public static string? Abrir(
        HerramientaExterna herramienta, string ejecutable, DestinoRemoto destino)
    {
        ArgumentNullException.ThrowIfNull(destino);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ejecutable,
                Arguments = LineaDeComando.Para(herramienta, destino),

                UseShellExecute = false,
            });

            return null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            return $"No se pudo abrir {Nombre(herramienta)}: {ex.Message}";
        }
    }
}
