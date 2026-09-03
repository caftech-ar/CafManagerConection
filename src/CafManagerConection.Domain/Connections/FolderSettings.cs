namespace CafManagerConection.Domain.Connections;

/// <summary><c>null</c> en un campo = la carpeta no lo define y la herencia sigue subiendo (FR-058).</summary>
public sealed class FolderSettings
{
    public string? UserName { get; set; }
    public string? Domain { get; set; }
    public int? Port { get; set; }

    public string? RdpCredentialKey { get; set; }
    public string? SshCredentialKey { get; set; }
    public string? WebCredentialKey { get; set; }

    public bool? RdpClipboardEnabled { get; set; }
    public bool? RdpFitToTab { get; set; }
    public bool? RdpIgnoreCertificateWarnings { get; set; }

    public SshAuthMethod? SshAuthMethod { get; set; }
    public string? SshPrivateKeyPath { get; set; }

    /// <summary>Se hereda por el mismo camino que <see cref="SshPrivateKeyPath"/> pero no depende de él.</summary>
    public string? SshCertificatePath { get; set; }

    public int? SshKeepAliveSeconds { get; set; }

    /// <summary>Etiqueta de entorno que heredan los descendientes (FR-130).</summary>
    public Guid? TagId { get; set; }

    public bool IsEmpty =>
        UserName is null && Domain is null && Port is null &&
        RdpCredentialKey is null && SshCredentialKey is null && WebCredentialKey is null &&
        RdpClipboardEnabled is null && RdpFitToTab is null &&
        RdpIgnoreCertificateWarnings is null &&
        SshAuthMethod is null && SshPrivateKeyPath is null && SshCertificatePath is null &&
        SshKeepAliveSeconds is null &&
        TagId is null;

    public string? CredentialKeyFor(Protocol protocol) => protocol switch
    {
        Protocol.Rdp => RdpCredentialKey,
        Protocol.Ssh => SshCredentialKey,
        Protocol.Web => WebCredentialKey,
        _ => null,
    };
}
