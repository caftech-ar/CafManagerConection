using System.Globalization;

namespace CafManagerConection.Domain.Connections;

/// <summary>Qué hace el cliente cuando la pestaña de la sesión cambia de tamaño. Excluyentes entre sí.</summary>
public enum ModoDeTamanoRdp
{
    Ninguno,

    EscalarPixeles,

    RenegociarResolucion,
}

/// <summary>Elementos visuales que se apagan para aliviar un enlace lento. Los valores son los bits que espera <c>PerformanceFlags</c> del control.</summary>
[Flags]
public enum BanderasDeRendimientoRdp
{
    Ninguna = 0,

    SinFondoDeEscritorio = 0x01,

    SinArrastreDeContenido = 0x02,

    SinAnimacionesDeMenu = 0x04,

    SinTemas = 0x08,

    SinSombraDelCursor = 0x20,
}

/// <summary>Tipo de enlace declarado. Los valores son los que espera <c>NetworkConnectionType</c> del control.</summary>
public enum TipoDeRedRdp
{
    Modem = 1,

    BandaAnchaBaja = 2,

    Satelital = 3,

    BandaAnchaAlta = 4,

    Wan = 5,

    Lan = 6,

    Automatico = 7,
}

/// <summary>Ajustes de cliente RDP que no piden puerto ni privilegio: rendimiento, escala, modo de tamaño y programa inicial.</summary>
public readonly record struct OpcionesDePantallaRdp(
    BanderasDeRendimientoRdp Rendimiento,
    TipoDeRedRdp? TipoDeRed,
    ModoDeTamanoRdp? ModoDeTamano,
    int? EscalaDeEscritorio,
    int? EscalaDeDispositivo,
    string? ProgramaInicial,
    string? DirectorioDeTrabajo,
    bool ComoRemoteApp)
{
    public static OpcionesDePantallaRdp Vacia => new(
        BanderasDeRendimientoRdp.Ninguna, null, null, null, null, null, null, false);
}

/// <summary>Los valores de escala que admite el protocolo. No hay entrada libre: un valor fuera de la lista el control lo descarta en silencio.</summary>
public static class EscalasDeRdp
{
    public static readonly IReadOnlyList<int> Escritorio = [100, 125, 150, 175, 200];

    public static readonly IReadOnlyList<int> Dispositivo = [100, 140, 180];

    public static bool EscritorioEsValida(int? valor) =>
        valor is null || Escritorio.Contains(valor.Value);

    public static bool DispositivoEsValida(int? valor) =>
        valor is null || Dispositivo.Contains(valor.Value);

    internal static int? ParseEntero(string? texto) =>
        int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
