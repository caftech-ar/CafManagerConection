namespace CafManagerConection.Domain.Connections;

/// <summary>Reenvío de puerto local de una conexión SSH; no es heredable (FR-088).</summary>
public sealed class SshTunnel
{
    public SshTunnel(Guid id, Guid connectionId, string name, int localPort, string remoteHost, int remotePort)
    {
        Id = id;
        ConnectionId = connectionId;
        Name = ValidateName(name);
        LocalPort = ValidatePort(localPort, nameof(localPort));
        RemoteHost = ValidateHost(remoteHost);
        RemotePort = ValidatePort(remotePort, nameof(remotePort));
    }

    public Guid Id { get; }

    public Guid ConnectionId { get; }

    public string Name { get; set; }

    public int LocalPort { get; set; }

    /// <summary>Destino visto <b>desde el servidor</b>; habitualmente <c>localhost</c>.</summary>
    public string RemoteHost { get; set; }

    public int RemotePort { get; set; }

    public bool AutoStart { get; set; }

    public int SortOrder { get; set; }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim();
    }

    private static string ValidateHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        return host.Trim();
    }

    private static int ValidatePort(int port, string paramName)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(paramName, port, "El puerto debe estar entre 1 y 65535.");
        }

        return port;
    }
}

public enum ConnectionOutcome
{
    Success,
    Failed,
    Cancelled,
}

public sealed record ConnectionHistoryEntry(
    Guid Id,
    Guid ConnectionId,
    DateTimeOffset AttemptedAt,
    ConnectionOutcome Outcome,
    Sessions.SessionFailureReason? FailureReason = null,
    int? DurationSeconds = null)
{
    /// <summary>Eventos que se conservan por conexión; al insertar uno más se descarta el más antiguo.</summary>
    public const int RetentionPerConnection = 100;
}
