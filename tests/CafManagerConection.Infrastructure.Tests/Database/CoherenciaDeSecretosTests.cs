using CafManagerConection.Domain.Connections;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

public class CoherenciaDeSecretosTests
{
    [Fact]
    public async Task Una_base_sin_clave_maestra_y_sin_secretos_es_coherente()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        Assert.True(CoherenciaDeSecretos.Comprobar(db.Factory).Coherente);
    }

    [Fact]
    public async Task Sin_clave_maestra_los_secretos_en_claro_son_coherentes()
    {
        using var db = new TempDatabase();
        await GuardarSecretoAsync(db, cifrado: false);

        Assert.True(CoherenciaDeSecretos.Comprobar(db.Factory).Coherente);
    }

    [Fact]
    public async Task Un_secreto_cifrado_sin_clave_maestra_se_informa()
    {
        using var db = new TempDatabase();
        await GuardarSecretoAsync(db, cifrado: true);

        var estado = CoherenciaDeSecretos.Comprobar(db.Factory);

        Assert.False(estado.Coherente);
        Assert.Contains("clave maestra", estado.Motivo!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_secreto_en_claro_con_clave_maestra_se_informa()
    {
        using var db = new TempDatabase();
        await GuardarSecretoAsync(db, cifrado: false);
        Ejecutar(db, """
            INSERT INTO vault (
                id, formato, kdf_hash, kdf_sal, kdf_iteraciones, verificador_nonce, verificador,
                creado_en)
            VALUES (1, 3, 'SHA512', X'01', 600000, X'02', X'03', '2026-01-01T00:00:00.0000000Z');
            """);

        var estado = CoherenciaDeSecretos.Comprobar(db.Factory);

        Assert.False(estado.Coherente);
        Assert.Contains("sin cifrar", estado.Motivo!, StringComparison.Ordinal);
    }

    private static async Task GuardarSecretoAsync(TempDatabase db, bool cifrado)
    {
        await db.CreateInitializer().InitializeAsync();

        var repo = new ConnectionRepository(db.Factory);
        var c = new Connection(Guid.NewGuid(), "S", Protocol.Ssh, "192.0.2.9");
        await repo.AddAsync(new ConnectionRecord(c, Ssh: new SshSettings { ConnectionId = c.Id }));

        Ejecutar(db, $"""
            UPDATE connections
               SET secreto = X'0102', secreto_nonce = {(cifrado ? "X'0304'" : "NULL")}
             WHERE id = '{c.Id:D}';
            """);
    }

    private static void Ejecutar(TempDatabase db, string sql)
    {
        using SqliteConnection connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
