namespace CafManagerConection.Domain.Settings;

public enum AppTheme
{
    Light,
    Dark,
    System,
}

public readonly record struct AreaDePantalla(int X, int Y, int Width, int Height);

public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized)
{
    public static WindowPlacement Default { get; } = new(0, 0, 1280, 800, false);

    /// <summary>Si la geometría se solapa, aunque sea en parte, con algún monitor (FR-047).</summary>
    public bool EsVisibleEn(IReadOnlyList<AreaDePantalla> pantallas)
    {
        ArgumentNullException.ThrowIfNull(pantallas);

        if (Width <= 0 || Height <= 0)
        {
            return false;
        }

        return pantallas.Any(p =>
            X < p.X + p.Width &&
            X + Width > p.X &&
            Y < p.Y + p.Height &&
            Y + Height > p.Y);
    }
}

public sealed record TerminalPreferences(string FontFamily, int FontSize, int ScrollbackLines)
{
    public static TerminalPreferences Default { get; } = new("Cascadia Mono", 10, 10_000);
}

public static class SettingKeys
{
    public const string WindowX = "window.x";
    public const string WindowY = "window.y";
    public const string WindowWidth = "window.width";
    public const string WindowHeight = "window.height";
    public const string WindowMaximized = "window.maximized";
    public const string Theme = "theme";

    public const string ComandosGuardados = "commands.palette";

    public const string CopiasActivas = "backup.enabled";
    public const string CopiasCarpeta = "backup.folder";
    public const string CopiasCuantas = "backup.keep";
    public const string TerminalScrollbackLines = "terminal.scrollbackLines";
    public const string TerminalFontFamily = "terminal.fontFamily";
    public const string TerminalFontSize = "terminal.fontSize";
    public const string ConnectionTimeoutSeconds = "connection.timeoutSeconds";

    public const string ClaveDeColorRdp = "icon.color.rdp";
    public const string ClaveDeColorSsh = "icon.color.ssh";
    public const string ClaveDeColorWeb = "icon.color.web";

    public const string TreeExpandedFolders = "tree.expandedFolders";
    public const string TreeSelected = "tree.selected";

    public const string PanelWidths = "panel.widths";

    // Vacío = las que elija el filtro automático (FR-083).
    public const string InterfacesVisibles = "status.visibleInterfaces";

    /// <summary>Cada cuántos segundos el panel de estado vuelve a leer el servidor (FR-175).</summary>
    public const string IntervaloDeMuestreo = "status.sampleSeconds";

    public const string ArbolAjusteDeTamano = "tree.fontDelta";
    public const string ArbolMuestraHost = "tree.showHost";
}

public sealed record EstadoDelArbol(IReadOnlyList<Guid> CarpetasAbiertas, Guid? Seleccionado);

public static class Defaults
{
    public const int ConnectionTimeoutSeconds = 30;
    public const int SshKeepAliveSeconds = 60;
    public const int MetricsSampleIntervalSeconds = 5;

    // FR-175: piso de 2 s porque CPU y red se calculan por diferencia entre dos lecturas.
    public static readonly int[] IntervalosDeMuestreo = [2, 5, 10, 30, 60];
    // Diez y no tres: la lectura de estado es un solo comando de trece partes.
    public const int MetricsQueryTimeoutSeconds = 10;
    public const int InventoryQueryTimeoutSeconds = 10;
    public const int MetricsHistoryPoints = 60;
    public const int ClipboardClearSeconds = 30;
}
