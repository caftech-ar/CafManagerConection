using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CafManagerConection.App.Bootstrap;

/// <summary>Trae al frente la ventana de la instancia que ya está corriendo (FR-112).</summary>
[SupportedOSPlatform("windows")]
internal static partial class SingleInstance
{
    private const int SW_RESTORE = 9;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint hWnd);

    public static void FocusExistingWindow()
    {
        try
        {
            var actual = Process.GetCurrentProcess();

            var otra = Process.GetProcessesByName(actual.ProcessName)
                .FirstOrDefault(p => p.Id != actual.Id && p.MainWindowHandle != nint.Zero);

            if (otra is null)
            {
                return;
            }

            var handle = otra.MainWindowHandle;

            if (IsIconic(handle))
            {
                ShowWindow(handle, SW_RESTORE);
            }

            SetForegroundWindow(handle);
        }
        catch (Exception)
        {
        }
    }
}
