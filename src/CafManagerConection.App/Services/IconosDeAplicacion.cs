using CafManagerConection.App.Themes;
using CafManagerConection.Platform;

namespace CafManagerConection.App.Services;

/// <summary>Con qué icono del catálogo y qué color se dibuja cada aplicación conocida.</summary>
public static class IconosDeAplicacion
{
    /// <summary>El logo del producto cuando el catálogo lo tiene, y el concepto de su clase cuando no.</summary>
    /// <param name="aplicacion">La aplicación reconocida en el servidor.</param>
    public static string Glifo(AplicacionConocida aplicacion) =>
        LogoDelProducto(aplicacion.Nombre) ?? Glifo(aplicacion.Clase);

    /// <summary>El concepto que le toca a una clase de aplicación.</summary>
    /// <param name="clase">Clase a la que pertenece la aplicación.</param>
    public static string Glifo(ClaseDeAplicacion clase) => clase switch
    {
        ClaseDeAplicacion.ServidorWeb => IconosDeLaInterfaz.Web,
        ClaseDeAplicacion.BaseDeDatos => IconosDeLaInterfaz.BaseDeDatos,
        ClaseDeAplicacion.Contenedor => "container",
        ClaseDeAplicacion.AccesoRemoto => IconosDeLaInterfaz.Ssh,
        ClaseDeAplicacion.SupervisionDeProcesos => IconosDeLaInterfaz.PanelSupervisor,
        ClaseDeAplicacion.Mensajeria => IconosDeLaInterfaz.PanelTuneles,
        ClaseDeAplicacion.ServicioDelSistema => IconosDeLaInterfaz.Ajustes,
        _ => IconosDeLaInterfaz.Aplicacion,
    };

    private static string? LogoDelProducto(string nombre) => nombre switch
    {
        "nginx" => "nginx",
        "Apache" => "apache",
        "Traefik" => "traefikproxy",

        "PostgreSQL" => "postgresql",
        "MySQL" => "brand-mysql",
        "MariaDB" => "mariadb",
        "MongoDB" => "brand-mongodb",
        "Redis" => "redis",
        "Elasticsearch" => "brand-elastic",

        "Docker" => "brand-docker",
        "RabbitMQ" => "rabbitmq",

        "Aplicación Python" or "Gunicorn (Python)" or "uWSGI (Python)" or "Uvicorn (Python)"
            => "brand-python",
        "Aplicación Node.js" => "brand-nodejs",
        "Aplicación Java" => "openjdk",
        "Aplicación .NET" => "dotnet",
        "PHP" => "brand-php",

        _ => null,
    };

    /// <summary>Color por producto dentro de su clase.</summary>
    public static string Color(AplicacionConocida aplicacion) => aplicacion.Nombre switch
    {
        "nginx" => "IconoVerde",
        "Apache" => "IconoRojo",
        "Caddy" => "IconoCyan",
        "Traefik" => "IconoAzul",
        "HAProxy" => "IconoAmbar",

        "PostgreSQL" => "IconoAzul",
        "MySQL" => "IconoNaranja",
        "MariaDB" => "IconoAmbar",
        "MongoDB" => "IconoVerde",
        "Redis" => "IconoRojo",
        "Memcached" => "IconoGris",
        "InfluxDB" => "IconoVioleta",
        "ClickHouse" => "IconoLima",
        "Elasticsearch" => "IconoCyan",

        "Aplicación Python" or "Gunicorn (Python)" or "uWSGI (Python)" or "Uvicorn (Python)"
            => "IconoAmbar",
        "Aplicación Node.js" => "IconoLima",
        "Aplicación Java" => "IconoNaranja",
        "Aplicación .NET" => "IconoVioleta",
        "Aplicación Ruby" => "IconoRojo",
        "PHP" => "IconoAzul",
        "Erlang/Elixir" => "IconoRosa",

        _ => PorClase(aplicacion.Clase),
    };

    private static string PorClase(ClaseDeAplicacion clase) => clase switch
    {
        ClaseDeAplicacion.ServidorWeb => "IconoVerde",
        ClaseDeAplicacion.BaseDeDatos => "IconoAzul",
        ClaseDeAplicacion.Contenedor => "IconoCyan",
        ClaseDeAplicacion.AccesoRemoto => "IconoVioleta",
        ClaseDeAplicacion.SupervisionDeProcesos => "IconoRosa",
        ClaseDeAplicacion.Mensajeria => "IconoNaranja",
        ClaseDeAplicacion.ServicioDelSistema => "IconoGris",
        _ => "IconoAmbar",
    };
}
