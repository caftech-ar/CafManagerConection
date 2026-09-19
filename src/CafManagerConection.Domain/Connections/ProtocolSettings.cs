using System.Globalization;

namespace CafManagerConection.Domain.Connections;

public enum SshAuthMethod
{
    Password,
    PrivateKey,
}

// Las redirecciones (discos, audio, impresoras, puertos) no se modelan; el adaptador las fija apagadas.
public sealed class RdpSettings
{
    public Guid ConnectionId { get; init; }

    public string? Domain { get; set; }

    public bool? ClipboardEnabled { get; set; }

    /// <summary><c>null</c> hereda; si nadie lo define, se validan.</summary>
    public bool? IgnoreCertificateWarnings { get; set; }

    /// <summary>La sesión arranca en su ventana propia en vez de en una pestaña.</summary>
    public bool AbreEnVentanaPropia { get; set; }
}

/// <summary>Ajustes que viven en los campos propios de la conexión, que ya se serializan enteros: sumar uno no pide columna ni migración.</summary>
public static class AjustesReservados
{
    private const string Prefijo = "cmc:";

    public const string IdentidadDeWindows = Prefijo + "rdpIdentidadDeWindows";

    public const string Rendimiento = Prefijo + "rdpRendimiento";

    public const string TipoDeRed = Prefijo + "rdpTipoDeRed";

    public const string ModoDeTamano = Prefijo + "rdpModoDeTamano";

    public const string EscalaDeEscritorio = Prefijo + "rdpEscalaEscritorio";

    public const string EscalaDeDispositivo = Prefijo + "rdpEscalaDispositivo";

    public const string ProgramaInicial = Prefijo + "rdpProgramaInicial";

    public const string DirectorioDeTrabajo = Prefijo + "rdpDirectorioTrabajo";

    public const string ComoRemoteApp = Prefijo + "rdpRemoteApp";

    /// <summary>Bitácora de la sesión: ausente hereda lo global, <c>1</c> la enciende y <c>0</c> la apaga.</summary>
    public const string BitacoraDeSesion = Prefijo + "sshBitacora";

    /// <summary>Franja de métricas: ausente hereda lo global, <c>1</c> la enciende y <c>0</c> la apaga.</summary>
    public const string BarraDeMetricas = Prefijo + "sshBarraDeMetricas";

    /// <summary>Lo que la conexión decide sobre un ajuste de tres estados, o <c>null</c> si hereda.</summary>
    /// <param name="conexion">La conexión a consultar.</param>
    /// <param name="campo">Nombre del campo reservado.</param>
    public static bool? Decision(Connection conexion, string campo)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        return conexion.CustomFields.TryGetValue(campo, out var valor)
               && bool.TryParse(Normalizar(valor), out var decidido)
            ? decidido
            : null;
    }

    /// <summary>Guarda lo que la conexión decide sobre un ajuste de tres estados.</summary>
    /// <param name="conexion">La conexión a modificar.</param>
    /// <param name="campo">Nombre del campo reservado.</param>
    /// <param name="decision">Encendido, apagado, o <c>null</c> para heredar lo global.</param>
    public static void FijarDecision(Connection conexion, string campo, bool? decision)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        conexion.SetCustomField(
            campo, decision is { } valor ? valor.ToString() : null);
    }

    /// <summary>Si el ajuste queda activo para una conexión, con lo global de reserva.</summary>
    /// <param name="conexion">La conexión a consultar.</param>
    /// <param name="campo">Nombre del campo reservado.</param>
    /// <param name="global">Lo que dice el ajuste global.</param>
    public static bool Activo(Connection conexion, string campo, bool global) =>
        Decision(conexion, campo) ?? global;

    private static string Normalizar(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        "1" or "si" or "sí" or "true" => "true",
        "0" or "no" or "false" => "false",
        var otro => otro ?? string.Empty,
    };

    /// <summary>Los campos reservados no se muestran en la grilla de campos propios ni se borran al guardarla.</summary>
    public static bool EsReservado(string nombre) =>
        nombre is not null && nombre.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase);

    /// <summary>Entrar con la identidad de la sesión de Windows, sin usuario ni contraseña.</summary>
    public static bool UsaIdentidadDeWindows(Connection conexion)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        return conexion.Protocol == Protocol.Rdp
               && conexion.CustomFields.TryGetValue(IdentidadDeWindows, out var valor)
               && bool.TryParse(valor, out var activa)
               && activa;
    }

    public static void FijarIdentidadDeWindows(Connection conexion, bool activa)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        conexion.SetCustomField(IdentidadDeWindows, activa ? bool.TrueString : null);
    }

    /// <summary>Lee las opciones de pantalla propias de un juego de campos reservados; los ausentes vuelven vacíos.</summary>
    public static OpcionesDePantallaRdp LeerOpcionesDePantalla(
        IReadOnlyDictionary<string, string> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);

        return new OpcionesDePantallaRdp(
            ParseBanderas(Valor(campos, Rendimiento)),
            ParseTipoDeRed(Valor(campos, TipoDeRed)),
            ParseModoDeTamano(Valor(campos, ModoDeTamano)),
            EscalasDeRdp.ParseEntero(Valor(campos, EscalaDeEscritorio)),
            EscalasDeRdp.ParseEntero(Valor(campos, EscalaDeDispositivo)),
            Texto(Valor(campos, ProgramaInicial)),
            Texto(Valor(campos, DirectorioDeTrabajo)),
            bool.TryParse(Valor(campos, ComoRemoteApp), out var remota) && remota);
    }

    /// <summary>Vuelca las opciones de pantalla a campos reservados llamando a <paramref name="fijar"/>; un valor vacío se pasa como <c>null</c> para que se borre.</summary>
    public static void EscribirOpcionesDePantalla(
        OpcionesDePantallaRdp opciones, Action<string, string?> fijar)
    {
        ArgumentNullException.ThrowIfNull(fijar);

        fijar(Rendimiento, opciones.Rendimiento == BanderasDeRendimientoRdp.Ninguna
            ? null
            : ((int)opciones.Rendimiento).ToString(CultureInfo.InvariantCulture));
        fijar(TipoDeRed, opciones.TipoDeRed?.ToString());
        fijar(ModoDeTamano, opciones.ModoDeTamano?.ToString());
        fijar(EscalaDeEscritorio,
            opciones.EscalaDeEscritorio?.ToString(CultureInfo.InvariantCulture));
        fijar(EscalaDeDispositivo,
            opciones.EscalaDeDispositivo?.ToString(CultureInfo.InvariantCulture));
        fijar(ProgramaInicial, Texto(opciones.ProgramaInicial));
        fijar(DirectorioDeTrabajo, Texto(opciones.DirectorioDeTrabajo));
        fijar(ComoRemoteApp, opciones.ComoRemoteApp ? bool.TrueString : null);
    }

    public static BanderasDeRendimientoRdp ParseBanderas(string? texto) =>
        int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? (BanderasDeRendimientoRdp)v
            : BanderasDeRendimientoRdp.Ninguna;

    public static TipoDeRedRdp? ParseTipoDeRed(string? texto) =>
        Enum.TryParse<TipoDeRedRdp>(texto, out var v) && Enum.IsDefined(v) ? v : null;

    public static ModoDeTamanoRdp? ParseModoDeTamano(string? texto) =>
        Enum.TryParse<ModoDeTamanoRdp>(texto, out var v) && Enum.IsDefined(v) ? v : null;

    private static string? Valor(IReadOnlyDictionary<string, string> campos, string clave) =>
        campos.TryGetValue(clave, out var v) ? v : null;

    private static string? Texto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public sealed class SshSettings
{
    public Guid ConnectionId { get; init; }

    public SshAuthMethod? AuthMethod { get; set; }

    public string? PrivateKeyPath { get; set; }

    public string? CertificatePath { get; set; }

    /// <summary>Formato <c>SHA256:base64</c>, igual al de OpenSSH. No heredable.</summary>
    public string? KnownHostFingerprint { get; set; }

    public int? KeepAliveSeconds { get; set; }
}

public sealed class WebSettings
{
    public Guid ConnectionId { get; init; }

    public string Url { get; set; } = string.Empty;

    /// <summary>Ruta del navegador; <c>null</c> usa el predeterminado del sistema.</summary>
    public string? Browser { get; set; }

    public bool PrivateWindow { get; set; }
}
