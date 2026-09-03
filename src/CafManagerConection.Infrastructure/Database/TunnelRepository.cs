using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class TunnelRepository : ITunnelRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public TunnelRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<SshTunnel>> GetForConnectionAsync(
        Guid connectionId, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var rows = db.Query<TunnelRow>(
            "SELECT * FROM ssh_tunnels WHERE connection_id = @Id ORDER BY sort_order;",
            new { Id = connectionId.ToString("D") }).ToList();

        return Task.FromResult<IReadOnlyList<SshTunnel>>(rows.Select(r => r.ToDomain()).ToList());
    }

    public Task<IReadOnlyList<SshTunnel>> GetAllAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var rows = db.Query<TunnelRow>(
            "SELECT * FROM ssh_tunnels ORDER BY connection_id, sort_order;").ToList();

        return Task.FromResult<IReadOnlyList<SshTunnel>>(rows.Select(r => r.ToDomain()).ToList());
    }

    public Task AddAsync(SshTunnel tunnel, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute("""
            INSERT INTO ssh_tunnels (
                id, connection_id, name, local_port, remote_host, remote_port,
                auto_start, sort_order)
            VALUES (@Id, @ConnectionId, @Name, @LocalPort, @RemoteHost, @RemotePort,
                    @AutoStart, @SortOrder);
            """, ToParams(tunnel));

        return Task.CompletedTask;
    }

    public Task UpdateAsync(SshTunnel tunnel, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute("""
            UPDATE ssh_tunnels SET
                name = @Name, local_port = @LocalPort, remote_host = @RemoteHost,
                remote_port = @RemotePort, auto_start = @AutoStart, sort_order = @SortOrder
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
        t.SortOrder,
    };

    private sealed class TunnelRow
    {
        public string Id { get; init; } = string.Empty;
        public string Connection_Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Local_Port { get; init; }
        public string Remote_Host { get; init; } = string.Empty;
        public int Remote_Port { get; init; }
        public long Auto_Start { get; init; }
        public int Sort_Order { get; init; }

        public SshTunnel ToDomain() => new(
            Guid.Parse(Id), Guid.Parse(Connection_Id), Name, Local_Port, Remote_Host, Remote_Port)
        {
            AutoStart = Auto_Start != 0,
            SortOrder = Sort_Order,
        };
    }
}
