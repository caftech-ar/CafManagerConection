namespace CafManagerConection.Platform;

public enum ClaseDeAplicacion
{
    ServidorWeb,
    BaseDeDatos,
    Contenedor,
    AccesoRemoto,
    SupervisionDeProcesos,
    Aplicacion,
    Mensajeria,
    ServicioDelSistema,
}

/// <summary><c>Nombre</c> es cómo se la conoce, no cómo se llama su binario.</summary>
public sealed record AplicacionConocida(string Nombre, ClaseDeAplicacion Clase);

public static class AplicacionesConocidas
{
    // Se busca la clave contenida y no la igualdad: los nombres reales traen sufijos (mysqld, python3.11).
    private static readonly (string Clave, AplicacionConocida Aplicacion)[] Tabla =
    [
        ("nginx", new("nginx", ClaseDeAplicacion.ServidorWeb)),
        ("apache", new("Apache", ClaseDeAplicacion.ServidorWeb)),
        ("httpd", new("Apache", ClaseDeAplicacion.ServidorWeb)),
        ("caddy", new("Caddy", ClaseDeAplicacion.ServidorWeb)),
        ("traefik", new("Traefik", ClaseDeAplicacion.ServidorWeb)),
        ("haproxy", new("HAProxy", ClaseDeAplicacion.ServidorWeb)),

        ("postgres", new("PostgreSQL", ClaseDeAplicacion.BaseDeDatos)),
        ("postmaster", new("PostgreSQL", ClaseDeAplicacion.BaseDeDatos)),
        ("mysqld", new("MySQL", ClaseDeAplicacion.BaseDeDatos)),
        ("mariadb", new("MariaDB", ClaseDeAplicacion.BaseDeDatos)),
        ("mongod", new("MongoDB", ClaseDeAplicacion.BaseDeDatos)),
        ("redis", new("Redis", ClaseDeAplicacion.BaseDeDatos)),
        ("memcached", new("Memcached", ClaseDeAplicacion.BaseDeDatos)),
        ("influxd", new("InfluxDB", ClaseDeAplicacion.BaseDeDatos)),
        ("clickhouse", new("ClickHouse", ClaseDeAplicacion.BaseDeDatos)),
        ("elasticsearch", new("Elasticsearch", ClaseDeAplicacion.BaseDeDatos)),

        ("dockerd", new("Docker", ClaseDeAplicacion.Contenedor)),
        ("docker-proxy", new("Docker (publicación de puerto)", ClaseDeAplicacion.Contenedor)),
        ("containerd", new("containerd", ClaseDeAplicacion.Contenedor)),
        ("podman", new("Podman", ClaseDeAplicacion.Contenedor)),

        ("sshd", new("OpenSSH", ClaseDeAplicacion.AccesoRemoto)),
        ("xrdp", new("xrdp", ClaseDeAplicacion.AccesoRemoto)),
        ("vnc", new("VNC", ClaseDeAplicacion.AccesoRemoto)),

        ("supervisord", new("supervisord", ClaseDeAplicacion.SupervisionDeProcesos)),
        ("systemd", new("systemd", ClaseDeAplicacion.SupervisionDeProcesos)),

        ("python", new("Aplicación Python", ClaseDeAplicacion.Aplicacion)),
        ("gunicorn", new("Gunicorn (Python)", ClaseDeAplicacion.Aplicacion)),
        ("uwsgi", new("uWSGI (Python)", ClaseDeAplicacion.Aplicacion)),
        ("uvicorn", new("Uvicorn (Python)", ClaseDeAplicacion.Aplicacion)),
        ("node", new("Aplicación Node.js", ClaseDeAplicacion.Aplicacion)),
        ("java", new("Aplicación Java", ClaseDeAplicacion.Aplicacion)),
        ("dotnet", new("Aplicación .NET", ClaseDeAplicacion.Aplicacion)),
        ("ruby", new("Aplicación Ruby", ClaseDeAplicacion.Aplicacion)),
        ("php", new("PHP", ClaseDeAplicacion.Aplicacion)),
        ("beam", new("Erlang/Elixir", ClaseDeAplicacion.Aplicacion)),

        ("rabbitmq", new("RabbitMQ", ClaseDeAplicacion.Mensajeria)),
        ("mosquitto", new("Mosquitto (MQTT)", ClaseDeAplicacion.Mensajeria)),

        ("cupsd", new("Impresión (CUPS)", ClaseDeAplicacion.ServicioDelSistema)),
        ("smbd", new("Samba", ClaseDeAplicacion.ServicioDelSistema)),
        ("nmbd", new("Samba", ClaseDeAplicacion.ServicioDelSistema)),
        ("chronyd", new("Reloj (chrony)", ClaseDeAplicacion.ServicioDelSistema)),
        ("ntpd", new("Reloj (NTP)", ClaseDeAplicacion.ServicioDelSistema)),
        ("named", new("DNS (BIND)", ClaseDeAplicacion.ServicioDelSistema)),
        ("dnsmasq", new("DNS (dnsmasq)", ClaseDeAplicacion.ServicioDelSistema)),
        ("dovecot", new("Dovecot", ClaseDeAplicacion.ServicioDelSistema)),
        ("snmpd", new("SNMP", ClaseDeAplicacion.ServicioDelSistema)),
        ("rpcbind", new("rpcbind", ClaseDeAplicacion.ServicioDelSistema)),
    ];

    /// <summary>Gana la clave más larga que coincida: <c>postmaster</c> contiene <c>master</c>.</summary>
    public static AplicacionConocida? Reconocer(string? proceso)
    {
        if (string.IsNullOrWhiteSpace(proceso))
        {
            return null;
        }

        var texto = proceso.ToLowerInvariant();

        AplicacionConocida? mejor = null;
        var largoDelMejor = 0;

        foreach (var (clave, aplicacion) in Tabla)
        {
            if (clave.Length > largoDelMejor
                && texto.Contains(clave, StringComparison.Ordinal))
            {
                mejor = aplicacion;
                largoDelMejor = clave.Length;
            }
        }

        return mejor;
    }
}
