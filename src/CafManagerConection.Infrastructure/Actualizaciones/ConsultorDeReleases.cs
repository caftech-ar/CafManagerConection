using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Actualizaciones;

public sealed record ActivoDeRelease(string Nombre, string UrlDeDescarga);

public sealed record InformacionDeRelease(
    string Version,
    string? Nombre,
    string? Novedades,
    DateTimeOffset? PublicadoEl,
    IReadOnlyList<ActivoDeRelease> Activos);

public sealed record ResultadoConsultaDeVersion(InformacionDeRelease? Release, string? Motivo)
{
    public bool Exito => Release is not null;

    public static ResultadoConsultaDeVersion Ok(InformacionDeRelease release) => new(release, null);

    public static ResultadoConsultaDeVersion NoSePudo(string motivo) => new(null, motivo);
}

/// <summary>Pregunta a GitHub la última release publicada: consulta anónima, de sólo lectura, sin nada que identifique al usuario ni al equipo (FR-159a).</summary>
public sealed class ConsultorDeReleases : IDisposable
{
    /// <summary>Lo único que va en el <c>User-Agent</c>: ni versión ni ningún otro dato (FR-159a).</summary>
    public const string NombreDelProducto = Configuration.AppPaths.ProductFolderName;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly IAppLogger? _logger;

    public ConsultorDeReleases(
        HttpMessageHandler? manejador = null,
        IAppLogger? logger = null,
        TimeSpan? tiempoDeEspera = null)
    {
        _http = new HttpClient(manejador ?? new HttpClientHandler(), disposeHandler: true)
        {
            Timeout = tiempoDeEspera ?? TimeSpan.FromSeconds(5),
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd(NombreDelProducto);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _logger = logger;
    }

    public async Task<ResultadoConsultaDeVersion> UltimaReleaseAsync(
        string propietario, string repositorio, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propietario);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositorio);

        var url = $"https://api.github.com/repos/{propietario}/{repositorio}/releases/latest";

        try
        {
            using var respuesta = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return ResultadoConsultaDeVersion.NoSePudo(
                    "El repositorio no tiene releases publicadas.");
            }

            if (respuesta.StatusCode == HttpStatusCode.Forbidden
                || (int)respuesta.StatusCode == 429)
            {
                // GitHub contesta 403 tanto para «sin permiso» como para «límite agotado»; para una consulta anónima diaria el motivo es el mismo.
                return ResultadoConsultaDeVersion.NoSePudo(
                    "Se alcanzó el límite de consultas anónimas a GitHub.");
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                return ResultadoConsultaDeVersion.NoSePudo(
                    $"GitHub respondió {(int)respuesta.StatusCode}.");
            }

            await using var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct)
                .ConfigureAwait(false);

            var dto = await JsonSerializer
                .DeserializeAsync<ReleaseDto>(cuerpo, OpcionesJson, ct)
                .ConfigureAwait(false);

            if (dto is null || string.IsNullOrWhiteSpace(dto.TagName))
            {
                return ResultadoConsultaDeVersion.NoSePudo(
                    "La respuesta de GitHub no trae la forma esperada.");
            }

            var activos = (dto.Assets ?? [])
                .Where(a => !string.IsNullOrWhiteSpace(a.Name)
                    && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
                .Select(a => new ActivoDeRelease(a.Name!, a.BrowserDownloadUrl!))
                .ToList();

            return ResultadoConsultaDeVersion.Ok(new InformacionDeRelease(
                dto.TagName, dto.Name, dto.Body, dto.PublishedAt, activos));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ResultadoConsultaDeVersion.NoSePudo("GitHub no contestó a tiempo.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException)
        {
            _logger?.TechnicalError("consultar la última versión publicada", ex);
            return ResultadoConsultaDeVersion.NoSePudo("No se pudo consultar GitHub.");
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<ActivoDto>? Assets { get; set; }
    }

    private sealed class ActivoDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
