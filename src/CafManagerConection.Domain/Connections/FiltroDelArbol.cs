namespace CafManagerConection.Domain.Connections;

/// <summary>Los filtros rápidos del árbol. Uno a la vez: apretar otro apaga el anterior, y apretar el que ya está prendido lo apaga.</summary>
public enum FiltroDelArbol
{
    Ninguno,
    Favoritas,
    Ssh,
    Rdp,
}

public static class Filtros
{
    public static bool Activo(this FiltroDelArbol filtro) => filtro != FiltroDelArbol.Ninguno;

    public static bool Admite(this FiltroDelArbol filtro, Protocol protocolo, bool esFavorita) =>
        filtro switch
        {
            FiltroDelArbol.Favoritas => esFavorita,
            FiltroDelArbol.Ssh => protocolo == Protocol.Ssh,
            FiltroDelArbol.Rdp => protocolo == Protocol.Rdp,
            _ => true,
        };

    /// <summary>Apretar el que ya está prendido lo apaga: es lo que hace que se pueda volver a ver todo sin buscar otro control.</summary>
    public static FiltroDelArbol Alternar(this FiltroDelArbol actual, FiltroDelArbol apretado) =>
        actual == apretado ? FiltroDelArbol.Ninguno : apretado;

    /// <summary>Lo que se dice cuando el filtro deja el árbol vacío: sin esto, un árbol vacío se lee como que se perdieron las conexiones.</summary>
    public static string Descripcion(this FiltroDelArbol filtro) => filtro switch
    {
        FiltroDelArbol.Favoritas => "favoritas",
        FiltroDelArbol.Ssh => "sólo SSH",
        FiltroDelArbol.Rdp => "sólo RDP",
        _ => string.Empty,
    };
}
