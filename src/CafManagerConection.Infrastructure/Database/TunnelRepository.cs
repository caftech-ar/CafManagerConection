using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class TunnelRepository : ITunnelRepository
{
    private const string Columnas =
        "SELECT id, connection_id, name, local_port, remote_host, remote_port, auto_start "
        + "FROM ssh_tunnels";

    private readonly ISqliteConnectionFactory _factory;

    public TunnelRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<SshTunnel>> GetForConnectionAsync(
        Guid connectionId, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var rows = db.Query<TunnelRow>(
            Columnas + " WHERE connection_id = @Id ORDER BY name;",
            new { Id = connectionId.ToString("D") }).ToList();

        return Task.FromResult<IReadOnlyList<SshTunnel>>(rows.Select(r => r.ADominio()).ToList());
    }

    public Task AddAsync(SshTunnel tunnel, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute("""
            INSERT INTO ssh_tunnels (
                id, connection_id, name, local_port, remote_host, remote_port, auto_start)
            VALUES (@Id, @ConnectionId, @Name, @LocalPort, @RemoteHost, @RemotePort, @AutoStart);
            """, ToParams(tunnel));

        return Task.CompletedTask;
    }

    public Task UpdateAsync(SshTunnel tunnel, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute("""
            UPDATE ssh_tunnels SET
                name = @Name, local_port = @LocalPort, remote_host = @RemoteHost,
                remote_port = @RemotePort, auto_start = @AutoStart
            WHERE id = @Id;
            """, ToParams(tunnel));

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute("DELETE FROM ssh_tunnels WHERE id = @Id;", new { Id = id.ToString("D") });
        return Task.CompletedTask;
    }

    private static object ToParams(SshTunnel t) => new
    {
        Id = t.Id.ToString("D"),
        ConnectionId = t.ConnectionId.ToString("D"),
        t.Name,
        t.LocalPort,
        t.RemoteHost,
        t.RemotePort,
        AutoStart = t.AutoStart ? 1 : 0,
    };

    private sealed class TunnelRow
    {
        public string Id { get; init; } = string.Empty;
        public string ConnectionId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int LocalPort { get; init; }
        public string RemoteHost { get; init; } = string.Empty;
        public int RemotePort { get; init; }
        public long AutoStart { get; init; }

        public SshTunnel ADominio() => new(
            Guid.Parse(Id), Guid.Parse(ConnectionId), Name, LocalPort, RemoteHost, RemotePort)
        {
            AutoStart = AutoStart != 0,
        };
    }
}
