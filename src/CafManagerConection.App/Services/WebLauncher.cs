using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using CafManagerConection.Domain.Connections;

namespace CafManagerConection.App.Services;

/// <summary>Abre una entrada web en un navegador del sistema (FR-115, FR-115a).</summary>
[SupportedOSPlatform("windows")]
public static class WebLauncher
{
    public static void Open(WebSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.Browser))
        {
            OpenWithDefaultBrowser(settings.Url);
            return;
        }

        var args = BuildArguments(settings.Browser, settings.Url, settings.PrivateWindow);

        Process.Start(new ProcessStartInfo
        {
            FileName = settings.Browser,
            Arguments = args,
            UseShellExecute = true,
        });
    }

    private static void OpenWithDefaultBrowser(string url)
    {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    /// <summary>Cada familia de navegadores nombra distinto el modo privado. Se detecta por el nombre del ejecutable, que es lo único disponible sin consultar el registro.</summary>
    internal static string BuildArguments(string browserPath, string url, bool privateWindow)
    {
        var quotedUrl = $"\"{url}\"";

        if (!privateWindow)
        {
            return quotedUrl;
        }

        var exe = Path.GetFileNameWithoutExtension(browserPath).ToLowerInvariant();

        var flag = exe switch
        {
            "firefox" or "waterfox" or "librewolf" => "-private-window",
            "msedge" => "-inprivate",
            "chrome" or "brave" or "vivaldi" or "opera" or "chromium" => "--incognito",
            _ => string.Empty,
        };

        return string.IsNullOrEmpty(flag) ? quotedUrl : $"{flag} {quotedUrl}";
    }
}
