using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using DomainDefaults = CafManagerConection.Domain.Settings.Defaults;

namespace CafManagerConection.UseCases.Inheritance;

/// <summary>Valor resuelto por la cascada, con la carpeta que lo definió para poder decir «heredado de X».</summary>
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
    /// <summary>Dónde está la contraseña que se va a usar: la de la conexión, o la de la carpeta más cercana que tenga una.</summary>
    public required Inherited<ReferenciaDeSecreto> Secreto { get; init; }
    public required Inherited<string> Domain { get; init; }

    /// <summary>Propio de la conexión, o el de la carpeta más cercana que lo defina.</summary>
    public Inherited<Guid> TagId { get; init; }

    public Inherited<bool> ClipboardEnabled { get; init; }
    public Inherited<bool> FitToTab { get; init; }
    public Inherited<bool> IgnoreCertificateWarnings { get; init; }

    public Inherited<BanderasDeRendimientoRdp> Rendimiento { get; init; }
    public Inherited<TipoDeRedRdp> TipoDeRed { get; init; }
    public Inherited<ModoDeTamanoRdp> ModoDeTamano { get; init; }
    public Inherited<int> EscalaDeEscritorio { get; init; }
    public Inherited<int> EscalaDeDispositivo { get; init; }
    public Inherited<string> ProgramaInicial { get; init; }
    public Inherited<string> DirectorioDeTrabajo { get; init; }
    public Inherited<bool> ComoRemoteApp { get; init; }

    /// <summary>Entrar con la identidad de la sesión de Windows, sin usuario ni contraseña.</summary>
    public Inherited<bool> UseWindowsIdentity { get; init; }

    public Inherited<SshAuthMethod> AuthMethod { get; init; }
    public Inherited<string> PrivateKeyPath { get; init; }

    public Inherited<string> CertificatePath { get; init; }

    public Inherited<int> KeepAliveSeconds { get; init; }

    public int ResolvedPort => Port.IsDefined ? Port.Value : Connection.DefaultPortFor(Protocol);

    public bool ResolvedClipboardEnabled => ClipboardEnabled.ValueOr(true);

    public bool ResolvedFitToTab => FitToTab.ValueOr(true);

    public BanderasDeRendimientoRdp ResolvedRendimiento =>
        Rendimiento.ValueOr(BanderasDeRendimientoRdp.Ninguna);

    public TipoDeRedRdp? ResolvedTipoDeRed => TipoDeRed.IsDefined ? TipoDeRed.Value : null;

    /// <summary>El modo propio o heredado; si nadie lo define, se deriva del viejo <see cref="FitToTab"/> para no cambiarle la conducta a lo ya guardado.</summary>
    public ModoDeTamanoRdp ResolvedModoDeTamano =>
        ModoDeTamano.IsDefined
            ? ModoDeTamano.Value
            : ResolvedFitToTab ? ModoDeTamanoRdp.EscalarPixeles : ModoDeTamanoRdp.Ninguno;

    public int? ResolvedEscalaDeEscritorio =>
        EscalaDeEscritorio.IsDefined ? EscalaDeEscritorio.Value : null;

    public int? ResolvedEscalaDeDispositivo =>
        EscalaDeDispositivo.IsDefined ? EscalaDeDispositivo.Value : null;

    public string? ResolvedProgramaInicial => ProgramaInicial.Value;

    public string? ResolvedDirectorioDeTrabajo => DirectorioDeTrabajo.Value;

    public bool ResolvedComoRemoteApp => ComoRemoteApp.ValueOr(false);

    /// <summary>Predeterminado <c>false</c>: validar es lo seguro.</summary>
    public bool ResolvedIgnoreCertificateWarnings => IgnoreCertificateWarnings.ValueOr(false);

    /// <summary>Predeterminado <c>false</c>: el camino de siempre es pedir credenciales.</summary>
    public bool ResolvedUseWindowsIdentity => UseWindowsIdentity.ValueOr(false);

    public SshAuthMethod ResolvedAuthMethod => AuthMethod.ValueOr(
        string.IsNullOrWhiteSpace(PrivateKeyPath.Value)
            ? SshAuthMethod.Password
            : SshAuthMethod.PrivateKey);

    public int ResolvedKeepAliveSeconds =>
        KeepAliveSeconds.ValueOr(DomainDefaults.SshKeepAliveSeconds);
}
