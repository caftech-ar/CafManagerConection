using System.Security.Cryptography;
using CafManagerConection.Infrastructure.Actualizaciones;
using CafManagerConection.Infrastructure.Configuration;

namespace CafManagerConection.Infrastructure.Tests.Actualizaciones;

// FR-161 y FR-161a.
public sealed class DescargadorDeInstaladorTests : IDisposable
{
    private const string ContenidoDelInstalador = "contenido de prueba del instalador";

    private readonly string _raiz;
    private readonly AppPaths _rutas;

    public DescargadorDeInstaladorTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "cmc-actualizaciones-" + Guid.NewGuid().ToString("N"));
        _rutas = new AppPaths(_raiz);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_raiz, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static string HashDe(string contenido) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(contenido)));

    private static InformacionDeRelease ReleaseCon(params ActivoDeRelease[] activos) =>
        new("v1.4.0", "CMC 1.4.0", null, DateTimeOffset.UtcNow, activos);

    private static ActivoDeRelease Instalador() => new(
        "cmc-setup.exe", "https://github.com/operador/cmc/releases/download/v1.4.0/cmc-setup.exe");

    [Fact]
    public async Task Descarga_y_verifica_cuando_el_hash_coincide_via_asset_sha256()
    {
        var instalador = Instalador();
        var hashCorrecto = HashDe(ContenidoDelInstalador);
        var activoHash = new ActivoDeRelease(
            instalador.Nombre + ".sha256", "https://ejemplo/cmc-setup.exe.sha256");

        var manejador = new ManejadorFalso(peticion =>
            peticion.RequestUri!.ToString() == instalador.UrlDeDescarga
                ? RespuestaHttp.Bytes(System.Text.Encoding.UTF8.GetBytes(ContenidoDelInstalador))
                : RespuestaHttp.Texto($"{hashCorrecto}  cmc-setup.exe\n"));

        using var descargador = new DescargadorDeInstalador(_rutas, manejador);

        var resultado = await descargador.DescargarYVerificarAsync(
            ReleaseCon(instalador, activoHash), instalador);

        Assert.Equal(EstadoDeDescarga.Verificada, resultado.Estado);
        Assert.NotNull(resultado.RutaArchivo);
        Assert.True(File.Exists(resultado.RutaArchivo));
        Assert.Equal(ContenidoDelInstalador, await File.ReadAllTextAsync(resultado.RutaArchivo!));
    }

    [Fact]
    public async Task Descarga_y_verifica_leyendo_el_hash_del_cuerpo_de_la_release_si_no_hay_asset()
    {
        var instalador = Instalador();
        var hashCorrecto = HashDe(ContenidoDelInstalador);

        var manejador = ManejadorFalso.Fijo(
            RespuestaHttp.Bytes(System.Text.Encoding.UTF8.GetBytes(ContenidoDelInstalador)));

        using var descargador = new DescargadorDeInstalador(_rutas, manejador);

        var release = new InformacionDeRelease(
            "v1.4.0", "CMC 1.4.0", $"Novedades.\n\nSHA-256: {hashCorrecto}", DateTimeOffset.UtcNow,
            [instalador]);

        var resultado = await descargador.DescargarYVerificarAsync(release, instalador);

        Assert.Equal(EstadoDeDescarga.Verificada, resultado.Estado);
    }

    [Fact]
    public async Task El_archivo_se_borra_cuando_el_hash_no_coincide()
    {
        var instalador = Instalador();
        var hashDeOtraCosa = HashDe("esto no es lo que se descargó");

        var manejador = ManejadorFalso.Fijo(
            RespuestaHttp.Bytes(System.Text.Encoding.UTF8.GetBytes(ContenidoDelInstalador)));

        using var descargador = new DescargadorDeInstalador(_rutas, manejador);

        var release = new InformacionDeRelease(
            "v1.4.0", "CMC 1.4.0", $"SHA-256: {hashDeOtraCosa}", DateTimeOffset.UtcNow, [instalador]);

        var resultado = await descargador.DescargarYVerificarAsync(release, instalador);

        Assert.Equal(EstadoDeDescarga.HashNoCoincide, resultado.Estado);
        Assert.Null(resultado.RutaArchivo);

        var rutaEsperada = Path.Combine(descargador.CarpetaDeDescargas, instalador.Nombre);
        Assert.False(File.Exists(rutaEsperada));
    }

    [Fact]
    public async Task La_verificacion_del_hash_detecta_una_mutacion_que_la_deshabilite()
    {
        var instalador = Instalador();

        var manejador = ManejadorFalso.Fijo(
            RespuestaHttp.Bytes(System.Text.Encoding.UTF8.GetBytes(ContenidoDelInstalador)));

        using var descargador = new DescargadorDeInstalador(_rutas, manejador);

        // 64 'f' es un hash válido en forma pero, con certeza, no el de ContenidoDelInstalador.
        var release = new InformacionDeRelease(
            "v1.4.0", "CMC 1.4.0", $"SHA-256: {new string('f', 64)}", DateTimeOffset.UtcNow,
            [instalador]);

        var resultado = await descargador.DescargarYVerificarAsync(release, instalador);

        Assert.NotEqual(EstadoDeDescarga.Verificada, resultado.Estado);
        Assert.Equal(EstadoDeDescarga.HashNoCoincide, resultado.Estado);
    }

    [Fact]
    public async Task Sin_hash_publicado_no_se_descarga_nada_y_se_informa()
    {
        var instalador = Instalador();
        var manejador = new ManejadorFalso(_ =>
            throw new InvalidOperationException("No debería pedir nada por la red."));

        using var descargador = new DescargadorDeInstalador(_rutas, manejador);

        var resultado = await descargador.DescargarYVerificarAsync(
            ReleaseCon(instalador), instalador);

        Assert.Equal(EstadoDeDescarga.SinHashPublicado, resultado.Estado);
        Assert.Null(resultado.RutaArchivo);
        Assert.NotNull(resultado.Motivo);
    }

    [Fact]
    public async Task Un_fallo_de_red_al_descargar_no_lanza_y_vuelve_como_fallo()
    {
        var instalador = Instalador();
        var hashCorrecto = HashDe(ContenidoDelInstalador);

        var manejador = new ManejadorFalso(peticion =>
            peticion.RequestUri!.ToString() == instalador.UrlDeDescarga
                ? throw new HttpRequestException("sin red")
                : RespuestaHttp.Texto(hashCorrecto));

        using var descargador = new DescargadorDeInstalador(_rutas, manejador);

        var release = new InformacionDeRelease(
            "v1.4.0", "CMC 1.4.0", $"SHA-256: {hashCorrecto}", DateTimeOffset.UtcNow, [instalador]);

        var resultado = await descargador.DescargarYVerificarAsync(release, instalador);

        Assert.Equal(EstadoDeDescarga.Fallo, resultado.Estado);
        Assert.Null(resultado.RutaArchivo);
    }

    [Fact]
    public async Task Informa_el_progreso_de_la_descarga()
    {
        var instalador = Instalador();
        var hashCorrecto = HashDe(ContenidoDelInstalador);

        var manejador = new ManejadorFalso(peticion =>
            peticion.RequestUri!.ToString() == instalador.UrlDeDescarga
                ? RespuestaHttp.Bytes(System.Text.Encoding.UTF8.GetBytes(ContenidoDelInstalador))
                : RespuestaHttp.Texto(hashCorrecto));

        using var descargador = new DescargadorDeInstalador(_rutas, manejador);

        var release = new InformacionDeRelease(
            "v1.4.0", "CMC 1.4.0", $"SHA-256: {hashCorrecto}", DateTimeOffset.UtcNow, [instalador]);

        var avisos = new List<ProgresoDeDescarga>();
        var progreso = new Progress<ProgresoDeDescarga>(avisos.Add);

        var resultado = await descargador.DescargarYVerificarAsync(release, instalador, progreso);

        Assert.Equal(EstadoDeDescarga.Verificada, resultado.Estado);
        // Progress<T> despacha al SynchronizationContext: puede llegar después de que el método termine.
        await Task.Delay(50);
        Assert.NotEmpty(avisos);
        Assert.Equal(ContenidoDelInstalador.Length, avisos[^1].BytesDescargados);
    }
}
