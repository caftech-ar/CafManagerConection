using System.Net.Http;
using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Actualizaciones;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.App.Services;

/// <summary>Resultado de preguntar si hay una versión nueva.</summary>
public sealed record ResultadoDeComprobacion(
    bool Consultada,
    InformacionDeRelease? Release,
    VersionDeAplicacion? VersionDisponible,
    bool HayVersionNueva,
    string? Motivo);

/// <summary>La capa de aplicación del aviso de versión nueva: decide si corresponde preguntar, guarda cuándo se preguntó, y compara lo que contesta GitHub contra lo que está corriendo (FR-159 a FR-162).</summary>
public sealed class ActualizacionesService
{
    private readonly AjustesDeActualizacionStore _ajustes;
    private readonly IAppLogger? _logger;
    private readonly HttpMessageHandler? _manejador;

    public ActualizacionesService(
        ISettingsStore store, IAppLogger? logger = null, HttpMessageHandler? manejador = null)
    {
        _ajustes = new AjustesDeActualizacionStore(store);
        _logger = logger;
        _manejador = manejador;
    }

    public Task<AjustesDeActualizacion> ObtenerAjustesAsync(CancellationToken ct = default) =>
        _ajustes.ObtenerAsync(ct);

    public Task GuardarAjustesAsync(AjustesDeActualizacion ajustes, CancellationToken ct = default) =>
        _ajustes.GuardarAsync(ajustes, ct);

    public static VersionDeAplicacion VersionActual()
    {
        var texto = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        return VersionDeAplicacion.TryParse(texto, out var version) && version is not null
            ? version
            : VersionDeAplicacion.Parse("0.0.0");
    }

    public bool CorrespondeAvisar(AjustesDeActualizacion ajustes, VersionDeAplicacion versionDisponible) =>
        PoliticaDeActualizaciones.CorrespondeAvisar(
            versionDisponible,
            VersionDeAplicacion.TryParse(ajustes.VersionPospuesta, out var pospuesta) ? pospuesta : null,
            ajustes.MomentoDePosposicion,
            DateTimeOffset.Now);

    /// <summary>Pospone el aviso de esa versión hasta mañana (FR-160a).</summary>
    public Task PosponerAsync(
        AjustesDeActualizacion ajustes, VersionDeAplicacion version, CancellationToken ct = default) =>
        _ajustes.GuardarAsync(
            ajustes with { VersionPospuesta = version.ToString(), MomentoDePosposicion = DateTimeOffset.Now },
            ct);

    public static string UrlDePagina(string origen, string version) =>
        $"https://github.com/{origen}/releases/tag/{version}";

    public async Task<ResultadoDeComprobacion> ComprobarAsync(CancellationToken ct = default)
    {
        var ajustes = await _ajustes.ObtenerAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(ajustes.Origen))
        {
            return new ResultadoDeComprobacion(false, null, null, false, "No hay un origen configurado.");
        }

        var partes = ajustes.Origen.Split('/', 2, StringSplitOptions.TrimEntries);

        if (partes.Length != 2 || partes[0].Length == 0 || partes[1].Length == 0)
        {
            return new ResultadoDeComprobacion(
                false, null, null, false, "El origen debe tener la forma propietario/repositorio.");
        }

        using var consultor = new ConsultorDeReleases(_manejador, _logger);
        var resultado = await consultor.UltimaReleaseAsync(partes[0], partes[1], ct).ConfigureAwait(false);

        await _ajustes.GuardarAsync(ajustes with { UltimaConsulta = DateTimeOffset.Now }, ct)
            .ConfigureAwait(false);

        if (!resultado.Exito || resultado.Release is null)
        {
            return new ResultadoDeComprobacion(true, null, null, false, resultado.Motivo);
        }

        if (!VersionDeAplicacion.TryParse(resultado.Release.Version, out var versionDisponible)
            || versionDisponible is null)
        {
            return new ResultadoDeComprobacion(
                true, resultado.Release, null, false, "La versión publicada no se pudo interpretar.");
        }

        return new ResultadoDeComprobacion(
            true, resultado.Release, versionDisponible, versionDisponible > VersionActual(), null);
    }
}
