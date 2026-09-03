using System.Net;
using CafManagerConection.Infrastructure.Actualizaciones;

namespace CafManagerConection.Infrastructure.Tests.Actualizaciones;

// FR-159 y FR-159a.
public sealed class ConsultorDeReleasesTests
{
    private const string JsonDeReleaseReal = """
        {
          "tag_name": "v1.4.0",
          "name": "CafManagerConection 1.4.0",
          "body": "### Novedades\n- Se agregó el aviso de nuevas versiones.\n- Correcciones varias.",
          "published_at": "2026-06-01T10:15:00Z",
          "assets": [
            {
              "name": "CafManagerConection-1.4.0-setup.exe",
              "browser_download_url": "https://github.com/operador/cmc/releases/download/v1.4.0/CafManagerConection-1.4.0-setup.exe"
            },
            {
              "name": "CafManagerConection-1.4.0-setup.exe.sha256",
              "browser_download_url": "https://github.com/operador/cmc/releases/download/v1.4.0/CafManagerConection-1.4.0-setup.exe.sha256"
            }
          ]
        }
        """;

    [Fact]
    public async Task Trae_version_novedades_y_activos_de_un_json_de_release_real()
    {
        using var manejador = ManejadorFalso.Fijo(RespuestaHttp.Json(JsonDeReleaseReal));
        using var consultor = new ConsultorDeReleases(manejador);

        var resultado = await consultor.UltimaReleaseAsync("operador", "cmc");

        Assert.True(resultado.Exito);
        Assert.Equal("v1.4.0", resultado.Release!.Version);
        Assert.Equal("CafManagerConection 1.4.0", resultado.Release.Nombre);
        Assert.Contains("aviso de nuevas versiones", resultado.Release.Novedades);
        Assert.Equal(
            new DateTimeOffset(2026, 6, 1, 10, 15, 0, TimeSpan.Zero), resultado.Release.PublicadoEl);
        Assert.Equal(2, resultado.Release.Activos.Count);
        Assert.Equal("CafManagerConection-1.4.0-setup.exe", resultado.Release.Activos[0].Nombre);
    }

    [Fact]
    public async Task Una_release_sin_assets_trae_la_lista_vacia_y_no_falla()
    {
        const string json = """
            {
              "tag_name": "v1.4.0",
              "name": "CafManagerConection 1.4.0",
              "body": "Sin instalador adjunto todavía.",
              "published_at": "2026-06-01T10:15:00Z",
              "assets": []
            }
            """;

        using var manejador = ManejadorFalso.Fijo(RespuestaHttp.Json(json));
        using var consultor = new ConsultorDeReleases(manejador);

        var resultado = await consultor.UltimaReleaseAsync("operador", "cmc");

        Assert.True(resultado.Exito);
        Assert.Empty(resultado.Release!.Activos);
    }

    [Fact]
    public async Task Un_json_corrupto_no_lanza_y_vuelve_como_no_se_pudo()
    {
        using var manejador = ManejadorFalso.Fijo(RespuestaHttp.Json("{ esto no es json válido"));
        using var consultor = new ConsultorDeReleases(manejador);

        var resultado = await consultor.UltimaReleaseAsync("operador", "cmc");

        Assert.False(resultado.Exito);
        Assert.Null(resultado.Release);
        Assert.NotNull(resultado.Motivo);
    }

    [Fact]
    public async Task Un_json_sin_tag_name_no_lanza_y_vuelve_como_no_se_pudo()
    {
        using var manejador = ManejadorFalso.Fijo(RespuestaHttp.Json("""{ "body": "sin tag" }"""));
        using var consultor = new ConsultorDeReleases(manejador);

        var resultado = await consultor.UltimaReleaseAsync("operador", "cmc");

        Assert.False(resultado.Exito);
        Assert.NotNull(resultado.Motivo);
    }

    [Fact]
    public async Task Un_404_significa_que_el_repositorio_no_tiene_releases()
    {
        using var manejador = ManejadorFalso.Fijo(RespuestaHttp.Estado(HttpStatusCode.NotFound));
        using var consultor = new ConsultorDeReleases(manejador);

        var resultado = await consultor.UltimaReleaseAsync("operador", "cmc");

        Assert.False(resultado.Exito);
        Assert.Contains("no tiene releases", resultado.Motivo);
    }

    [Fact]
    public async Task Un_403_de_limite_de_consultas_no_lanza_y_avisa_del_limite()
    {
        using var manejador = ManejadorFalso.Fijo(RespuestaHttp.Estado(HttpStatusCode.Forbidden));
        using var consultor = new ConsultorDeReleases(manejador);

        var resultado = await consultor.UltimaReleaseAsync("operador", "cmc");

        Assert.False(resultado.Exito);
        Assert.Contains("límite", resultado.Motivo);
    }

    [Fact]
    public async Task Un_tiempo_de_espera_agotado_no_lanza_y_avisa_que_no_contestó()
    {
        using var manejador = ManejadorFalso.QueNuncaContesta();
        using var consultor = new ConsultorDeReleases(
            manejador, tiempoDeEspera: TimeSpan.FromMilliseconds(50));

        var resultado = await consultor.UltimaReleaseAsync("operador", "cmc");

        Assert.False(resultado.Exito);
        Assert.NotNull(resultado.Motivo);
    }

    [Fact]
    public async Task No_manda_ningun_dato_que_identifique_al_usuario_ni_al_equipo()
    {
        using var manejador = ManejadorFalso.Fijo(RespuestaHttp.Json(JsonDeReleaseReal));
        using var consultor = new ConsultorDeReleases(manejador);

        await consultor.UltimaReleaseAsync("operador", "cmc");

        var peticion = manejador.UltimaPeticion!;

        Assert.Null(peticion.Headers.Authorization);

        Assert.Equal(
            ConsultorDeReleases.NombreDelProducto,
            string.Join(' ', peticion.Headers.UserAgent.Select(p => p.Product?.Name)));
        Assert.DoesNotContain(
            peticion.Headers.UserAgent, p => p.Product?.Version is not null);

        Assert.True(string.IsNullOrEmpty(peticion.RequestUri!.Query));
        Assert.DoesNotContain(Environment.MachineName, peticion.RequestUri.ToString());
        Assert.DoesNotContain(Environment.UserName, peticion.RequestUri.ToString());
    }
}
