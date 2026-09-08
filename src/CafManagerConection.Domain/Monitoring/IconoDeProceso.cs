namespace CafManagerConection.Domain.Monitoring;

/// <summary>Qué icono le corresponde a un proceso del servidor cuando se lo reconoce. Sin coincidencia devuelve <c>null</c>: mostrar un icono genérico a todo hace que ninguno signifique nada.</summary>
public static class IconoDeProceso
{
    // El nombre que trae /proc/<pid>/stat es el del ejecutable, sin ruta y a lo sumo 15 caracteres.
    private static readonly (string Nombre, string Clave)[] Conocidos =
    [
        ("dockerd", "IconoPanelDocker"),
        ("containerd", "IconoPanelDocker"),
        ("containerd-shim", "IconoPanelDocker"),
        ("docker-proxy", "IconoPanelDocker"),
        ("nginx", "IconoPanelNginx"),
        ("supervisord", "IconoPanelSupervisor"),
        ("sshd", "IconoSsh"),
        ("systemd", "IconoAjustes"),
        ("systemd-journal", "IconoPanelEstado"),
        ("systemd-logind", "IconoAjustes"),
        ("systemd-udevd", "IconoAjustes"),
        ("cron", "IconoAjustes"),
        ("crond", "IconoAjustes"),
        ("rsyslogd", "IconoPanelEstado"),
        ("mysqld", "IconoBaseDeDatos"),
        ("mariadbd", "IconoBaseDeDatos"),
        ("postgres", "IconoBaseDeDatos"),
        ("mongod", "IconoBaseDeDatos"),
        ("redis-server", "IconoBaseDeDatos"),
        ("php-fpm", "IconoWeb"),
        ("apache2", "IconoWeb"),
        ("httpd", "IconoWeb"),
        ("node", "IconoWeb"),
        ("dotnet", "IconoAplicacion"),
        ("java", "IconoAplicacion"),
        ("javaw", "IconoAplicacion"),
        ("python", "IconoAplicacion"),
        ("python3", "IconoAplicacion"),
        ("bash", "IconoTerminalExterna"),
        ("sh", "IconoTerminalExterna"),
        ("zsh", "IconoTerminalExterna"),
        ("postfix", "IconoCorreo"),
        ("master", "IconoCorreo"),
        ("wazuh-agentd", "IconoCortafuegos"),
        ("firewalld", "IconoCortafuegos"),
        ("ufw", "IconoCortafuegos"),
        ("snmpd", "IconoPanelEstado"),
        ("chronyd", "IconoAjustes"),
        ("systemd-timesyn", "IconoAjustes"),
    ];

    public static string? ClaveDeIcono(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return null;
        }

        var limpio = nombre.Trim();

        foreach (var (conocido, clave) in Conocidos)
        {
            if (string.Equals(limpio, conocido, StringComparison.OrdinalIgnoreCase))
            {
                return clave;
            }
        }

        // «php-fpm: pool www» y «nginx: worker process» son el mismo binario con el rol pegado.
        var dosPuntos = limpio.IndexOf(':', StringComparison.Ordinal);

        if (dosPuntos > 0)
        {
            return ClaveDeIcono(limpio[..dosPuntos]);
        }

        // «python3.11» y «postgres-16» son versiones del mismo binario.
        var corte = limpio.AsSpan().IndexOfAny('.', '-');

        return corte > 0 ? Exacto(limpio[..corte]) : null;
    }

    public static bool EsConocido(string? nombre) => ClaveDeIcono(nombre) is not null;

    private static string? Exacto(string nombre)
    {
        foreach (var (conocido, clave) in Conocidos)
        {
            if (string.Equals(nombre, conocido, StringComparison.OrdinalIgnoreCase))
            {
                return clave;
            }
        }

        return null;
    }
}
