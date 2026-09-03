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
        public byte[]? Clave_Dpapi { get; init; }
        public byte[]? Kdf_Sal { get; init; }
        public long? Kdf_Iteraciones { get; init; }
        public byte[]? Clave_Maestra_Nonce { get; init; }
        public byte[]? Clave_Maestra_Envuelta { get; init; }
    }

    private sealed class FilaDeCredencial
    {
        public string Clave { get; init; } = string.Empty;
        public string Usuario { get; init; } = string.Empty;
        public string? Dominio { get; init; }
        public byte[] Secreto_Nonce { get; init; } = [];
        public byte[] Secreto { get; init; } = [];
    }

    public async Task<FilaDelVault?> LeerAsync(CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        var fila = await cn.QuerySingleOrDefaultAsync<FilaDeVault>(
            """
            SELECT formato, clave_dpapi, kdf_sal, kdf_iteraciones,
                   clave_maestra_nonce, clave_maestra_envuelta
            FROM vault WHERE id = 1
            """).ConfigureAwait(false);

        return fila is null
            ? null
            : new FilaDelVault(
                (int)fila.Formato,
                fila.Clave_Dpapi,
                fila.Kdf_Sal,
                (int?)fila.Kdf_Iteraciones,
                fila.Clave_Maestra_Nonce,
                fila.Clave_Maestra_Envuelta);
    }

    public async Task GuardarAsync(FilaDelVault fila, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fila);

        if (fila.EstaHuerfano)
        {
            throw new InvalidOperationException(
                "Un vault sin envoltura de DPAPI ni de clave maestra no se puede abrir nunca más.");
        }

        using var cn = _factory.Create();

        await cn.ExecuteAsync(
            """
            INSERT INTO vault (id, formato, clave_dpapi, kdf_sal, kdf_iteraciones,
                               clave_maestra_nonce, clave_maestra_envuelta, creado_en)
            VALUES (1, @Formato, @ClaveDpapi, @KdfSal, @KdfIteraciones,
                    @ClaveMaestraNonce, @ClaveMaestraEnvuelta, @Ahora)
            ON CONFLICT(id) DO UPDATE SET
                formato = @Formato,
                clave_dpapi = @ClaveDpapi,
                kdf_sal = @KdfSal,
                kdf_iteraciones = @KdfIteraciones,
                clave_maestra_nonce = @ClaveMaestraNonce,
                clave_maestra_envuelta = @ClaveMaestraEnvuelta
            """,
            new
            {
                fila.Formato,
                fila.ClaveDpapi,
                fila.KdfSal,
                fila.KdfIteraciones,
                fila.ClaveMaestraNonce,
                fila.ClaveMaestraEnvuelta,
                Ahora = DateTimeOffset.Now.ToString("O"),
            }).ConfigureAwait(false);
    }

    public async Task<CredencialCifrada?> LeerCredencialAsync(
        string clave, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        var fila = await cn.QuerySingleOrDefaultAsync<FilaDeCredencial>(
            """
            SELECT clave, usuario, dominio, secreto_nonce, secreto
            FROM vault_credenciales WHERE clave = @clave
            """,
            new { clave }).ConfigureAwait(false);

        return fila is null
            ? null
            : new CredencialCifrada(
                fila.Clave,
                fila.Usuario,
                fila.Dominio,
                new SobreCifrado(fila.Secreto_Nonce, fila.Secreto));
    }

    public async Task GuardarCredencialAsync(
        CredencialCifrada credencial, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credencial);

        using var cn = _factory.Create();

        await cn.ExecuteAsync(
            """
            INSERT INTO vault_credenciales
                (clave, usuario, dominio, secreto_nonce, secreto, guardado_en)
            VALUES (@Clave, @Usuario, @Dominio, @Nonce, @Secreto, @Ahora)
            ON CONFLICT(clave) DO UPDATE SET
                usuario = @Usuario,
                dominio = @Dominio,
                secreto_nonce = @Nonce,
                secreto = @Secreto,
                guardado_en = @Ahora
            """,
            new
            {
                credencial.Clave,
                credencial.Usuario,
                credencial.Dominio,
                Nonce = credencial.Sobre.Nonce,
                Secreto = credencial.Sobre.Cifrado,
                Ahora = DateTimeOffset.Now.ToString("O"),
            }).ConfigureAwait(false);
    }

    public async Task BorrarCredencialAsync(string clave, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        await cn.ExecuteAsync(
            "DELETE FROM vault_credenciales WHERE clave = @clave", new { clave })
            .ConfigureAwait(false);
    }

    public async Task<bool> ExisteCredencialAsync(string clave, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        return await cn.ExecuteScalarAsync<long>(
            "SELECT COUNT(1) FROM vault_credenciales WHERE clave = @clave", new { clave })
            .ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<string>> ClavesAsync(
        string prefijo, CancellationToken ct = default)
    {
        using var cn = _factory.Create();

        var claves = await cn.QueryAsync<string>(
            "SELECT clave FROM vault_credenciales WHERE clave LIKE @patron ORDER BY clave",
            new { patron = prefijo + "%" }).ConfigureAwait(false);

        return [.. claves];
    }

    /// <summary>Recifrar todas las credenciales cuando cambia la clave del vault. Va en una transacción: a medias, la mitad no se descifra con ninguna de las dos claves.</summary>
    public async Task RecifrarTodoAsync(
        Func<SobreCifrado, SobreCifrado> recifrar, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recifrar);

        using var cn = _factory.Create();
        using var tx = cn.BeginTransaction();

        var filas = (await cn.QueryAsync<FilaDeCredencial>(
            "SELECT clave, usuario, dominio, secreto_nonce, secreto FROM vault_credenciales",
            transaction: tx).ConfigureAwait(false)).ToList();

        foreach (var fila in filas)
        {
            var nuevo = recifrar(new SobreCifrado(fila.Secreto_Nonce, fila.Secreto));

            await cn.ExecuteAsync(
                """
                UPDATE vault_credenciales
                SET secreto_nonce = @Nonce, secreto = @Secreto
                WHERE clave = @Clave
                """,
                new { fila.Clave, Nonce = nuevo.Nonce, Secreto = nuevo.Cifrado },
                tx).ConfigureAwait(false);
        }

        tx.Commit();
    }
}
