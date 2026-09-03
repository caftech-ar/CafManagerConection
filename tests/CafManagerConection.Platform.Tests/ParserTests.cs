using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

public class DockerPsParserTests
{
    private const string Salida = """
        {"Command":"\"nginx -g 'daemon of…\"","CreatedAt":"2026-08-01 10:00:00 -0300","ID":"a1b2c3d4e5f6","Image":"nginx:alpine","Names":"web-proxy","Ports":"0.0.0.0:80->80/tcp, :::80->80/tcp","State":"running","Status":"Up 3 weeks"}
        {"Command":"docker-entrypoint.s…","CreatedAt":"2026-08-01 10:00:00 -0300","ID":"f6e5d4c3b2a1","Image":"postgres:16","Names":"app-db","Ports":"5432/tcp","State":"running","Status":"Up 3 weeks"}
        {"Command":"/bin/sh","CreatedAt":"2026-07-15 08:00:00 -0300","ID":"111222333444","Image":"busybox","Names":"viejo","Ports":"","State":"exited","Status":"Exited (0) 2 weeks ago"}
        """;

    [Fact]
    public void Lee_los_contenedores()
    {
        var c = DockerPsParser.Parse(Salida);

        Assert.Equal(3, c.Count);
        Assert.Equal("web-proxy", c[0].Name);
        Assert.Equal("nginx:alpine", c[0].Image);
    }

    [Fact]
    public void Distingue_los_que_estan_corriendo()
    {
        var c = DockerPsParser.Parse(Salida);

        Assert.True(c[0].IsRunning);
        Assert.False(c[2].IsRunning);
    }

    [Fact]
    public void Deja_solo_los_puertos_publicados_hacia_afuera()
    {
        var c = DockerPsParser.Parse(Salida);

        Assert.Single(c[0].PublishedPorts);
        Assert.Contains("80->80/tcp", c[0].PublishedPorts[0], StringComparison.Ordinal);
        Assert.Empty(c[1].PublishedPorts);
    }

    [Fact]
    public void Sin_campo_State_lo_deduce_del_Status()
    {
        const string viejo =
            """{"ID":"abc","Names":"x","Image":"y","Ports":"","Status":"Up 2 hours"}""";

        var c = DockerPsParser.Parse(viejo);

        Assert.True(c[0].IsRunning);
    }

    [Fact]
    public void Una_linea_corrupta_no_invalida_el_resto()
    {
        var conBasura = Salida + "\nesto no es json\n";

        var c = DockerPsParser.Parse(conBasura);

        Assert.Equal(3, c.Count);
    }

    [Fact]
    public void Una_salida_vacia_devuelve_lista_vacia()
    {
        Assert.Empty(DockerPsParser.Parse(string.Empty));
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var crlf = Salida.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        var c = DockerPsParser.Parse(crlf);
        var esperado = DockerPsParser.Parse(Salida);

        Assert.Equal(esperado.Count, c.Count);
        Assert.Equal(esperado.Select(x => x.Name), c.Select(x => x.Name));
        Assert.Equal(esperado.Select(x => x.Image), c.Select(x => x.Image));
    }
}

public class ComposeParserTests
{
    [Fact]
    public void Lee_los_proyectos_de_un_arreglo_json()
    {
        const string salida = """
            [{"Name":"miapp","Status":"running(3)","ConfigFiles":"/srv/miapp/docker-compose.yml"},
             {"Name":"monitoreo","Status":"running(2)","ConfigFiles":"/srv/monitoreo/compose.yaml"}]
            """;

        var p = ComposeParser.ParseProjects(salida);

        Assert.Equal(2, p.Count);
        Assert.Equal("miapp", p[0].Name);
        Assert.Equal("/srv/miapp/docker-compose.yml", p[0].FilePath);
    }

    [Fact]
    public void Lee_los_proyectos_en_formato_linea_por_objeto()
    {
        const string salida = """
            {"Name":"miapp","ConfigFiles":"/srv/miapp/docker-compose.yml"}
            {"Name":"otro","ConfigFiles":"/srv/otro/compose.yaml"}
            """;

        var p = ComposeParser.ParseProjects(salida);

        Assert.Equal(2, p.Count);
    }

    [Fact]
    public void Relaciona_cada_servicio_con_su_contenedor()
    {
        var contenedores = new[]
        {
            new ContainerInfo("1", "miapp-web-1", "nginx", "running", "Up", []),
            new ContainerInfo("2", "miapp-db-1", "postgres", "exited", "Exited", []),
        };

        var servicios = ComposeParser.Correlate("miapp", ["web", "db", "cache"], contenedores);

        Assert.Equal(3, servicios.Count);
        Assert.True(servicios[0].IsRunning);
        Assert.False(servicios[1].IsRunning);

        Assert.Null(servicios[2].ContainerName);
        Assert.False(servicios[2].IsRunning);
    }

    [Fact]
    public void Reconoce_tambien_la_convencion_vieja_con_guion_bajo()
    {
        var contenedores = new[]
        {
            new ContainerInfo("1", "miapp_web_1", "nginx", "running", "Up", []),
        };

        var servicios = ComposeParser.Correlate("miapp", ["web"], contenedores);

        Assert.True(servicios[0].IsRunning);
    }

    [Fact]
    public void Lee_la_lista_de_servicios_descartando_avisos()
    {
        const string salida = """
            WARN[0000] The "TAG" variable is not set. Defaulting to a blank string.
            web
            db
            cache
            """;

        var s = ComposeParser.ParseServices(salida);

        Assert.Equal(3, s.Count);
        Assert.DoesNotContain(s, x => x.StartsWith("WARN", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseServices_Tolera_CRLF_igual_que_LF()
    {
        const string salida = "web\ndb\ncache";
        var crlf = salida.Replace("\n", "\r\n");

        Assert.Equal(ComposeParser.ParseServices(salida), ComposeParser.ParseServices(crlf));
    }
}

public class NginxConfigParserTests
{
    private const string Config = """
        # configuration file /etc/nginx/nginx.conf:
        http {
            include /etc/nginx/sites-enabled/*;
        }

        # configuration file /etc/nginx/sites-enabled/sitio.conf:
        server {
            listen 80;
            listen [::]:80;
            server_name ejemplo.com www.ejemplo.com;
            root /var/www/ejemplo;

            location / {
                try_files $uri $uri/ =404;
            }
        }

        server {
            listen 443 ssl http2;
            server_name api.ejemplo.com;
            root /var/www/api;
        }
        """;

    [Fact]
    public void Lee_los_bloques_server()
    {
        var sitios = NginxConfigParser.Parse(Config);

        Assert.Equal(2, sitios.Count);
    }

    [Fact]
    public void Lee_los_nombres_de_servidor()
    {
        var sitios = NginxConfigParser.Parse(Config);

        Assert.Contains("ejemplo.com", sitios[0].ServerNames);
        Assert.Contains("www.ejemplo.com", sitios[0].ServerNames);
        Assert.Contains("api.ejemplo.com", sitios[1].ServerNames);
    }

    [Fact]
    public void Lee_los_puertos_en_escucha()
    {
        var sitios = NginxConfigParser.Parse(Config);

        Assert.Contains(80, sitios[0].ListenPorts);
        Assert.Contains(443, sitios[1].ListenPorts);
    }

    [Fact]
    public void Los_modificadores_de_listen_no_se_toman_como_puertos()
    {
        var sitios = NginxConfigParser.Parse(Config);

        Assert.Single(sitios[1].ListenPorts);
    }

    [Fact]
    public void Lee_la_raiz_de_documentos()
    {
        var sitios = NginxConfigParser.Parse(Config);

        Assert.Equal("/var/www/ejemplo", sitios[0].DocumentRoot);
    }

    [Fact]
    public void Registra_de_que_archivo_viene_cada_sitio()
    {
        var sitios = NginxConfigParser.Parse(Config);

        Assert.Equal("/etc/nginx/sites-enabled/sitio.conf", sitios[0].ConfigFile);
    }

    [Fact]
    public void El_bloque_location_anidado_no_cierra_el_server_antes_de_tiempo()
    {
        var sitios = NginxConfigParser.Parse(Config);

        Assert.NotNull(sitios[0].DocumentRoot);
    }

    [Fact]
    public void Una_configuracion_sin_server_no_rompe()
    {
        Assert.Empty(NginxConfigParser.Parse("http {\n  sendfile on;\n}"));
    }

    private const string ConfigRepartida = """
        # configuration file /etc/nginx/nginx.conf:
        user www-data;
        http {
            include /etc/nginx/conf.d/*.conf;
            include otros/*.conf;
        }

        # configuration file /etc/nginx/otros/capa.conf:
        server {
            listen 8443 ssl;
            server_name capa.interno;
            root /srv/capa;
        }

        # configuration file /etc/nginx/sites-enabled/api:
        server {
            listen 443 ssl;
            server_name api.example.com;
            location / { proxy_pass http://127.0.0.1:8080; }
        }
        """;

    [Fact]
    public void Lee_una_configuracion_repartida_en_varios_archivos()
    {
        var sitios = NginxConfigParser.Parse(ConfigRepartida);

        Assert.Equal(2, sitios.Count);
        Assert.Contains("capa.interno", sitios[0].ServerNames);
        Assert.Contains("api.example.com", sitios[1].ServerNames);
    }

    [Fact]
    public void Cada_sitio_repartido_recuerda_su_archivo()
    {
        var sitios = NginxConfigParser.Parse(ConfigRepartida);

        Assert.Equal("/etc/nginx/otros/capa.conf", sitios[0].ConfigFile);
        Assert.Equal("/etc/nginx/sites-enabled/api", sitios[1].ConfigFile);
    }

    [Fact]
    public void El_nginx_conf_sin_bloques_server_no_aporta_sitios()
    {
        var sitios = NginxConfigParser.Parse(ConfigRepartida);

        Assert.DoesNotContain(sitios, s => s.ConfigFile == "/etc/nginx/nginx.conf");
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var crlf = Config.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        var sitios = NginxConfigParser.Parse(crlf);
        var esperado = NginxConfigParser.Parse(Config);

        Assert.Equal(esperado.Count, sitios.Count);
        Assert.Equal(esperado[0].ConfigFile, sitios[0].ConfigFile);
        Assert.Equal(esperado[0].ServerNames, sitios[0].ServerNames);
        Assert.Equal(esperado[0].ListenPorts, sitios[0].ListenPorts);
        Assert.Equal(esperado[0].DocumentRoot, sitios[0].DocumentRoot);
    }
}

public class SupervisorStatusParserTests
{
    private const string Salida = """
        celery-worker                    RUNNING   pid 1234, uptime 3 days, 2:15:30
        gunicorn                         RUNNING   pid 1235, uptime 3 days, 2:15:29
        scraper                          FATAL     Exited too quickly (process log may have details)
        backup                           STOPPED   Aug 20 03:00 AM
        flaky                            BACKOFF   Exited too quickly
        """;

    [Fact]
    public void Lee_todos_los_procesos()
    {
        var p = SupervisorStatusParser.Parse(Salida);

        Assert.Equal(5, p.Count);
    }

    [Fact]
    public void Distingue_los_que_corren()
    {
        var p = SupervisorStatusParser.Parse(Salida);

        Assert.True(p[0].IsRunning);
        Assert.False(p[2].IsRunning);
    }

    [Fact]
    public void Marca_como_fallidos_los_estados_que_lo_ameritan()
    {
        var p = SupervisorStatusParser.Parse(Salida);

        Assert.True(p.First(x => x.Name == "scraper").HasFailed); 
        Assert.True(p.First(x => x.Name == "flaky").HasFailed); 
        Assert.False(p.First(x => x.Name == "backup").HasFailed); 
        Assert.False(p.First(x => x.Name == "gunicorn").HasFailed);
    }

    [Fact]
    public void Conserva_el_detalle_del_estado()
    {
        var p = SupervisorStatusParser.Parse(Salida);

        Assert.Contains("uptime", p[0].Detail!, StringComparison.Ordinal);
        Assert.Contains("Exited too quickly", p[2].Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_salida_vacia_devuelve_lista_vacia()
    {
        Assert.Empty(SupervisorStatusParser.Parse(string.Empty));
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var crlf = Salida.ReplaceLineEndings("\n").Replace("\n", "\r\n");

        var p = SupervisorStatusParser.Parse(crlf);
        var esperado = SupervisorStatusParser.Parse(Salida);

        Assert.Equal(esperado.Count, p.Count);
        Assert.Equal(esperado.Select(x => (x.Name, x.State, x.Detail)), p.Select(x => (x.Name, x.State, x.Detail)));
    }
}
