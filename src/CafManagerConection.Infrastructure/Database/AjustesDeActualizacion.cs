using System.Globalization;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Database;

public sealed record AjustesDeActualizacion(
    string Origen = AjustesDeActualizacion.OrigenPorOmision,
    DateTimeOffset? UltimaConsulta = null,
    string? VersionPospuesta = null,
    DateTimeOffset? MomentoDePosposicion = null)
{
    /// <summary>El repositorio del que se leen las releases. Fijo, no configurable (FR-159b).</summary>
    public const string Repositorio = "caftech-ar/CafManagerConection";

    /// <summary>Nombre viejo de la constante, conservado para no romper llamadas existentes.</summary>
    public const string OrigenPorOmision = Repositorio;

    public static AjustesDeActualizacion Default { get; } = new();
}

public sealed class AjustesDeActualizacionStore
{
    private const string ClaveUltimaConsulta = "updates.lastCheckedAt";
    private const string ClaveVersionPospuesta = "updates.postponedVersion";
    private const string ClaveMomentoDePosposicion = "updates.postponedAt";

    private readonly ISettingsStore _store;

    public AjustesDeActualizacionStore(ISettingsStore store) => _store = store;

    public async Task<AjustesDeActualizacion> ObtenerAsync(CancellationToken ct = default)
    {
        var todos = await _store.GetAllAsync(ct).ConfigureAwait(false);

        return new AjustesDeActualizacion(
            // No se lee de la base: una base editada a mano no puede apuntar la comprobación de versión a otro repositorio ni apagarla.
            AjustesDeActualizacion.Repositorio,
            Fecha(todos, ClaveUltimaConsulta),
            NuloSiVacio(todos.GetValueOrDefault(ClaveVersionPospuesta)),
            Fecha(todos, ClaveMomentoDePosposicion));
    }

    public async Task GuardarAsync(AjustesDeActualizacion ajustes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ajustes);

        await _store.SetAsync(ClaveUltimaConsulta, Texto(ajustes.UltimaConsulta), ct)
            .ConfigureAwait(false);
        await _store.SetAsync(ClaveVersionPospuesta, ajustes.VersionPospuesta ?? string.Empty, ct)
            .ConfigureAwait(false);
        await _store.SetAsync(ClaveMomentoDePosposicion, Texto(ajustes.MomentoDePosposicion), ct)
            .ConfigureAwait(false);
    }

    private static string? NuloSiVacio(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor;

    // Formato "O" (ida y vuelta): guarda el desfase horario, y sin él una posposición de la tarde se puede leer como del día siguiente.
    private static string Texto(DateTimeOffset? momento) =>
        momento?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private static DateTimeOffset? Fecha(IReadOnlyDictionary<string, string> todos, string clave) =>
        todos.TryGetValue(clave, out var texto)
        && DateTimeOffset.TryParse(
            texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var valor)
            ? valor
            : null;
}
