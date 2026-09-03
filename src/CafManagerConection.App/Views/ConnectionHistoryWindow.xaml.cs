using System.Globalization;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.App.Themes;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.App.Views;

/// <summary>Historial de intentos de conexión, con cuánto duró cada sesión (FR-009).</summary>
[SupportedOSPlatform("windows")]
public partial class ConnectionHistoryWindow : Window
{
    private readonly CompositionRoot _root;

    public ConnectionHistoryWindow(CompositionRoot root)
    {
        _root = root;
        InitializeComponent();

        Loaded += async (_, _) => await CargarAsync().ConfigureAwait(true);
    }

    /// <summary>Una fila de la grilla, ya lista para mostrar.</summary>
    public sealed record Fila(
        string Conexion, string Cuando, string Resultado, string Duracion, string Detalle, Brush Color);

    private async Task CargarAsync()
    {
        var eventos = await _root.History.GetRecentAsync().ConfigureAwait(true);
        var conexiones = await _root.ConnectionService.GetTreeAsync().ConfigureAwait(true);

        var nombres = conexiones.ToDictionary(c => c.Id, c => c.Name);

        var filas = eventos
            .Select(e => new Fila(
                nombres.GetValueOrDefault(e.ConnectionId, "(conexión eliminada)"),
                e.AttemptedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
                Nombre(e.Outcome),
                Duracion(e.DurationSeconds),
                Detalle(e),
                ColorDe(e.Outcome)))
            .ToList();

        _grilla.ItemsSource = filas;
        _vacio.Visibility = filas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var conectadas = eventos.Count(e => e.Outcome == ConnectionOutcome.Success);
        var total = TimeSpan.FromSeconds(eventos.Sum(e => e.DurationSeconds ?? 0));

        _resumen.Text = filas.Count switch
        {
            0 => string.Empty,
            _ => $"{filas.Count} evento(s) · {conectadas} conexión(es) lograda(s) · "
                 + $"{Legible(total)} de sesión en total",
        };
    }

    private static string Nombre(ConnectionOutcome resultado) => resultado switch
    {
        ConnectionOutcome.Success => "Conectada",
        ConnectionOutcome.Failed => "Falló",
        ConnectionOutcome.Cancelled => "Cancelada",
        _ => "Desconocido",
    };

    private static Brush ColorDe(ConnectionOutcome resultado) => Pinceles.De(resultado switch
    {
        ConnectionOutcome.Success => "EstadoConectado",
        ConnectionOutcome.Failed => "EstadoError",
        _ => "EstadoInactivo",
    });

    private static string Duracion(int? segundos) =>
        segundos is null ? "—" : Legible(TimeSpan.FromSeconds(segundos.Value));

    /// <summary>Duración en la unidad que corresponda.</summary>
    private static string Legible(TimeSpan lapso) => lapso.TotalSeconds switch
    {
        < 1 => "menos de 1 s",
        < 60 => $"{(int)lapso.TotalSeconds} s",
        < 3600 => $"{(int)lapso.TotalMinutes} min",
        _ => $"{(int)lapso.TotalHours} h {lapso.Minutes} min",
    };

    /// <summary>Por qué falló, cuando se sabe.</summary>
    private static string Detalle(ConnectionHistoryEntry e) => e.FailureReason switch
    {
        null => string.Empty,
        SessionFailureReason.AuthenticationRejected => "Credenciales rechazadas",
        SessionFailureReason.HostUnreachable => "No se llegó al servidor",
        SessionFailureReason.HostKeyMismatch => "La clave del host cambió",
        SessionFailureReason.Timeout => "Tiempo de espera agotado",
        SessionFailureReason.CertificateUntrusted => "Certificado no confiable",
        SessionFailureReason.PrivateKeyNotFound => "No se encontró la clave privada",
        SessionFailureReason.BadPassphrase => "Frase de paso incorrecta",
        SessionFailureReason.CredentialMissing => "Falta la credencial",
        SessionFailureReason.UnexpectedDisconnect => "Desconexión inesperada",
        _ => "Sin detalle",
    };

    private async void AlActualizar(object sender, RoutedEventArgs e) =>
        await CargarAsync().ConfigureAwait(true);

    private void AlCerrar(object sender, RoutedEventArgs e) => Close();
}
