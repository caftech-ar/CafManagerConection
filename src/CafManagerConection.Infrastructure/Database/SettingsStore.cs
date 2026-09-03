using System.Globalization;
using CafManagerConection.Domain.Settings;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class SettingsStore : ISettingsStore
{
    private readonly ISqliteConnectionFactory _factory;

    public SettingsStore(ISqliteConnectionFactory factory) => _factory = factory;

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var value = db.QuerySingleOrDefault<string>(
            "SELECT value FROM application_settings WHERE key = @Key;", new { Key = key });
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute("""
            INSERT INTO application_settings (key, value) VALUES (@Key, @Value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """, new { Key = key, Value = value });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var rows = db.Query<(string Key, string Value)>(
            "SELECT key, value FROM application_settings;");

        return Task.FromResult<IReadOnlyDictionary<string, string>>(
            rows.ToDictionary(r => r.Key, r => r.Value));
    }
}

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly ISettingsStore _store;

    public AppSettingsService(ISettingsStore store) => _store = store;

    public async Task<WindowPlacement> GetWindowPlacementAsync(CancellationToken ct = default)
    {
        var all = await _store.GetAllAsync(ct).ConfigureAwait(false);

        if (!all.ContainsKey(SettingKeys.WindowWidth))
        {
            return WindowPlacement.Default;
        }

        return new WindowPlacement(
            Int(all, SettingKeys.WindowX, WindowPlacement.Default.X),
            Int(all, SettingKeys.WindowY, WindowPlacement.Default.Y),
            Int(all, SettingKeys.WindowWidth, WindowPlacement.Default.Width),
            Int(all, SettingKeys.WindowHeight, WindowPlacement.Default.Height),
            Bool(all, SettingKeys.WindowMaximized, false));
    }

    public async Task SaveWindowPlacementAsync(
        WindowPlacement placement, CancellationToken ct = default)
    {
        await _store.SetAsync(SettingKeys.WindowX, Str(placement.X), ct).ConfigureAwait(false);
        await _store.SetAsync(SettingKeys.WindowY, Str(placement.Y), ct).ConfigureAwait(false);
        await _store.SetAsync(SettingKeys.WindowWidth, Str(placement.Width), ct).ConfigureAwait(false);
        await _store.SetAsync(SettingKeys.WindowHeight, Str(placement.Height), ct).ConfigureAwait(false);
        await _store.SetAsync(
            SettingKeys.WindowMaximized, placement.Maximized ? "1" : "0", ct).ConfigureAwait(false);
    }

    public async Task<AppTheme> GetThemeAsync(CancellationToken ct = default)
    {
        var value = await _store.GetAsync(SettingKeys.Theme, ct).ConfigureAwait(false);
        return Enum.TryParse<AppTheme>(value, out var theme) ? theme : AppTheme.Light;
    }

    public Task SetThemeAsync(AppTheme theme, CancellationToken ct = default) =>
        _store.SetAsync(SettingKeys.Theme, theme.ToString(), ct);

    public async Task<PaletaDeComandos> GetCommandPaletteAsync(CancellationToken ct = default)
    {
        var texto = await _store
            .GetAsync(SettingKeys.ComandosGuardados, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return new PaletaDeComandos();
        }

        try
        {
            var guardados = System.Text.Json.JsonSerializer
                .Deserialize<List<ComandoGuardado>>(texto, PaletaJson);

            return new PaletaDeComandos(guardados);
        }
        catch (System.Text.Json.JsonException)
        {
            return new PaletaDeComandos();
        }
    }

    public Task SaveCommandPaletteAsync(
        PaletaDeComandos paleta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paleta);

        var texto = System.Text.Json.JsonSerializer.Serialize(paleta.Todos, PaletaJson);

        return _store.SetAsync(SettingKeys.ComandosGuardados, texto, ct);
    }

    private static readonly System.Text.Json.JsonSerializerOptions PaletaJson = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    public async Task<AjustesDeCopia> GetBackupSettingsAsync(CancellationToken ct = default)
    {
        var todos = await _store.GetAllAsync(ct).ConfigureAwait(false);
        var d = AjustesDeCopia.Default;

        return new AjustesDeCopia(
            Bool(todos, SettingKeys.CopiasActivas, d.Activas),
            todos.GetValueOrDefault(SettingKeys.CopiasCarpeta, d.Carpeta),
            Int(todos, SettingKeys.CopiasCuantas, d.CuantasGuardar)).Normalizados();
    }

    public async Task SaveBackupSettingsAsync(
        AjustesDeCopia ajustes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ajustes);

        var n = ajustes.Normalizados();

        await _store.SetAsync(
            SettingKeys.CopiasActivas, n.Activas ? "1" : "0", ct).ConfigureAwait(false);
        await _store.SetAsync(SettingKeys.CopiasCarpeta, n.Carpeta, ct).ConfigureAwait(false);
        await _store.SetAsync(SettingKeys.CopiasCuantas, Str(n.CuantasGuardar), ct)
            .ConfigureAwait(false);
    }

    public async Task<ColoresDeIconos> GetIconColorsAsync(CancellationToken ct = default)
    {
        var todos = await _store.GetAllAsync(ct).ConfigureAwait(false);
        var d = ColoresDeIconos.Default;

        string Elegir(string clave, string porOmision)
        {
            var valor = todos.GetValueOrDefault(clave);
            return PaletaIconos.EsValido(valor) ? valor! : porOmision;
        }

        return new ColoresDeIconos(
            Elegir(SettingKeys.ClaveDeColorRdp, d.Rdp),
            Elegir(SettingKeys.ClaveDeColorSsh, d.Ssh),
            Elegir(SettingKeys.ClaveDeColorWeb, d.Web));
    }

    public async Task SetIconColorsAsync(
        ColoresDeIconos colores, CancellationToken ct = default)
    {
        await _store.SetAsync(SettingKeys.ClaveDeColorRdp, colores.Rdp, ct).ConfigureAwait(false);
        await _store.SetAsync(SettingKeys.ClaveDeColorSsh, colores.Ssh, ct).ConfigureAwait(false);
        await _store.SetAsync(SettingKeys.ClaveDeColorWeb, colores.Web, ct).ConfigureAwait(false);
    }

    public async Task<TerminalPreferences> GetTerminalPreferencesAsync(CancellationToken ct = default)
    {
        var all = await _store.GetAllAsync(ct).ConfigureAwait(false);
        var d = TerminalPreferences.Default;

        return new TerminalPreferences(
            all.GetValueOrDefault(SettingKeys.TerminalFontFamily, d.FontFamily),
            Int(all, SettingKeys.TerminalFontSize, d.FontSize),
            Int(all, SettingKeys.TerminalScrollbackLines, d.ScrollbackLines));
    }

    public async Task SaveTerminalPreferencesAsync(
        TerminalPreferences preferences, CancellationToken ct = default)
    {
        await _store.SetAsync(
            SettingKeys.TerminalFontFamily, preferences.FontFamily, ct).ConfigureAwait(false);
        await _store.SetAsync(
            SettingKeys.TerminalFontSize, Str(preferences.FontSize), ct).ConfigureAwait(false);
        await _store.SetAsync(
            SettingKeys.TerminalScrollbackLines, Str(preferences.ScrollbackLines), ct).ConfigureAwait(false);
    }

    public async Task<EstadoDelArbol?> GetTreeStateAsync(CancellationToken ct = default)
    {
        var todos = await _store.GetAllAsync(ct).ConfigureAwait(false);

        if (!todos.ContainsKey(SettingKeys.TreeExpandedFolders))
        {
            return null;
        }

        var abiertas = (todos.GetValueOrDefault(SettingKeys.TreeExpandedFolders) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Guid.TryParse(t, out var id) ? id : (Guid?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList();

        var elegida = Guid.TryParse(todos.GetValueOrDefault(SettingKeys.TreeSelected), out var sel)
            ? sel
            : (Guid?)null;

        return new EstadoDelArbol(abiertas, elegida);
    }

    public async Task SaveTreeStateAsync(EstadoDelArbol estado, CancellationToken ct = default)
    {
        await _store.SetAsync(
            SettingKeys.TreeExpandedFolders,
            string.Join(',', estado.CarpetasAbiertas.Select(id => id.ToString("D"))),
            ct).ConfigureAwait(false);

        await _store.SetAsync(
            SettingKeys.TreeSelected,
            estado.Seleccionado?.ToString("D") ?? string.Empty,
            ct).ConfigureAwait(false);
    }

    /// <summary>Ancho recordado de cada panel, todos en una sola clave con el formato <c>panel=ancho;panel=ancho</c>.</summary>
    public async Task<IReadOnlyDictionary<string, double>> GetPanelWidthsAsync(
        CancellationToken ct = default)
    {
        var todos = await _store.GetAllAsync(ct).ConfigureAwait(false);
        var crudo = todos.GetValueOrDefault(SettingKeys.PanelWidths) ?? string.Empty;
        var anchos = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var tramo in crudo.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = tramo.Split('=', 2);

            if (partes.Length == 2
                && double.TryParse(
                    partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ancho)
                && ancho > 0)
            {
                anchos[partes[0].Trim()] = ancho;
            }
        }

        return anchos;
    }

    public async Task SavePanelWidthAsync(
        string panel, double ancho, CancellationToken ct = default)
    {
        var anchos = new Dictionary<string, double>(
            await GetPanelWidthsAsync(ct).ConfigureAwait(false), StringComparer.OrdinalIgnoreCase)
        {
            [panel] = ancho,
        };

        var texto = string.Join(
            ';',
            anchos.Select(p => $"{p.Key}={p.Value.ToString("0.##", CultureInfo.InvariantCulture)}"));

        await _store.SetAsync(SettingKeys.PanelWidths, texto, ct).ConfigureAwait(false);
    }

    public Task ResetPanelWidthsAsync(CancellationToken ct = default) =>
        _store.SetAsync(SettingKeys.PanelWidths, string.Empty, ct);

    // Separadas por punto y coma: un nombre de interfaz no lo lleva (eth0, enp3s0, «Ethernet 2»), así que no hace falta escape.
    public async Task<IReadOnlyList<string>> GetVisibleInterfacesAsync(
        CancellationToken ct = default)
    {
        var todos = await _store.GetAllAsync(ct).ConfigureAwait(false);
        var crudo = todos.GetValueOrDefault(SettingKeys.InterfacesVisibles) ?? string.Empty;

        return [.. crudo
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    public Task SaveVisibleInterfacesAsync(
        IReadOnlyList<string> interfaces, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(interfaces);

        return _store.SetAsync(
            SettingKeys.InterfacesVisibles, string.Join(';', interfaces), ct);
    }

    // Se valida al leer y no sólo al escribir: una base editada a mano con un 0 dejaría el panel consultando el servidor sin pausa.
    public async Task<int> GetStatusIntervalAsync(CancellationToken ct = default)
    {
        var todos = await _store.GetAllAsync(ct).ConfigureAwait(false);

        return Valido(int.TryParse(
            todos.GetValueOrDefault(SettingKeys.IntervaloDeMuestreo), out var guardado)
            ? guardado
            : Defaults.MetricsSampleIntervalSeconds);
    }

    public Task SaveStatusIntervalAsync(int segundos, CancellationToken ct = default) =>
        _store.SetAsync(
            SettingKeys.IntervaloDeMuestreo,
            Valido(segundos).ToString(CultureInfo.InvariantCulture),
            ct);

    private static int Valido(int segundos) =>
        Defaults.IntervalosDeMuestreo.Contains(segundos)
            ? segundos
            : Defaults.MetricsSampleIntervalSeconds;

    public async Task<AjustesDelArbol> GetTreeAppearanceAsync(CancellationToken ct = default)
    {
        var todos = await _store.GetAllAsync(ct).ConfigureAwait(false);

        return new AjustesDelArbol(
            Double(todos, SettingKeys.ArbolAjusteDeTamano, AjustesDelArbol.AjustePorOmision),
            Bool(todos, SettingKeys.ArbolMuestraHost, false)).Acotado();
    }

    public async Task SaveTreeAppearanceAsync(
        AjustesDelArbol ajustes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ajustes);

        var acotado = ajustes.Acotado();

        await _store.SetAsync(
            SettingKeys.ArbolAjusteDeTamano,
            acotado.AjusteDeTamano.ToString("0.###", CultureInfo.InvariantCulture),
            ct).ConfigureAwait(false);

        await _store.SetAsync(
            SettingKeys.ArbolMuestraHost,
            acotado.MuestraHost ? "1" : "0",
            ct).ConfigureAwait(false);
    }

    private static string Str(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static int Int(IReadOnlyDictionary<string, string> all, string key, int fallback) =>
        all.TryGetValue(key, out var v) &&
        int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    // Cultura invariable en las dos puntas: en una máquina con coma decimal un «-1.5» escrito acá volvía sin interpretarse.
    private static double Double(
        IReadOnlyDictionary<string, string> all, string key, double fallback) =>
        all.TryGetValue(key, out var v) &&
        double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool Bool(IReadOnlyDictionary<string, string> all, string key, bool fallback) =>
        all.TryGetValue(key, out var v) ? v == "1" : fallback;
}
