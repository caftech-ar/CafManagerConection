using CafManagerConection.Infrastructure.Database.Migrations;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <remarks>Dos <c>ALTER TABLE … ADD COLUMN</c> en <c>ssh_settings</c> y <c>folder_settings</c>, sin <c>DROP</c>.</remarks>
public sealed class Migracion004Tests : IDisposable
{
    private readonly string _ruta;

    public Migracion004Tests() =>
        _ruta = Path.Combine(Path.GetTempPath(), $"cmc-mig4-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_ruta);
        }
        catch (IOException)
        {
        }
    }

    private SqliteConnection Abrir()
    {
        var cn = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _ruta, Pooling = false }.ToString());

        cn.Open();
        return cn;
    }

    private static void Correr(SqliteConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static IReadOnlyList<string> Columnas(SqliteConnection cn, string tabla)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT name FROM pragma_table_info('{tabla}')";

        var nombres = new List<string>();
        using var lector = cmd.ExecuteReader();

        while (lector.Read())
        {
            nombres.Add(lector.GetString(0));
        }

        return nombres;
    }

    private SqliteConnection ConLasCuatroMigraciones()
    {
        var cn = Abrir();

        Correr(cn, Migration001_InitialSchema.Sql);
        Correr(cn, Migration002_ColorJerarquiaCatalogo.Sql);
        Correr(cn, Migration003_EtiquetasConfigurables.Sql);
        Correr(cn, Migration004_CertificadoSsh.Sql);

        return cn;
    }

    [Fact]
    public void Se_aplica_sobre_el_esquema_anterior()
    {
        using var cn = ConLasCuatroMigraciones();

        Assert.Contains("ssh_certificate_path", Columnas(cn, "ssh_settings"));
    }

    /// <remarks>La columna se llama <c>ssh_certificate_path</c> en las dos tablas, no <c>certificate_path</c> en una.</remarks>
    [Fact]
    public void La_columna_queda_en_las_dos_tablas_con_el_mismo_nombre()
    {
        using var cn = ConLasCuatroMigraciones();

        Assert.Contains("ssh_certificate_path", Columnas(cn, "ssh_settings"));
        Assert.Contains("ssh_certificate_path", Columnas(cn, "folder_settings"));
    }

    [Fact]
    public void No_se_pierde_ninguna_columna_existente()
    {
        using var cn = ConLasCuatroMigraciones();

        Assert.Contains("private_key_path", Columnas(cn, "ssh_settings"));
        Assert.Contains("ssh_private_key_path", Columnas(cn, "folder_settings"));
        Assert.Contains("known_host_fingerprint", Columnas(cn, "ssh_settings"));
    }

    [Fact]
    public void Es_reanudable_una_base_que_ya_estaba_en_la_version_3_la_recibe_sin_perder_datos()
    {
        using var cn = Abrir();

        Correr(cn, Migration001_InitialSchema.Sql);
        Correr(cn, Migration002_ColorJerarquiaCatalogo.Sql);
        Correr(cn, Migration003_EtiquetasConfigurables.Sql);

        Correr(cn, """
            INSERT INTO connections (id, folder_id, name, protocol, host, sort_order,
                                     created_at, updated_at)
            VALUES ('c1', NULL, 'Servidor', 'Ssh', '192.0.2.1', 0,
                    datetime('now'), datetime('now'));

            INSERT INTO ssh_settings (connection_id, private_key_path)
            VALUES ('c1', 'C:\claves\id_ed25519');
            """);

        Correr(cn, Migration004_CertificadoSsh.Sql);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT private_key_path, ssh_certificate_path FROM ssh_settings WHERE connection_id = 'c1'";

        using var lector = cmd.ExecuteReader();

        Assert.True(lector.Read());
        Assert.Equal(@"C:\claves\id_ed25519", lector.GetString(0));
        Assert.True(lector.IsDBNull(1));
    }
}
