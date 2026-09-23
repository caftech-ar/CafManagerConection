using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Settings;

/// <summary>Con qué icono del catálogo se dibuja cada cosa cuando nadie eligió uno a mano.</summary>
/// <remarks>Sumar un tipo de recurso nuevo es una entrada de enum y una línea acá.</remarks>
public static class IconosPorOmision
{
    public const string Carpeta = "folder";
    public const string Rdp = "device-desktop-share";
    public const string Ssh = "terminal-2";
    public const string Web = "world-www";
    public const string Aplicacion = "box";

    /// <summary>El icono que le toca a un protocolo.</summary>
    /// <param name="protocolo">Protocolo de la conexión, o null si no se sabe.</param>
    public static string DeProtocolo(Protocol? protocolo) => protocolo switch
    {
        Protocol.Rdp => Rdp,
        Protocol.Ssh => Ssh,
        Protocol.Web => Web,
        _ => Aplicacion,
    };

    /// <summary>El icono que le toca a un nodo del árbol que no eligió el suyo.</summary>
    /// <param name="esCarpeta">Si el nodo es una carpeta.</param>
    /// <param name="protocolo">Protocolo de la conexión, ignorado en una carpeta.</param>
    public static string DelArbol(bool esCarpeta, Protocol? protocolo) =>
        esCarpeta ? Carpeta : DeProtocolo(protocolo);
}
