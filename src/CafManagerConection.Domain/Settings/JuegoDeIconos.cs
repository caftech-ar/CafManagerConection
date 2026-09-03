namespace CafManagerConection.Domain.Settings;

public sealed record IconoDelJuego(string Clave, string Nombre, string Recurso);

public static class JuegoDeIconos
{
    public static IReadOnlyList<IconoDelJuego> Iconos { get; } =
    [
        new("carpeta", "Carpeta", "IconoCarpeta"),
        new("escritorio", "Escritorio remoto", "IconoRdp"),
        new("terminal", "Terminal", "IconoSsh"),
        new("web", "Servidor web", "IconoWeb"),
        new("base-de-datos", "Base de datos", "IconoBaseDeDatos"),
        new("correo", "Correo", "IconoCorreo"),
        new("archivos", "Archivos", "IconoPanelArchivos"),
        new("respaldo", "Respaldo", "IconoRespaldo"),
        new("contenedor", "Contenedor", "IconoPanelDocker"),
        new("cortafuegos", "Cortafuegos", "IconoCortafuegos"),
        new("monitoreo", "Monitoreo", "IconoPanelEstado"),
        new("proxy", "Proxy inverso", "IconoPanelNginx"),
        new("servicios", "Servicios", "IconoPanelSupervisor"),
        new("red", "Red y túneles", "IconoPanelTuneles"),
        new("puertos", "Puertos", "IconoPanelPuertos"),
        new("aplicacion", "Aplicación", "IconoAplicacion"),
    ];

    public static bool EsValido(string? clave) =>
        clave is not null && Iconos.Any(i => i.Clave == clave);

    /// <summary>null cuando la clave no está en el juego: quien llama cae en el icono de la aplicación (FR-195b).</summary>
    public static string? ClaveDeRecurso(string? clave) =>
        Iconos.FirstOrDefault(i => i.Clave == clave)?.Recurso;

    public static IconoDelJuego? Resolver(string? clave) =>
        Iconos.FirstOrDefault(i => i.Clave == clave);
}
