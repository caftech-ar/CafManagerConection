namespace CafManagerConection.Domain.Monitoring;

/// <summary>Qué icono del catálogo le corresponde a un proceso del servidor cuando se lo reconoce. Sin coincidencia devuelve <c>null</c>: mostrar un icono genérico a todo hace que ninguno signifique nada.</summary>
/// <remarks>Cuando el producto tiene logo en el catálogo se usa el logo; si no, el concepto que le toca.</remarks>
public static class IconoDeProceso
{
    // El nombre que trae /proc/<pid>/stat es el del ejecutable, sin ruta y a lo sumo 15 caracteres.
    private static readonly (string Nombre, string Clave)[] Conocidos =
    [
        ("dockerd", "brand-docker"),
        ("containerd", "brand-docker"),
        ("containerd-shim", "brand-docker"),
        ("docker-proxy", "brand-docker"),
        ("nginx", "nginx"),
        ("supervisord", "settings-automation"),
        ("sshd", "terminal-2"),
        ("systemd", "settings"),
        ("systemd-journal", "list-details"),
        ("systemd-logind", "settings"),
        ("systemd-udevd", "settings"),
        ("cron", "calendar-time"),
        ("crond", "calendar-time"),
        ("rsyslogd", "list-details"),
        ("mysqld", "brand-mysql"),
        ("mariadbd", "mariadb"),
        ("postgres", "postgresql"),
        ("mongod", "brand-mongodb"),
        ("redis-server", "redis"),
        ("php-fpm", "brand-php"),
        ("apache2", "apache"),
        ("httpd", "apache"),
        ("node", "brand-nodejs"),
        ("dotnet", "dotnet"),
        ("java", "openjdk"),
        ("javaw", "openjdk"),
        ("python", "brand-python"),
        ("python3", "brand-python"),
        ("bash", "gnubash"),
        ("sh", "prompt"),
        ("zsh", "prompt"),
        ("postfix", "mail"),
        ("master", "mail"),
        ("wazuh-agentd", "wall"),
        ("firewalld", "wall"),
        ("ufw", "wall"),
        ("snmpd", "activity"),
        ("chronyd", "clock"),
        ("systemd-timesyn", "clock"),
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
