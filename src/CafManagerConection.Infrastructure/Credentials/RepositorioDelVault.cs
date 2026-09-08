using System.Data;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Credentials;

public sealed class RepositorioDelVault : IRepositorioDelVault
{
    private readonly ISqliteConnectionFactory _factory;

    public RepositorioDelVault(ISqliteConnectionFactory factory) => _factory = factory;

    // Clase y no record posicional: Dapper busca un constructor que coincida en tipos, y un
    // INTEGER de SQLite llega como Int64, con lo que la lectura entera falla.
    private sealed class FilaDeVault
    {
        public long Formato { get; init; }
        public string Kdf_Hash { get; init; } = string.Empty;
        public byte[] Kdf_Sal { get; init; } = [];
        public long Kdf_Iteraciones { get; init; }
        public byte[] Verificador_Nonce { get; init; } = [];
        public byte[] Verificador { get; init; } = [];
    }

    private sealed class FilaDeSecreto
    {
        public string Id { get; init; } = string.Empty;
        public long Es_De_Carpeta { get; init; }
        public string Protocolo { get; init; } = string.Empty;
        public byte[]? Secreto { get; init; }
        public byte[]? Nonce { get; init; }
    }

    public async Task<FilaDelVault?> LeerAsync(CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        var fila = await cn.QuerySingleOrDefaultAsync<FilaDeVault>(
            """
            SELECT formato, kdf_hash, kdf_sal, kdf_iteraciones, verificador_nonce, verificador
            FROM vault WHERE id = 1
            """).ConfigureAwait(false);

        return fila is null
            ? null
            : new FilaDelVault(
                (int)fila.Formato,
                fila.Kdf_Hash,
                fila.Kdf_Sal,
                (int)fila.Kdf_Iteraciones,
                fila.Verificador_Nonce,
                fila.Verificador);
    }

    public async Task GuardarAsync(FilaDelVault fila, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fila);

        using var cn = _factory.Create();

        await cn.ExecuteAsync(SqlDeLaFila, ParametrosDeLaFila(fila)).ConfigureAwait(false);
    }

    public async Task BorrarAsync(CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        await cn.ExecuteAsync("DELETE FROM vault WHERE id = 1").ConfigureAwait(false);
    }

    public async Task<SecretoGuardado?> LeerSecretoAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        var fila = await cn.QuerySingleOrDefaultAsync<FilaDeSecreto>(
            SelectDe(referencia), new { id = referencia.Id.ToString("D") }).ConfigureAwait(false);

        return fila?.Secreto is { } bytes ? new SecretoGuardado(bytes, fila.Nonce) : null;
    }

    public async Task GuardarSecretoAsync(
        ReferenciaDeSecreto referencia, SecretoGuardado secreto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(secreto);

        using var cn = _factory.Create();

        await cn.ExecuteAsync(
            UpdateDe(referencia),
            new
            {
                id = referencia.Id.ToString("D"),
                secreto = secreto.Bytes,
                nonce = secreto.Nonce,
            }).ConfigureAwait(false);
    }

    public async Task BorrarSecretoAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        await cn.ExecuteAsync(
            UpdateDe(referencia),
            new { id = referencia.Id.ToString("D"), secreto = (byte[]?)null, nonce = (byte[]?)null })
            .ConfigureAwait(false);
    }

    public async Task<bool> HaySecretoAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        return await cn.ExecuteScalarAsync<long>(
            $"SELECT COUNT(1) FROM {Tabla(referencia)} "
            + $"WHERE {Llave(referencia)} = @id AND {Columna(referencia)} IS NOT NULL",
            new { id = referencia.Id.ToString("D") }).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<ReferenciaDeSecreto>> ReferenciasConSecretoAsync(
        CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        var filas = await cn.QueryAsync<FilaDeSecreto>(SelectDeTodos).ConfigureAwait(false);

        return [.. filas.Select(Referencia)];
    }

    public async Task<IReadOnlyList<(ReferenciaDeSecreto Referencia, SecretoGuardado Secreto)>>
        TodosLosSecretosAsync(CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        var filas = await cn.QueryAsync<FilaDeSecreto>(SelectDeTodos).ConfigureAwait(false);

        return
        [
            .. filas.Select(f => (Referencia(f), new SecretoGuardado(f.Secreto!, f.Nonce))),
        ];
    }

    public async Task ReemplazarTodoAsync(
        FilaDelVault? fila,
        IReadOnlyList<(ReferenciaDeSecreto Referencia, SecretoGuardado Secreto)> secretos,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(secretos);

        using var cn = _factory.Create();
        using var tx = cn.BeginTransaction();

        // Una sola transaccion para la fila de la clave maestra y todos los secretos: si esto se
        // corta por la mitad, la base queda como estaba y ningun secreto se vuelve ilegible.
        if (fila is null)
        {
            await cn.ExecuteAsync("DELETE FROM vault WHERE id = 1", transaction: tx)
                .ConfigureAwait(false);
        }
        else
        {
            await cn.ExecuteAsync(SqlDeLaFila, ParametrosDeLaFila(fila), tx).ConfigureAwait(false);
        }

        foreach (var (referencia, secreto) in secretos)
        {
            await cn.ExecuteAsync(
                UpdateDe(referencia),
                new
                {
                    id = referencia.Id.ToString("D"),
                    secreto = secreto.Bytes,
                    nonce = secreto.Nonce,
                },
                tx).ConfigureAwait(false);
        }

        tx.Commit();
    }

    private const string SqlDeLaFila = """
        INSERT INTO vault (id, formato, kdf_hash, kdf_sal, kdf_iteraciones,
                           verificador_nonce, verificador, creado_en)
        VALUES (1, @Formato, @KdfHash, @KdfSal, @KdfIteraciones,
                @VerificadorNonce, @Verificador, @Ahora)
        ON CONFLICT(id) DO UPDATE SET
            formato = @Formato,
            kdf_hash = @KdfHash,
            kdf_sal = @KdfSal,
            kdf_iteraciones = @KdfIteraciones,
            verificador_nonce = @VerificadorNonce,
            verificador = @Verificador
        """;

    // Las conexiones aportan su unico secreto; las carpetas, uno por protocolo. Se leen juntas
    // porque el recifrado masivo los necesita todos y no distingue de donde salen.
    private const string SelectDeTodos = """
        SELECT id, 0 AS es_de_carpeta, protocol AS protocolo, secreto, secreto_nonce AS nonce
        FROM connections WHERE secreto IS NOT NULL
        UNION ALL
        SELECT folder_id, 1, 'Rdp', rdp_secreto, rdp_secreto_nonce
        FROM folder_settings WHERE rdp_secreto IS NOT NULL
        UNION ALL
        SELECT folder_id, 1, 'Ssh', ssh_secreto, ssh_secreto_nonce
        FROM folder_settings WHERE ssh_secreto IS NOT NULL
        UNION ALL
        SELECT folder_id, 1, 'Web', web_secreto, web_secreto_nonce
        FROM folder_settings WHERE web_secreto IS NOT NULL
        """;

    private static object ParametrosDeLaFila(FilaDelVault fila) => new
    {
        fila.Formato,
        fila.KdfHash,
        fila.KdfSal,
        fila.KdfIteraciones,
        fila.VerificadorNonce,
        fila.Verificador,
        Ahora = DateTimeOffset.Now.ToString("O"),
    };

    private static ReferenciaDeSecreto Referencia(FilaDeSecreto fila)
    {
        var id = Guid.Parse(fila.Id);
        var protocolo = Enum.Parse<Protocol>(fila.Protocolo);

        return fila.Es_De_Carpeta == 1
            ? ReferenciaDeSecreto.DeCarpeta(id, protocolo)
            : ReferenciaDeSecreto.DeConexion(id, protocolo);
    }

    private static string Tabla(ReferenciaDeSecreto r) =>
        r.EsDeCarpeta ? "folder_settings" : "connections";

    private static string Llave(ReferenciaDeSecreto r) =>
        r.EsDeCarpeta ? "folder_id" : "id";

    private static string Columna(ReferenciaDeSecreto r) => r.EsDeCarpeta
        ? r.Protocolo switch
        {
            Protocol.Rdp => "rdp_secreto",
            Protocol.Ssh => "ssh_secreto",
            Protocol.Web => "web_secreto",
            _ => throw new ArgumentOutOfRangeException(nameof(r)),
        }
        : "secreto";

    private static string SelectDe(ReferenciaDeSecreto r) =>
        $"SELECT {Llave(r)} AS id, {(r.EsDeCarpeta ? 1 : 0)} AS es_de_carpeta, "
        + $"'{r.Protocolo}' AS protocolo, {Columna(r)} AS secreto, "
        + $"{Columna(r)}_nonce AS nonce FROM {Tabla(r)} WHERE {Llave(r)} = @id";

    private static string UpdateDe(ReferenciaDeSecreto r) =>
        $"UPDATE {Tabla(r)} SET {Columna(r)} = @secreto, {Columna(r)}_nonce = @nonce "
        + $"WHERE {Llave(r)} = @id";
}
