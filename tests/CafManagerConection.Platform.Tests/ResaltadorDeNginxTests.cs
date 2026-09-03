using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

// FR-101a, FR-101b
public sealed class ResaltadorDeNginxTests
{
    private const string Config = """
        # sitio de ejemplo
        server {
            listen 80;
            listen 443 ssl;
            server_name ejemplo.com www.ejemplo.com;
            root /var/www/ejemplo;

            location /api/ {
                proxy_pass http://127.0.0.1:3000;
                proxy_set_header Host $host;
                proxy_read_timeout 30s;
                client_max_body_size 20m;
            }

            error_page 500 502 503 504 /50x.html;
            access_log /var/log/nginx/ejemplo.access.log combined;
            add_header X-Frame-Options "SAMEORIGIN";
        }
        """;

    private static TipoDeTramo TipoDe(string texto, string fragmento)
    {
        var i = texto.IndexOf(fragmento, StringComparison.Ordinal);
        Assert.True(i >= 0, $"«{fragmento}» no está en el texto de prueba.");

        var tramos = ResaltadorDeNginx.Analizar(texto);

        return tramos.First(t => t.Desde == i && t.Largo == fragmento.Length).Tipo;
    }

    // SC-026
    [Theory]
    [InlineData(Config)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("server{listen 80;}")]
    [InlineData("# solo un comentario sin salto")]
    [InlineData("add_header X \"cadena sin cerrar\ndirectiva_siguiente on;")]
    [InlineData("weird\t\t   \n\n\n   spacing;")]
    [InlineData("directiva 'comilla simple' \"doble\" $var;")]
    public void Reconstruir_los_tramos_devuelve_el_texto_exacto(string texto)
    {
        var tramos = ResaltadorDeNginx.Analizar(texto);

        Assert.Equal(texto, ResaltadorDeNginx.Reconstruir(texto, tramos));
    }

    [Fact]
    public void Los_tramos_van_en_orden_y_sin_superponerse()
    {
        var tramos = ResaltadorDeNginx.Analizar(Config);
        var esperado = 0;

        foreach (var t in tramos)
        {
            Assert.Equal(esperado, t.Desde);
            Assert.True(t.Largo > 0, "un tramo de largo cero no pinta nada y rompe el recorrido.");
            esperado = t.Desde + t.Largo;
        }

        Assert.Equal(Config.Length, esperado);
    }

    [Fact]
    public void Los_comentarios_se_reconocen() =>
        Assert.Equal(TipoDeTramo.Comentario, TipoDe(Config, "# sitio de ejemplo"));

    [Theory]
    [InlineData("server")]
    [InlineData("location")]
    public void Los_bloques_se_distinguen_de_las_directivas(string bloque) =>
        Assert.Equal(TipoDeTramo.Bloque, TipoDe(Config, bloque));

    [Theory]
    [InlineData("listen")]
    [InlineData("proxy_pass")]
    [InlineData("client_max_body_size")]
    [InlineData("access_log")]
    public void Las_directivas_se_reconocen(string directiva) =>
        Assert.Equal(TipoDeTramo.Directiva, TipoDe(Config, directiva));

    [Fact]
    public void Las_variables_se_reconocen() =>
        Assert.Equal(TipoDeTramo.Variable, TipoDe(Config, "$host"));

    [Fact]
    public void Las_cadenas_entre_comillas_se_reconocen() =>
        Assert.Equal(TipoDeTramo.Cadena, TipoDe(Config, "\"SAMEORIGIN\""));

    [Theory]
    [InlineData("80")]
    [InlineData("30s")]
    [InlineData("20m")]
    public void Los_numeros_con_y_sin_unidad_se_reconocen(string numero) =>
        Assert.Equal(TipoDeTramo.Numero, TipoDe(Config, numero));

    [Theory]
    [InlineData("http://127.0.0.1:3000;")]
    [InlineData("ejemplo.com")]
    public void Las_direcciones_no_se_parten_como_numeros(string fragmento) =>
        Assert.NotEqual(TipoDeTramo.Numero, TipoDe(Config, fragmento.TrimEnd(';')));

    [Fact]
    public void Un_valor_no_se_pinta_como_directiva() =>
        Assert.NotEqual(TipoDeTramo.Directiva, TipoDe(Config, "ssl"));

    [Fact]
    public void Tras_el_punto_y_coma_vuelve_a_haber_directiva()
    {
        const string enUnaLinea = "listen 80; root /var/www;";

        Assert.Equal(TipoDeTramo.Directiva, TipoDe(enUnaLinea, "root"));
    }

    [Fact]
    public void Un_texto_vacio_no_da_tramos() =>
        Assert.Empty(ResaltadorDeNginx.Analizar(""));
}
