using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>
/// Historial de intentos de conexión (FR-009).
/// </summary>
public sealed class HistorialDeConexionesTests
{
    private static async Task<(TempDatabase Db, ConnectionHistoryRepository Repo, Connection Conexion)>
        ArmarAsync()
    {
        var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        var conexiones = new ConnectionRepository(db.Factory);
        var c = new Connection(Guid.NewGuid(), "Aplicaciones", Protocol.Ssh, "192.0.2.1");
        await conexiones.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));

        return (db, new ConnectionHistoryRepository(db.Factory), c);
    }

    private static ConnectionHistoryEntry Evento(
        Guid conexion,
        DateTimeOffset cuando,
        ConnectionOutcome resultado = ConnectionOutcome.Success,
        int? segundos = 60,
        SessionFailureReason? motivo = null) =>
        new(Guid.NewGuid(), conexion, cuando, resultado, motivo, segundos);

    [Fact]
    public async Task Un_evento_sobrevive_a_guardar_y_leer_con_todos_sus_campos()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        var cuando = new DateTimeOffset(2026, 8, 26, 9, 30, 0, TimeSpan.FromHours(-3));

        await repo.AddAsync(Evento(
            c.Id, cuando, ConnectionOutcome.Failed, null, SessionFailureReason.AuthenticationRejected));

        var leidos = await repo.GetForConnectionAsync(c.Id);
        var e = Assert.Single(leidos);

        Assert.Equal(c.Id, e.ConnectionId);
        Assert.Equal(ConnectionOutcome.Failed, e.Outcome);
        Assert.Equal(SessionFailureReason.AuthenticationRejected, e.FailureReason);
        Assert.Null(e.DurationSeconds);

        Assert.Equal(cuando.ToUniversalTime(), e.AttemptedAt.ToUniversalTime());
    }

    [Fact]
    public async Task La_duracion_se_conserva()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        await repo.AddAsync(Evento(c.Id, DateTimeOffset.UtcNow, segundos: 7384));

        Assert.Equal(7384, (await repo.GetForConnectionAsync(c.Id))[0].DurationSeconds);
    }

    [Fact]
    public async Task Los_eventos_vienen_del_mas_reciente_al_mas_viejo()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        var base_ = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

        await repo.AddAsync(Evento(c.Id, base_));
        await repo.AddAsync(Evento(c.Id, base_.AddDays(2)));
        await repo.AddAsync(Evento(c.Id, base_.AddDays(1)));

        var leidos = await repo.GetForConnectionAsync(c.Id);

        Assert.Equal(base_.AddDays(2), leidos[0].AttemptedAt.ToUniversalTime());
        Assert.Equal(base_, leidos[2].AttemptedAt.ToUniversalTime());
    }

    /// <remarks>La retención se aplica en el mismo <c>INSERT</c>, no en una limpieza aparte.</remarks>
    [Fact]
    public async Task La_retencion_descarta_los_mas_viejos()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        var limite = ConnectionHistoryEntry.RetentionPerConnection;
        var base_ = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < limite + 15; i++)
        {
            await repo.AddAsync(Evento(c.Id, base_.AddMinutes(i)));
        }

        var leidos = await repo.GetForConnectionAsync(c.Id, limit: 1000);

        Assert.Equal(limite, leidos.Count);

        Assert.Equal(base_.AddMinutes(limite + 14), leidos[0].AttemptedAt.ToUniversalTime());
        Assert.Equal(base_.AddMinutes(15), leidos[^1].AttemptedAt.ToUniversalTime());
    }

    [Fact]
    public async Task La_retencion_es_por_conexion_y_no_del_total()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        var conexiones = new ConnectionRepository(db.Factory);
        var otra = new Connection(Guid.NewGuid(), "Otro servidor", Protocol.Ssh, "192.0.2.2");
        await conexiones.AddAsync(
            new ConnectionRecord(otra, Ssh: new SshSettings { ConnectionId = otra.Id }));

        var base_ = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < ConnectionHistoryEntry.RetentionPerConnection + 5; i++)
        {
            await repo.AddAsync(Evento(c.Id, base_.AddMinutes(i)));
        }

        await repo.AddAsync(Evento(otra.Id, base_));

        Assert.Single(await repo.GetForConnectionAsync(otra.Id));
    }

    [Fact]
    public async Task Los_recientes_mezclan_todas_las_conexiones()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        var conexiones = new ConnectionRepository(db.Factory);
        var otra = new Connection(Guid.NewGuid(), "Otro servidor", Protocol.Ssh, "192.0.2.2");
        await conexiones.AddAsync(
            new ConnectionRecord(otra, Ssh: new SshSettings { ConnectionId = otra.Id }));

        var base_ = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

        await repo.AddAsync(Evento(c.Id, base_));
        await repo.AddAsync(Evento(otra.Id, base_.AddHours(1)));

        var recientes = await repo.GetRecentAsync();

        Assert.Equal(2, recientes.Count);
        Assert.Equal(otra.Id, recientes[0].ConnectionId);
    }

    [Fact]
    public async Task Los_recientes_respetan_el_limite_pedido()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        var base_ = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 10; i++)
        {
            await repo.AddAsync(Evento(c.Id, base_.AddMinutes(i)));
        }

        Assert.Equal(3, (await repo.GetRecentAsync(limit: 3)).Count);
    }

    /// <remarks>
    /// attempted_at se guarda como texto y se ordena lexicográficamente; sin normalizar a UTC
    /// como <c>FolderRepository.Iso</c>, "...T10:00-03:00" ordenaría antes que "...T12:00+00:00"
    /// pese a ser el instante posterior.
    /// </remarks>
    [Fact]
    public async Task El_orden_no_se_rompe_cuando_los_offsets_son_distintos()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        var masVieja = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var masNueva = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.FromHours(-3));

        await repo.AddAsync(Evento(c.Id, masVieja));
        await repo.AddAsync(Evento(c.Id, masNueva));

        var leidos = await repo.GetForConnectionAsync(c.Id);

        Assert.Equal(masNueva.ToUniversalTime(), leidos[0].AttemptedAt.ToUniversalTime());
        Assert.Equal(masVieja.ToUniversalTime(), leidos[1].AttemptedAt.ToUniversalTime());
    }

    /// <remarks>La columna es <c>ON DELETE CASCADE</c>.</remarks>
    [Fact]
    public async Task Borrar_la_conexion_se_lleva_su_historial()
    {
        var (db, repo, c) = await ArmarAsync();
        using var _ = db;

        await repo.AddAsync(Evento(c.Id, DateTimeOffset.UtcNow));
        await new ConnectionRepository(db.Factory).DeleteAsync(c.Id);

        Assert.Empty(await repo.GetRecentAsync());
    }
}
