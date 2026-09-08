namespace CafManagerConection.Domain.Connections;

/// <summary><c>null</c> en un campo = la carpeta no lo define y la herencia sigue subiendo.</summary>
public sealed class FolderSettings
{
    /// <summary>Usuario compartido, de reserva: se usa cuando el protocolo no define el suyo.</summary>
    public string? UserName { get; set; }
    public string? Domain { get; set; }

    /// <summary>Puerto compartido, de reserva: se usa cuando el protocolo no define el suyo.</summary>
    public int? Port { get; set; }

    public string? RdpUserName { get; set; }
    public string? SshUserName { get; set; }
    public string? WebUserName { get; set; }

    public int? RdpPort { get; set; }
    public int? SshPort { get; set; }
    public int? WebPort { get; set; }

    /// <summary>El usuario del protocolo, o el compartido de reserva.</summary>
    public string? UsuarioDe(Protocol protocol) => (protocol switch
    {
        Protocol.Rdp => RdpUserName,
        Protocol.Ssh => SshUserName,
        Protocol.Web => WebUserName,
        _ => null,
    }) ?? UserName;

    /// <summary>El puerto del protocolo, o el compartido de reserva.</summary>
    public int? PuertoDe(Protocol protocol) => (protocol switch
    {
        Protocol.Rdp => RdpPort,
        Protocol.Ssh => SshPort,
        Protocol.Web => WebPort,
        _ => null,
    }) ?? Port;

    public bool RdpTieneSecreto { get; set; }
    public bool SshTieneSecreto { get; set; }
    public bool WebTieneSecreto { get; set; }

    public bool? RdpClipboardEnabled { get; set; }
    public bool? RdpFitToTab { get; set; }
    public bool? RdpIgnoreCertificateWarnings { get; set; }

    public SshAuthMethod? SshAuthMethod { get; set; }
    public string? SshPrivateKeyPath { get; set; }

    /// <summary>Se hereda por el mismo camino que <see cref="SshPrivateKeyPath"/> pero no depende de él.</summary>
    public string? SshCertificatePath { get; set; }

    public int? SshKeepAliveSeconds { get; set; }

    /// <summary>Etiqueta de entorno que heredan los descendientes.</summary>
    public Guid? TagId { get; set; }

    /// <summary>Ajustes reservados <c>cmc:</c> que la carpeta hereda a sus descendientes (opciones de pantalla RDP).</summary>
    public Dictionary<string, string> CustomFields { get; init; } = [];

    public bool IsEmpty =>
        UserName is null && Domain is null && Port is null &&
        RdpUserName is null && SshUserName is null && WebUserName is null &&
        RdpPort is null && SshPort is null && WebPort is null &&
        !RdpTieneSecreto && !SshTieneSecreto && !WebTieneSecreto &&
        RdpClipboardEnabled is null && RdpFitToTab is null &&
        RdpIgnoreCertificateWarnings is null &&
        SshAuthMethod is null && SshPrivateKeyPath is null && SshCertificatePath is null &&
        SshKeepAliveSeconds is null &&
        TagId is null &&
        CustomFields.Count == 0;

    public bool TieneSecretoPara(Protocol protocol) => protocol switch
    {
        Protocol.Rdp => RdpTieneSecreto,
        Protocol.Ssh => SshTieneSecreto,
        Protocol.Web => WebTieneSecreto,
        _ => false,
    };

    public void DefinirSecretoPara(Protocol protocol, bool tiene)
    {
        switch (protocol)
        {
            case Protocol.Rdp: RdpTieneSecreto = tiene; break;
            case Protocol.Ssh: SshTieneSecreto = tiene; break;
            case Protocol.Web: WebTieneSecreto = tiene; break;
        }
    }
}
