using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class ConnectionHistoryRepository : IConnectionHistoryRepository
{
    private const string Columnas =
        "SELECT id, connection_id, attempted_at, outcome, failure_reason, duration_seconds "
        + "FROM connection_history";

    private readonly ISqliteConnectionFactory _factory;

    public ConnectionHistoryRepository(ISqliteConnectionFactory factory) => _factory = factory;

    /// <summary>Anota el evento y descarta los que exceden la retención, en una sola transacción.</summary>
    public Task AddAsync(ConnectionHistoryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var db = _factory.Create();
        using var tx = db.BeginTransaction();

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
            }, tx);

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
            }, tx);

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConnectionHistoryEntry>> GetRecentAsync(
        int limit = 500, CancellationToken ct = default)
    {
        using var db = _factory.Create();

        var filas = db.Query<FilaHistorial>(
            Columnas + " ORDER BY attempted_at DESC LIMIT @Limite;",
            new { Limite = limit }).ToList();

        return Task.FromResult<IReadOnlyList<ConnectionHistoryEntry>>(
            filas.ConvertAll(f => f.ADominio()));
    }

    public Task<int> ContarAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return Task.FromResult(db.ExecuteScalar<int>("SELECT COUNT(*) FROM connection_history;"));
    }

    public Task<IReadOnlyDictionary<Guid, DateTimeOffset>> UltimaConexionExitosaPorConexionAsync(
        CancellationToken ct = default)
    {
        using var db = _factory.Create();

        var filas = db.Query<FilaUltima>("""
            SELECT connection_id, MAX(attempted_at) AS cuando
              FROM connection_history
             WHERE outcome = 'Success'
             GROUP BY connection_id;
            """).ToList();

        return Task.FromResult<IReadOnlyDictionary<Guid, DateTimeOffset>>(
            filas.ToDictionary(f => Guid.Parse(f.ConnectionId), f => Fecha(f.Cuando)));
    }

    private static DateTimeOffset Fecha(string valor) =>
        DateTimeOffset.Parse(valor, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private sealed class FilaUltima
    {
        public string ConnectionId { get; init; } = string.Empty;

        public string Cuando { get; init; } = string.Empty;
    }

    private sealed class FilaHistorial
    {
        public string Id { get; set; } = string.Empty;

        public string ConnectionId { get; set; } = string.Empty;

        public string AttemptedAt { get; set; } = string.Empty;

        public string Outcome { get; set; } = string.Empty;

        public string? FailureReason { get; set; }

        public int? DurationSeconds { get; set; }

        public ConnectionHistoryEntry ADominio() => new(
            Guid.Parse(Id),
            Guid.Parse(ConnectionId),
            Fecha(AttemptedAt),
            Enum.TryParse<ConnectionOutcome>(Outcome, out var resultado)
                ? resultado
                : ConnectionOutcome.Failed,
            Enum.TryParse<SessionFailureReason>(FailureReason, out var motivo) ? motivo : null,
            DurationSeconds);
    }
}
