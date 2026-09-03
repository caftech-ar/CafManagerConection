using CafManagerConection.Platform;

namespace CafManagerConection.App.Services;

/// <summary>Con qué glifo y color se dibuja cada clase de aplicación.</summary>
public static class IconosDeAplicacion
{
    public static string Glifo(ClaseDeAplicacion clase) => clase switch
    {
        ClaseDeAplicacion.ServidorWeb => "IconoPanelNginx",
        ClaseDeAplicacion.BaseDeDatos => "IconoBaseDeDatos",
        ClaseDeAplicacion.Contenedor => "IconoPanelDocker",
        ClaseDeAplicacion.AccesoRemoto => "IconoSsh",
        ClaseDeAplicacion.SupervisionDeProcesos => "IconoPanelSupervisor",
        ClaseDeAplicacion.Mensajeria => "IconoPanelTuneles",
        ClaseDeAplicacion.ServicioDelSistema => "IconoAjustes",
        _ => "IconoAplicacion",
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
