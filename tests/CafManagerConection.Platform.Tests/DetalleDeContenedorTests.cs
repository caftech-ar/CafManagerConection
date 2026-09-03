using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public sealed class DetalleDeContenedorTests
{
    private const string Salida = """
        cmc:resumen
        /inventario-api|registry.example/inventario-api:2.14|running|3|2026-08-23T11:04:18.442Z|unless-stopped|/app|dotnet Inventario.Api.dll|9f2c1d4e8a7b3c5d6e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d|sha256:1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b|2026-08-20T09:15:00.000Z
        cmc:salud
        healthy
        cmc:compose
        inventario|api
        cmc:redes
        inventario_default -> 192.0.2.4
        bridge
        cmc:puertos
        8080/tcp -> 0.0.0.0:18080
        8080/tcp -> [::]:18080
        cmc:volumenes
        /srv/inventario/config -> /app/config (rw)
        /var/log/inventario -> /app/logs (rw)
        cmc:registro
        warn: no se pudo resolver el host de correo
        info: escuchando en 0.0.0.0:8080
        cmc:consumo
        2.47%|486.3MiB / 2GiB|23.75%|1.2GB / 340MB|0B / 12.3MB|31
        """;

    private static DetalleDeContenedor Leer(string salida = Salida) =>
        DetalleDeContenedor.Interpretar("inventario-api", salida);

    [Fact]
    public void Se_lee_el_resumen()
    {
        var d = Leer();

        Assert.Equal("inventario-api", d.Nombre);
        Assert.Equal("registry.example/inventario-api:2.14", d.Imagen);
        Assert.Equal("running", d.Estado);
        Assert.Equal("healthy", d.Salud);
        Assert.Equal("unless-stopped", d.Politica);
        Assert.Equal("/app", d.Directorio);
        Assert.Equal("dotnet Inventario.Api.dll", d.Comando);
    }

    [Fact]
    public void Al_nombre_se_le_saca_la_barra_de_docker() =>
        Assert.Equal("inventario-api", Leer().Nombre);

    [Fact]
    public void Se_leen_los_reinicios() => Assert.Equal(3, Leer().Reinicios);

    [Fact]
    public void Se_lee_desde_cuando_corre()
    {
        var d = Leer();

        Assert.NotNull(d.Desde);
        Assert.Equal(2026, d.Desde!.Value.Year);
        Assert.NotNull(d.Uptime);
    }

    [Fact]
    public void Sin_chequeo_de_salud_no_se_muestra_la_cadena_de_docker()
    {
        var d = Leer(Salida.Replace(
            "cmc:salud\nhealthy", "cmc:salud\n<no value>", StringComparison.Ordinal));

        Assert.Null(d.Salud);
        Assert.Equal("running", d.Estado);
    }

    [Fact]
    public void Sin_politica_de_reinicio_queda_nula()
    {
        var d = Leer(Salida.Replace("|unless-stopped|", "|no|", StringComparison.Ordinal));

        Assert.Null(d.Politica);
    }

    [Fact]
    public void Se_lee_el_consumo()
    {
        var d = Leer();

        Assert.Equal("2.47%", d.Cpu);
        Assert.Equal("486.3MiB / 2GiB", d.Memoria);
        Assert.Equal("23.75%", d.MemoriaPorcentaje);
        Assert.Equal("1.2GB / 340MB", d.Red);
        Assert.Equal("0B / 12.3MB", d.Disco);
        Assert.Equal("31", d.Procesos);
    }

    [Fact]
    public void Se_leen_los_puertos()
    {
        var puertos = Leer().Puertos;

        Assert.Equal(2, puertos.Count);
        Assert.Contains("8080/tcp -> 0.0.0.0:18080", puertos);
    }

    [Fact]
    public void Se_leen_los_volumenes()
    {
        var volumenes = Leer().Volumenes;

        Assert.Equal(2, volumenes.Count);
        Assert.Contains("/srv/inventario/config -> /app/config (rw)", volumenes);
    }

    [Fact]
    public void Se_lee_el_registro()
    {
        var registro = Leer().Registro;

        Assert.Contains("escuchando en 0.0.0.0:8080", registro, StringComparison.Ordinal);
        Assert.Contains("no se pudo resolver", registro, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_puertos_el_resto_de_la_ficha_sigue()
    {
        var sinPuertos = Salida.Replace(
            "8080/tcp -> 0.0.0.0:18080\n8080/tcp -> [::]:18080\n",
            string.Empty,
            StringComparison.Ordinal);

        var d = DetalleDeContenedor.Interpretar("inventario-api", sinPuertos);

        Assert.Empty(d.Puertos);
        Assert.Equal("running", d.Estado);
        Assert.Equal("2.47%", d.Cpu);
    }

    [Fact]
    public void Una_salida_vacia_no_rompe()
    {
        var d = DetalleDeContenedor.Interpretar("x", string.Empty);

        Assert.False(d.TieneAlgo);
        Assert.Empty(d.Puertos);
        Assert.Empty(d.Volumenes);
        Assert.Null(d.Desde);
    }

    [Fact]
    public void Una_salida_sin_marcas_no_rompe()
    {
        var d = DetalleDeContenedor.Interpretar("x", "bash: docker: command not found");

        Assert.False(d.TieneAlgo);
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var crlf = Salida.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        var d = DetalleDeContenedor.Interpretar("inventario-api", crlf);
        var esperado = Leer();

        Assert.Equal(esperado.Nombre, d.Nombre);
        Assert.Equal(esperado.Imagen, d.Imagen);
        Assert.Equal(esperado.Estado, d.Estado);
        Assert.Equal(esperado.Politica, d.Politica);
        Assert.Equal(esperado.Comando, d.Comando);
        Assert.Equal(esperado.Puertos, d.Puertos);
        Assert.Equal(esperado.Volumenes, d.Volumenes);
        Assert.Equal(esperado.Cpu, d.Cpu);
        Assert.Equal(esperado.Memoria, d.Memoria);
    }

    [Fact]
    public void Con_datos_dice_que_tiene_algo() => Assert.True(Leer().TieneAlgo);

    [Theory]
    [InlineData("inventario-api", true)]
    [InlineData("redis", true)]
    [InlineData("proyecto_web.1", true)]
    [InlineData("a1b2c3d4e5f6", true)]
    [InlineData("api; rm -rf /", false)]
    [InlineData("api && whoami", false)]
    [InlineData("$(whoami)", false)]
    [InlineData("api`id`", false)]
    [InlineData("-flag", false)]
    [InlineData(".oculto", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Solo_se_aceptan_nombres_de_contenedor(string? nombre, bool esperado) =>
        Assert.Equal(esperado, ControlDeDocker.EsNombreValido(nombre));

    [Theory]
    [InlineData("    api")]
    [InlineData("  inventario-web")]
    [InlineData("api ")]
    public void Un_nombre_con_sangria_no_es_un_contenedor(string mostrado) =>
        Assert.False(ControlDeDocker.EsNombreValido(mostrado));

    [Theory]
    [InlineData("inventario_inventario-api_1")]
    [InlineData("inventario-inventario-api-1")]
    [InlineData("proyecto_db_1")]
    public void El_nombre_real_de_compose_es_valido(string real) =>
        Assert.True(ControlDeDocker.EsNombreValido(real));

    // FR-150c

    [Fact]
    public void El_identificador_se_corta_a_doce() =>
        Assert.Equal("9f2c1d4e8a7b", Leer().Id);

    [Fact]
    public void El_digest_va_sin_el_prefijo_del_algoritmo() =>
        Assert.Equal("1a2b3c4d5e6f", Leer().Digest);

    [Fact]
    public void La_fecha_de_creacion_se_interpreta() =>
        Assert.Equal(
            new DateTimeOffset(2026, 8, 20, 9, 15, 0, TimeSpan.Zero),
            Leer().Creado);

    [Fact]
    public void El_proyecto_y_el_servicio_de_compose_se_leen()
    {
        var d = Leer();

        Assert.Equal("inventario", d.Proyecto);
        Assert.Equal("api", d.Servicio);
    }

    [Fact]
    public void Las_redes_se_listan_con_su_direccion_cuando_la_hay()
    {
        var redes = Leer().Redes;

        Assert.Equal(2, redes.Count);
        Assert.Contains("inventario_default -> 192.0.2.4", redes);
        Assert.Contains("bridge", redes);
    }

    [Fact]
    public void Sin_etiquetas_de_compose_los_campos_quedan_vacios()
    {
        const string suelto = """
            cmc:resumen
            /redis|redis:7|running|0|2026-08-23T11:04:18.442Z|no|/data|redis-server|abc123def456789|sha256:ffee|2026-08-01T00:00:00.000Z
            cmc:salud
            cmc:compose
            |
            cmc:registro
            """;

        var d = DetalleDeContenedor.Interpretar("redis", suelto);

        Assert.Null(d.Proyecto);
        Assert.Null(d.Servicio);
        Assert.Null(d.Salud);
        Assert.Equal("abc123def456", d.Id);
    }

    /// <remarks>
    /// docker inspect aborta la plantilla entera si un campo no existe, en vez de dejarlo vacío:
    /// <c>template parsing error: ... at &lt;.State.Health.Status&gt;: map has no entry for key "Health"</c>.
    /// </remarks>
    [Fact]
    public void Un_contenedor_sin_healthcheck_no_pierde_el_resto_de_la_ficha()
    {
        const string sinSalud = """
            cmc:resumen
            /catalogo-frontend-1|catalogo-frontend:latest|running|0|2026-08-31T20:11:05.120Z|unless-stopped||nginx -g daemon off;|7c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d|sha256:9f8e7d6c5b4a39281706f5e4d3c2b1a09f8e7d6c5b4a39281706f5e4d3c2b1a0|2026-08-30T18:00:00.000Z
            cmc:salud
            cmc:compose
            catalogo|frontend
            cmc:redes
            catalogo_default -> 192.0.2.3
            cmc:puertos
            80/tcp -> 0.0.0.0:8089
            cmc:volumenes
            cmc:registro
            192.0.2.1 - - [31/Aug/2026:23:11:52 +0000] "POST /api/v1/firmantes/consulta HTTP/1.1" 200 15046
            cmc:consumo
            0.03%|12.4MiB / 1.94GiB|0.62%|1.5MB / 8.2MB|0B / 4.1kB|3
            """;

        var d = DetalleDeContenedor.Interpretar("catalogo-frontend-1", sinSalud);

        Assert.Equal("catalogo-frontend:latest", d.Imagen);
        Assert.Equal("running", d.Estado);
        Assert.Equal("7c1d2e3f4a5b", d.Id);
        Assert.Equal("9f8e7d6c5b4a", d.Digest);
        Assert.Equal("unless-stopped", d.Politica);
        Assert.Equal("nginx -g daemon off;", d.Comando);
        Assert.Equal("catalogo", d.Proyecto);
        Assert.Equal("frontend", d.Servicio);
        Assert.Contains("catalogo_default -> 192.0.2.3", d.Redes);
        Assert.Contains("80/tcp -> 0.0.0.0:8089", d.Puertos);
        Assert.NotEmpty(d.Registro);
        Assert.Equal("0.03%", d.Cpu);

        Assert.Null(d.Salud);
    }

    [Fact]
    public void Un_tramo_vacio_no_arrastra_a_los_demas()
    {
        const string sinVolumenes = """
            cmc:resumen
            /web|nginx:latest|running|0|2026-08-31T20:11:05.120Z|no||nginx|aabbccddeeff0011|sha256:112233445566|2026-08-30T18:00:00.000Z
            cmc:volumenes
            cmc:puertos
            80/tcp -> 0.0.0.0:8089
            """;

        var d = DetalleDeContenedor.Interpretar("web", sinVolumenes);

        Assert.Empty(d.Volumenes);
        Assert.Equal("nginx:latest", d.Imagen);
        Assert.Single(d.Puertos);
    }

    // FR-150e

    [Fact]
    public void Un_registro_vacio_pero_leido_se_distingue_de_uno_que_no_se_pudo_leer()
    {
        const string conMarcaVacia = """
            cmc:resumen
            /redis|redis:7|running|<no value>|0|2026-08-23T11:04:18.442Z|no|/data|redis-server
            cmc:registro
            cmc:consumo
            0.00%|1MiB / 2GiB|0.05%|0B / 0B|0B / 0B|1
            """;

        const string sinMarca = """
            cmc:resumen
            /redis|redis:7|running|<no value>|0|2026-08-23T11:04:18.442Z|no|/data|redis-server
            """;

        var leido = DetalleDeContenedor.Interpretar("redis", conMarcaVacia);
        var noLeido = DetalleDeContenedor.Interpretar("redis", sinMarca);

        Assert.True(leido.RegistroLeido);
        Assert.Empty(leido.Registro);

        Assert.False(noLeido.RegistroLeido);
    }

    [Fact]
    public void Un_registro_con_lineas_queda_marcado_como_leido() =>
        Assert.True(Leer().RegistroLeido);
}
