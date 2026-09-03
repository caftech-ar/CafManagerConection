using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Services;
using CafManagerConection.App.Views;

namespace CafManagerConection.App;

/// <summary>Punto de entrada. Garantiza una sola instancia por usuario y arma el grafo de dependencias antes de abrir la ventana.</summary>
[SupportedOSPlatform("windows")]
public partial class App : Application
{
    /// <summary>Nombre del mutex que garantiza una sola instancia por usuario (FR-112).</summary>
    private const string MutexInstanciaUnica = "Local\\CafManagerConection.SingleInstance";

    private Mutex? _mutex;
    private CompositionRoot? _root;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexInstanciaUnica, out var esPrimera);

        if (!esPrimera)
        {
            SingleInstance.FocusExistingWindow();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += OnErrorNoAtendido;

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((remitente, _) =>
            {
                if (remitente is Window ventana)
                {
                    Temas.AplicarBarraDeTitulo(ventana);
                }
            }));

        try
        {
            _root = CompositionRoot.CreateAsync().GetAwaiter().GetResult();

            var ventana = new MainWindow(_root);
            MainWindow = ventana;
            ventana.Show();
        }
        catch (Exception ex)
        {
            InformarFalloDeArranque(ex);
            Shutdown(1);
        }
    }

    private void OnErrorNoAtendido(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _root?.Logger.TechnicalError("error no atendido en la interfaz", e.Exception);

        var texto =
            $"Ocurrió un error inesperado:{Environment.NewLine}{Environment.NewLine}"
            + e.Exception.Message;

        try
        {
            if (MainWindow is { } principal)
            {
                Views.MessageWindow.Avisar(principal, "CafManagerConection", texto);
            }
            else
            {
                MostrarCrudo(texto, "CafManagerConection");
            }
        }
        catch (Exception)
        {
            MostrarCrudo(texto, "CafManagerConection");
        }

        e.Handled = true;
    }

    /// <summary>Un fallo al arrancar no debe cerrarse en silencio: el usuario necesita saber qué pasó y dónde mirar.</summary>
    private static void InformarFalloDeArranque(Exception ex)
    {
        var destino = Path.Combine(Path.GetTempPath(), "cmc-arranque.log");

        try
        {
            File.WriteAllText(destino, ex.ToString());
        }
        catch (IOException)
        {
        }

        MostrarCrudo(
            $"No se pudo iniciar CafManagerConection.{Environment.NewLine}{Environment.NewLine}"
            + $"{ex.Message}{Environment.NewLine}{Environment.NewLine}"
            + $"Detalle técnico en: {destino}",
            "Error de inicio");
    }

    private static void MostrarCrudo(string mensaje, string titulo) =>
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);

    protected override void OnExit(ExitEventArgs e)
    {
        _root?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
