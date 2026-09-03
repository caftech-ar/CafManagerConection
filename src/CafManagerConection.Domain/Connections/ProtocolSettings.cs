namespace CafManagerConection.Domain.Connections;

public enum SshAuthMethod
{
    Password,
    PrivateKey,
}

// FR-017: las redirecciones (discos, audio, impresoras, puertos, cámaras) no se modelan; el adaptador las fija apagadas.
public sealed class RdpSettings
{
    public Guid ConnectionId { get; init; }

    public string? Domain { get; set; }

    public bool? ClipboardEnabled { get; set; }

    public bool? FitToTab { get; set; }

    /// <summary><c>null</c> hereda; si nadie lo define, se validan (FR-016).</summary>
    public bool? IgnoreCertificateWarnings { get; set; }

    // Sin uso hasta la 002; ahora significa que la sesión arranca en su ventana propia (SessionView.xaml.cs:ConectarRdp).
    public bool StartFullScreen { get; set; }
}

/// <summary>Ajustes que viven en los campos propios de la conexión, que ya se serializan enteros: sumar uno no pide columna ni migración.</summary>
public static class AjustesReservados
{
    private const string Prefijo = "cmc:";

    public const string IdentidadDeWindows = Prefijo + "rdpIdentidadDeWindows";

    /// <summary>Los campos reservados no se muestran en la grilla de campos propios ni se borran al guardarla.</summary>
    public static bool EsReservado(string nombre) =>
        nombre is not null && nombre.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase);

    /// <summary>Entrar con la identidad de la sesión de Windows, sin usuario ni contraseña (FR-186).</summary>
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
}

public sealed class SshSettings
{
    public Guid ConnectionId { get; init; }

    public SshAuthMethod? AuthMethod { get; set; }

    public string? PrivateKeyPath { get; set; }

    public string? CertificatePath { get; set; }

    /// <summary>Formato <c>SHA256:base64</c>, igual al de OpenSSH. No heredable (FR-023).</summary>
    public string? KnownHostFingerprint { get; set; }

    public int? KeepAliveSeconds { get; set; }

    public string Encoding { get; set; } = "UTF-8";
}

public sealed class WebSettings
{
    public Guid ConnectionId { get; init; }

    public string Url { get; set; } = string.Empty;

    /// <summary>Ruta del navegador; <c>null</c> usa el predeterminado del sistema (FR-115).</summary>
    public string? Browser { get; set; }

    public bool PrivateWindow { get; set; }
}
