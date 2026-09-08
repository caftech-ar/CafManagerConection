using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class ConnectionHistoryRepository : IConnectionHistoryRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public ConnectionHistoryRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public Task AddAsync(ConnectionHistoryEntry entry, CancellationToken ct = default)
    {
        using var db = _factory.Create();

        db.Execute("""
            INSERT INTO connection_history (
                id, connection_id, attempted_at, outcome, failure_reason, duration_seconds)
            VALUES (@Id, @ConnectionId, @AttemptedAt, @Outcome, @FailureReason, @DurationSeconds);
            """,
            new
            {
                Id = entry.Id.ToString("D"),
                ConnectionId = entry.ConnectionId.ToString("D"),
                // Normalizado a UTC como FolderRepository.Iso: attempted_at se ordena lexicográficamente y con offsets mezclados el orden no es el cronológico.
                AttemptedAt = FolderRepository.Iso(entry.AttemptedAt),
                Outcome = entry.Outcome.ToString(),
                FailureReason = entry.FailureReason?.ToString(),
                entry.DurationSeconds,
            });

        db.Execute("""
            DELETE FROM connection_history
            WHERE connection_id = @ConnectionId
              AND id NOT IN (
                  SELECT id FROM connection_history
                  WHERE connection_id = @ConnectionId
                  ORDER BY attempted_at DESC
                  LIMIT @Retencion);
            """,
            new
            {
                ConnectionId = entry.ConnectionId.ToString("D"),
                Retencion = ConnectionHistoryEntry.RetentionPerConnection,
            });

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConnectionHistoryEntry>> GetForConnectionAsync(
        Guid connectionId, int limit = 50, CancellationToken ct = default)
    {
        using var db = _factory.Create();

        var filas = db.Query<FilaHistorial>("""
            SELECT * FROM connection_history
            WHERE connection_id = @Id
            ORDER BY attempted_at DESC
            LIMIT @Limite;
            """,
            new { Id = connectionId.ToString("D"), Limite = limit }).ToList();

        return Task.FromResult<IReadOnlyList<ConnectionHistoryEntry>>(
            filas.ConvertAll(f => f.ADominio()));
    }

    public Task<IReadOnlyList<ConnectionHistoryEntry>> GetRecentAsync(
        int limit = 500, CancellationToken ct = default)
    {
        using var db = _factory.Create();

        var filas = db.Query<FilaHistorial>("""
            SELECT * FROM connection_history
            ORDER BY attempted_at DESC
            LIMIT @Limite;
            """,
            new { Limite = limit }).ToList();

        return Task.FromResult<IReadOnlyList<ConnectionHistoryEntry>>(
            filas.ConvertAll(f => f.ADominio()));
    }

    private sealed class FilaHistorial
    {
        public string Id { get; set; } = string.Empty;

        public string Connection_Id { get; set; } = string.Empty;

        public string Attempted_At { get; set; } = string.Empty;

        public string Outcome { get; set; } = string.Empty;

        public string? Failure_Reason { get; set; }

        public int? Duration_Seconds { get; set; }

        public ConnectionHistoryEntry ADominio() => new(
            Guid.Parse(Id),
            Guid.Parse(Connection_Id),
            DateTimeOffset.Parse(Attempted_At, null, System.Globalization.DateTimeStyles.RoundtripKind),
            Enum.TryParse<ConnectionOutcome>(Outcome, out var resultado)
                ? resultado
                : ConnectionOutcome.Failed,
            Enum.TryParse<SessionFailureReason>(Failure_Reason, out var motivo) ? motivo : null,
            Duration_Seconds);
    }
}
