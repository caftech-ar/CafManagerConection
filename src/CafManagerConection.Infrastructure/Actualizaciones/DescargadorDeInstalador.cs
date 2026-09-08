using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Actualizaciones;

public sealed record ProgresoDeDescarga(long BytesDescargados, long? BytesTotales);

public enum EstadoDeDescarga
{
    Verificada,

    /// <summary>Se descargó pero el hash no coincide. El archivo ya se borró.</summary>
    HashNoCoincide,

    SinHashPublicado,

    Fallo,
}

public sealed record ResultadoDeDescarga(EstadoDeDescarga Estado, string? RutaArchivo, string? Motivo)
{
    public static ResultadoDeDescarga Verificada(string ruta) =>
        new(EstadoDeDescarga.Verificada, ruta, null);

    public static ResultadoDeDescarga HashNoCoincide(string motivo) =>
        new(EstadoDeDescarga.HashNoCoincide, null, motivo);

    public static ResultadoDeDescarga SinHashPublicado(string motivo) =>
        new(EstadoDeDescarga.SinHashPublicado, null, motivo);

    public static ResultadoDeDescarga Fallo(string motivo) =>
        new(EstadoDeDescarga.Fallo, null, motivo);
}

// El instalador no lleva firma de certificado de código: el hash publicado en la misma release es lo único que distingue el original de uno reemplazado en el camino.
public sealed class DescargadorDeInstalador : IDisposable
{
    // 64 caracteres hexadecimales seguidos: la forma de un SHA-256 en cualquier notación habitual.
    private static readonly Regex PatronDeHash = new("[0-9a-fA-F]{64}", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly AppPaths _rutas;
    private readonly IAppLogger? _logger;

    public DescargadorDeInstalador(
        AppPaths rutas, HttpMessageHandler? manejador = null, IAppLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        _rutas = rutas;
        _http = new HttpClient(manejador ?? new HttpClientHandler(), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd(ConsultorDeReleases.NombreDelProducto);
        _logger = logger;
    }

    public string CarpetaDeDescargas => Path.Combine(_rutas.Root, "actualizaciones");

    /// <summary>Descarga el instalador y lo verifica. Nunca lanza: el fallo vuelve como <see cref="ResultadoDeDescarga"/> con el motivo.</summary>
    public async Task<ResultadoDeDescarga> DescargarYVerificarAsync(
        InformacionDeRelease release,
        ActivoDeRelease instalador,
        IProgress<ProgresoDeDescarga>? progreso = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(instalador);

        string? destino = null;

        try
        {
            var hashPublicado = await HashPublicadoAsync(release, instalador, ct)
                .ConfigureAwait(false);

            if (hashPublicado is null)
            {
                return ResultadoDeDescarga.SinHashPublicado(
                    "La release no publica un hash SHA-256 para este archivo.");
            }

            Directory.CreateDirectory(CarpetaDeDescargas);
            destino = Path.Combine(CarpetaDeDescargas, instalador.Nombre);

            await DescargarArchivoAsync(instalador.UrlDeDescarga, destino, progreso, ct)
                .ConfigureAwait(false);

            string hashCalculado;

            await using (var flujo = File.OpenRead(destino))
            {
                hashCalculado = Convert.ToHexString(
                    await SHA256.HashDataAsync(flujo, ct).ConfigureAwait(false));
            }

            if (!string.Equals(hashCalculado, hashPublicado, StringComparison.OrdinalIgnoreCase))
            {
                BorrarSiExiste(destino);

                return ResultadoDeDescarga.HashNoCoincide(
                    "El archivo descargado no coincide con el hash publicado en la release; " +
                    "se borró sin ejecutarlo.");
            }

            return ResultadoDeDescarga.Verificada(destino);
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError("descargar y verificar el instalador", ex);
            BorrarSiExiste(destino);
            return ResultadoDeDescarga.Fallo("No se pudo descargar el instalador.");
        }
    }

    /// <summary>Primero el asset <c>&lt;instalador&gt;.sha256</c>; si no existe, un hash suelto en el cuerpo de la release.</summary>
    private async Task<string?> HashPublicadoAsync(
        InformacionDeRelease release, ActivoDeRelease instalador, CancellationToken ct)
    {
        var nombreEsperado = instalador.Nombre + ".sha256";

        var activoDeHash = release.Activos.FirstOrDefault(
            a => string.Equals(a.Nombre, nombreEsperado, StringComparison.OrdinalIgnoreCase));

        if (activoDeHash is not null)
        {
            var contenido = await _http.GetStringAsync(activoDeHash.UrlDeDescarga, ct)
                .ConfigureAwait(false);

            var enElAsset = PatronDeHash.Match(contenido);

            if (enElAsset.Success)
            {
                return enElAsset.Value;
            }
        }

        if (release.Novedades is { } novedades)
        {
            var enElCuerpo = PatronDeHash.Match(novedades);

            if (enElCuerpo.Success)
            {
                return enElCuerpo.Value;
            }
        }

        return null;
    }

    private async Task DescargarArchivoAsync(
        string url, string destino, IProgress<ProgresoDeDescarga>? progreso, CancellationToken ct)
    {
        using var respuesta = await _http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        respuesta.EnsureSuccessStatusCode();

        var total = respuesta.Content.Headers.ContentLength;

        await using var origen = await respuesta.Content.ReadAsStreamAsync(ct)
            .ConfigureAwait(false);

        await using var archivo = new FileStream(
            destino, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long descargados = 0;
        int leidos;

        while ((leidos = await origen.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await archivo.WriteAsync(buffer.AsMemory(0, leidos), ct).ConfigureAwait(false);
            descargados += leidos;
            progreso?.Report(new ProgresoDeDescarga(descargados, total));
        }
    }

    private void BorrarSiExiste(string? ruta)
    {
        if (ruta is null)
        {
            return;
        }

        try
        {
            File.Delete(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.TechnicalError($"borrar el instalador descartado {ruta}", ex);
        }
    }

    public void Dispose() => _http.Dispose();
}
