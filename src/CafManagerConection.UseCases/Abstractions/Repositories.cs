using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.UseCases.Abstractions;

public interface ITagRepository
{
    Task<IReadOnlyList<Etiqueta>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(Etiqueta etiqueta, CancellationToken ct = default);

    Task UpdateAsync(Etiqueta etiqueta, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<int> CountUsagesAsync(Guid id, CancellationToken ct = default);
}

public interface IFolderRepository
{
    Task<IReadOnlyList<Folder>> GetAllAsync(CancellationToken ct = default);

    Task<Folder?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Folder folder, CancellationToken ct = default);

    Task UpdateAsync(Folder folder, CancellationToken ct = default);

    Task<DeletionResult> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Renumera de cero en adelante. Un identificador que no existe se ignora: así se abre un lugar antes de insertar.</summary>
    Task ReorderAsync(
        Guid? parentId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}

public sealed record DeletionResult(
    IReadOnlyList<Guid> DeletedFolderIds,
    IReadOnlyList<Guid> DeletedConnectionIds);

public sealed record ConnectionRecord(
    Connection Connection,
    RdpSettings? Rdp = null,
    SshSettings? Ssh = null,
    WebSettings? Web = null);

public interface IConnectionRepository
{
    Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken ct = default);

    Task<ConnectionRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(ConnectionRecord record, CancellationToken ct = default);

    Task UpdateAsync(ConnectionRecord record, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task SetLastConnectedAsync(Guid id, DateTimeOffset when, CancellationToken ct = default);

    /// <summary>Renumera de cero en adelante. Un identificador que no existe se ignora: así se abre un lugar antes de insertar.</summary>
    Task ReorderAsync(Guid? folderId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}

public interface IConnectionHistoryRepository
{
    Task AddAsync(ConnectionHistoryEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<ConnectionHistoryEntry>> GetForConnectionAsync(
        Guid connectionId, int limit = 50, CancellationToken ct = default);

    Task<IReadOnlyList<ConnectionHistoryEntry>> GetRecentAsync(
        int limit = 500, CancellationToken ct = default);
}

public interface ITunnelRepository
{
    Task<IReadOnlyList<SshTunnel>> GetForConnectionAsync(Guid connectionId, CancellationToken ct = default);

    Task<IReadOnlyList<SshTunnel>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(SshTunnel tunnel, CancellationToken ct = default);

    Task UpdateAsync(SshTunnel tunnel, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
}

public interface IDatabaseInitializer
{
    Task<DatabaseStartupResult> InitializeAsync(CancellationToken ct = default);
}

/// <summary><c>RecoveredFromCorruptionPath</c>: dónde quedó la base dañada, si hubo que crear otra (FR-052).</summary>
public sealed record DatabaseStartupResult(
    bool Migrated,
    int FromVersion,
    int ToVersion,
    string? RecoveredFromCorruptionPath = null);

public interface IAppSettingsService
{
    Task<WindowPlacement> GetWindowPlacementAsync(CancellationToken ct = default);

    Task SaveWindowPlacementAsync(WindowPlacement placement, CancellationToken ct = default);

    Task<AppTheme> GetThemeAsync(CancellationToken ct = default);

    Task SetThemeAsync(AppTheme theme, CancellationToken ct = default);

    Task<ColoresDeIconos> GetIconColorsAsync(CancellationToken ct = default);

    Task SetIconColorsAsync(ColoresDeIconos colores, CancellationToken ct = default);

    Task<TerminalPreferences> GetTerminalPreferencesAsync(CancellationToken ct = default);

    Task SaveTerminalPreferencesAsync(TerminalPreferences preferences, CancellationToken ct = default);

    Task<EstadoDelArbol?> GetTreeStateAsync(CancellationToken ct = default);

    Task SaveTreeStateAsync(EstadoDelArbol estado, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, double>> GetPanelWidthsAsync(CancellationToken ct = default);

    Task SavePanelWidthAsync(string panel, double ancho, CancellationToken ct = default);

    Task ResetPanelWidthsAsync(CancellationToken ct = default);

    /// <summary>Vacía = las que decida el filtro automático (FR-083).</summary>
    Task<IReadOnlyList<string>> GetVisibleInterfacesAsync(CancellationToken ct = default);

    Task SaveVisibleInterfacesAsync(
        IReadOnlyList<string> interfaces, CancellationToken ct = default);

    Task<int> GetStatusIntervalAsync(CancellationToken ct = default);

    Task SaveStatusIntervalAsync(int segundos, CancellationToken ct = default);

    Task<AjustesDelArbol> GetTreeAppearanceAsync(CancellationToken ct = default);

    Task SaveTreeAppearanceAsync(AjustesDelArbol ajustes, CancellationToken ct = default);

    Task<PaletaDeComandos> GetCommandPaletteAsync(CancellationToken ct = default);

    Task SaveCommandPaletteAsync(PaletaDeComandos paleta, CancellationToken ct = default);

    Task<AjustesDeCopia> GetBackupSettingsAsync(CancellationToken ct = default);

    Task SaveBackupSettingsAsync(AjustesDeCopia ajustes, CancellationToken ct = default);
}
