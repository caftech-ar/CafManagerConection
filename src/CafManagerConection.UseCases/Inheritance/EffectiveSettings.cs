using CafManagerConection.Domain.Connections;

using DomainDefaults = CafManagerConection.Domain.Settings.Defaults;

namespace CafManagerConection.UseCases.Inheritance;

/// <summary>Valor resuelto por la cascada, con la carpeta que lo definió para poder decir «heredado de X» (FR-061).</summary>
public readonly record struct Inherited<T>(T? Value, ValueSource Source, Guid? SourceFolderId = null)
{
    public bool IsInherited => Source == ValueSource.Inherited;

    public bool IsDefined => Source != ValueSource.Undefined;

    public T? ValueOr(T? fallback) => IsDefined ? Value : fallback;

    public static Inherited<T> Own(T value) => new(value, ValueSource.Own);

    public static Inherited<T> From(T value, Guid folderId) =>
        new(value, ValueSource.Inherited, folderId);

    public static Inherited<T> None { get; } = new(default, ValueSource.Undefined);
}

public enum ValueSource
{
    Undefined,

    Own,

    Inherited,
}

public sealed record EffectiveSettings
{
    public required Guid ConnectionId { get; init; }
    public required Protocol Protocol { get; init; }
    public required string Host { get; init; }

    public required Inherited<int> Port { get; init; }
    public required Inherited<string> UserName { get; init; }
    public required Inherited<string> CredentialKey { get; init; }
    public required Inherited<string> Domain { get; init; }

    /// <summary>Propio de la conexión, o el de la carpeta más cercana que lo defina (FR-130).</summary>
    public Inherited<Guid> TagId { get; init; }

    public Inherited<bool> ClipboardEnabled { get; init; }
    public Inherited<bool> FitToTab { get; init; }
    public Inherited<bool> IgnoreCertificateWarnings { get; init; }

    /// <summary>Entrar con la identidad de la sesión de Windows, sin usuario ni contraseña (FR-186).</summary>
    public Inherited<bool> UseWindowsIdentity { get; init; }

    public Inherited<SshAuthMethod> AuthMethod { get; init; }
    public Inherited<string> PrivateKeyPath { get; init; }

    public Inherited<string> CertificatePath { get; init; }

    public Inherited<int> KeepAliveSeconds { get; init; }

    public int ResolvedPort => Port.IsDefined ? Port.Value : Connection.DefaultPortFor(Protocol);

    public bool ResolvedClipboardEnabled => ClipboardEnabled.ValueOr(true);

    public bool ResolvedFitToTab => FitToTab.ValueOr(true);

    /// <summary>Predeterminado <c>false</c>: validar es lo seguro (FR-016).</summary>
    public bool ResolvedIgnoreCertificateWarnings => IgnoreCertificateWarnings.ValueOr(false);

    /// <summary>Predeterminado <c>false</c>: el camino de siempre es pedir credenciales (FR-186).</summary>
    public bool ResolvedUseWindowsIdentity => UseWindowsIdentity.ValueOr(false);

    public SshAuthMethod ResolvedAuthMethod => AuthMethod.ValueOr(
        string.IsNullOrWhiteSpace(PrivateKeyPath.Value)
            ? SshAuthMethod.Password
            : SshAuthMethod.PrivateKey);

    public int ResolvedKeepAliveSeconds =>
        KeepAliveSeconds.ValueOr(DomainDefaults.SshKeepAliveSeconds);
}
