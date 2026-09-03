namespace CafManagerConection.Domain.Sessions;

public enum SessionState
{
    Connecting,
    Connected,
    Disconnected,
    Error,
}

public enum SessionFailureReason
{
    HostUnreachable,
    AuthenticationRejected,
    Timeout,
    HostKeyMismatch,
    CertificateUntrusted,
    PrivateKeyNotFound,

    CertificateNotFound,

    CertificateMismatch,

    BadPassphrase,
    CredentialMissing,
    UnexpectedDisconnect,

    /// <summary>No hubo algoritmo de intercambio, cifrado, MAC o clave de host en común (FR-148).</summary>
    AlgorithmNegotiationFailed,

    Other,
}

/// <summary><c>TechnicalDetail</c> no se muestra en la interfaz: va al registro (Principio II).</summary>
public sealed record SessionFailure(
    SessionFailureReason Reason,
    string UserMessage,
    string SuggestedAction,
    string? TechnicalDetail = null)
{
    public override string ToString() => $"SessionFailure({Reason})";
}

public sealed record SessionStateChanged(SessionState State, SessionFailure? Failure = null);
