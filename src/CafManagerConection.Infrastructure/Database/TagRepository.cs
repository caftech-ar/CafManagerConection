using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class TagRepository : ITagRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public TagRepository(ISqliteConnectionFactory factory) => _factory = factory;

    // Clase con propiedades y no un record posicional: Dapper busca un constructor que coincida en tipos, y un INTEGER llega como Int64, así que la lectura fallaba entera.
    private sealed class Fila
    {
        public string Id { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Color { get; init; } = string.Empty;
        public int Sort_Order { get; init; }
    }

    public async Task<IReadOnlyList<Etiqueta>> GetAllAsync(CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        var filas = await cn.QueryAsync<Fila>(
            "SELECT id, code, name, color, sort_order FROM tags ORDER BY sort_order, name")
            .ConfigureAwait(false);

        return [.. filas.Select(f => new Etiqueta(
            Guid.Parse(f.Id), f.Code, f.Name, f.Color, f.Sort_Order))];
    }

    public async Task AddAsync(Etiqueta etiqueta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(etiqueta);

        using var cn = _factory.Create();

        await cn.ExecuteAsync(
            """
            INSERT INTO tags (id, code, name, color, sort_order, created_at, updated_at)
            VALUES (@Id, @Code, @Name, @ClaveDeColor, @SortOrder, @Ahora, @Ahora)
            """,
            new
            {
                Id = etiqueta.Id.ToString(),
                Code = etiqueta.Codigo,
                Name = etiqueta.Nombre,
                etiqueta.ClaveDeColor,
                SortOrder = etiqueta.Orden,
                Ahora = DateTimeOffset.UtcNow.ToString("O"),
            }).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Etiqueta etiqueta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(etiqueta);

        using var cn = _factory.Create();

        await cn.ExecuteAsync(
            """
            UPDATE tags
            SET code = @Code, name = @Name, color = @ClaveDeColor,
                sort_order = @SortOrder, updated_at = @Ahora
            WHERE id = @Id
            """,
            new
            {
                Id = etiqueta.Id.ToString(),
                Code = etiqueta.Codigo,
                Name = etiqueta.Nombre,
                etiqueta.ClaveDeColor,
                SortOrder = etiqueta.Orden,
                Ahora = DateTimeOffset.UtcNow.ToString("O"),
            }).ConfigureAwait(false);
    }

    // La clave foránea está declarada ON DELETE SET NULL y no CASCADE: borrar «Producción» del catálogo no puede llevarse cada servidor de producción.
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        await cn.ExecuteAsync(
            "DELETE FROM tags WHERE id = @Id", new { Id = id.ToString() }).ConfigureAwait(false);
    }

    public async Task<int> CountUsagesAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        return await cn.ExecuteScalarAsync<int>(
            """
            SELECT (SELECT COUNT(*) FROM connections     WHERE tag_id = @Id)
                 + (SELECT COUNT(*) FROM folder_settings WHERE tag_id = @Id)
            """,
            new { Id = id.ToString() }).ConfigureAwait(false);
    }
}
