using CafManagerConection.Infrastructure.Database;
using CafManagerConection.Infrastructure.Database.Migrations;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests.Database;

/// <summary>La 004 corrida sobre una base en la versión 3 con datos que ejercitan cada arreglo.</summary>
public class Migration004Tests
{
    private const string Fecha = "2026-08-24T00:00:00.0000000Z";

    [Fact]
    public async Task El_usuario_compartido_se_promueve_a_los_tres_protocolos()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal("operador", Texto(db, "rdp_username", "f1"));
        Assert.Equal("operador", Texto(db, "ssh_username", "f1"));
        Assert.Equal("operador", Texto(db, "web_username", "f1"));
    }

    [Fact]
    public async Task El_usuario_por_protocolo_le_gana_al_compartido()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal("root", Texto(db, "ssh_username", "f2"));
        Assert.Equal("compartido", Texto(db, "rdp_username", "f2"));
        Assert.Equal("compartido", Texto(db, "web_username", "f2"));
    }

    [Fact]
    public async Task El_puerto_compartido_se_promueve_a_los_tres_protocolos()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal("2222", Texto(db, "ssh_port", "f3"));
        Assert.Equal("2222", Texto(db, "rdp_port", "f3"));
    }

    [Fact]
    public async Task Las_columnas_compartidas_de_carpeta_ya_no_estan()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.DoesNotContain("username", Columnas(db, "folder_settings"));
        Assert.DoesNotContain("port", Columnas(db, "folder_settings"));
        Assert.DoesNotContain("rdp_fit_to_tab", Columnas(db, "folder_settings"));
    }

    [Fact]
    public async Task La_marca_de_conexion_rapida_pasa_a_columna_y_sale_del_json()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal("1", Escalar(db, "SELECT es_rapida FROM connections WHERE id = 'c1';"));
        Assert.Equal(
            "7",
            Escalar(
                db,
                "SELECT json_extract(custom_fields, '$.\"cmc:rdpRendimiento\"') "
                + "FROM connections WHERE id = 'c1';"));
        Assert.Null(
            Escalar(
                db,
                "SELECT json_extract(custom_fields, '$.\"cmc:conexionRapida\"') "
                + "FROM connections WHERE id = 'c1';"));
    }

    [Fact]
    public async Task Una_conexion_rapida_sin_otra_clave_queda_sin_campos_propios()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal("1", Escalar(db, "SELECT es_rapida FROM connections WHERE id = 'c2';"));
        Assert.Null(Escalar(db, "SELECT custom_fields FROM connections WHERE id = 'c2';"));
    }

    [Fact]
    public async Task Un_json_invalido_se_anula_en_vez_de_abortar_la_migracion()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Null(Escalar(db, "SELECT custom_fields FROM connections WHERE id = 'c3';"));
        Assert.Equal("0", Escalar(db, "SELECT es_rapida FROM connections WHERE id = 'c3';"));
    }

    [Fact]
    public async Task Una_hija_queda_en_la_carpeta_de_su_padre()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal("f1", Escalar(db, "SELECT folder_id FROM connections WHERE id = 'h1';"));
    }

    [Fact]
    public async Task Una_hija_cuyo_padre_no_tiene_carpeta_queda_sin_carpeta()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Null(Escalar(db, "SELECT folder_id FROM connections WHERE id = 'h2';"));
    }

    [Fact]
    public async Task La_bandera_de_ventana_propia_conserva_su_valor_con_el_nombre_nuevo()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal(
            "1",
            Escalar(db, "SELECT abre_en_ventana_propia FROM rdp_settings WHERE connection_id = 'c1';"));
        Assert.DoesNotContain("start_full_screen", Columnas(db, "rdp_settings"));
        Assert.DoesNotContain("fit_to_tab", Columnas(db, "rdp_settings"));
    }

    [Fact]
    public async Task La_nota_que_dejo_el_importador_se_vacia_y_la_escrita_a_mano_queda()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Null(Escalar(db, "SELECT notes FROM connections WHERE id = 'c1';"));
        Assert.Equal(
            "Pedir el token al de guardia.",
            Escalar(db, "SELECT notes FROM connections WHERE id = 'c2';"));
    }

    [Fact]
    public async Task El_historial_deja_de_contradecirse()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Null(Escalar(db, "SELECT failure_reason FROM connection_history WHERE id = 'e1';"));
        Assert.Equal(
            "Other",
            Escalar(db, "SELECT failure_reason FROM connection_history WHERE id = 'e2';"));
        Assert.Equal(
            "HostUnreachable",
            Escalar(db, "SELECT failure_reason FROM connection_history WHERE id = 'e3';"));
    }

    [Fact]
    public async Task Las_fechas_de_las_etiquetas_quedan_en_el_formato_del_resto_de_la_base()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal(
            "2026-09-07T20:41:41.0000000Z",
            Escalar(db, "SELECT created_at FROM tags WHERE code = 'PRD';"));
    }

    [Fact]
    public async Task Las_columnas_sin_lector_ya_no_estan()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.DoesNotContain("documentation_url", Columnas(db, "connections"));
        Assert.DoesNotContain("last_connected_at", Columnas(db, "connections"));
        Assert.DoesNotContain("encoding", Columnas(db, "ssh_settings"));
        Assert.DoesNotContain("sort_order", Columnas(db, "ssh_tunnels"));
    }

    [Fact]
    public async Task El_esquema_queda_con_los_indices_que_alguna_consulta_usa()
    {
        using var db = await BaseEnLaVersion3Async();

        Assert.Equal(
            [
                "ix_connections_folder",
                "ix_connections_parent",
                "ix_connections_tag",
                "ix_folder_settings_tag",
                "ix_folders_parent",
                "ix_history_connection",
                "ix_tunnels_connection",
                "ux_tags_code",
                "ux_tags_name",
            ],
            Indices(db));
    }

    [Theory]
    [InlineData("UPDATE connections SET is_favorite = 2 WHERE id = 'c1';")]
    [InlineData("UPDATE connections SET name = '   ' WHERE id = 'c1';")]
    [InlineData("UPDATE connections SET host = '' WHERE id = 'c1';")]
    [InlineData("UPDATE connections SET custom_fields = '{no es json' WHERE id = 'c1';")]
    [InlineData("UPDATE connections SET es_rapida = 5 WHERE id = 'c1';")]
    [InlineData("UPDATE connection_folders SET name = ' ' WHERE id = 'f1';")]
    [InlineData("UPDATE folder_settings SET ssh_port = 70000 WHERE folder_id = 'f1';")]
    [InlineData("UPDATE rdp_settings SET abre_en_ventana_propia = 3 WHERE connection_id = 'c1';")]
    [InlineData("UPDATE connection_history SET failure_reason = 'Other' WHERE id = 'e1';")]
    [InlineData("UPDATE connection_history SET failure_reason = NULL WHERE id = 'e2';")]
    public async Task El_esquema_rechaza_lo_que_antes_entraba(string sql)
    {
        using var db = await BaseEnLaVersion3Async();

        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }

    /// <summary>Arma una base en la versión 3 con datos que tocan cada arreglo y la deja ya migrada.</summary>
    private static async Task<TempDatabase> BaseEnLaVersion3Async()
    {
        var db = new TempDatabase();

        try
        {
            using (var connection = db.Factory.Create())
            {
                Ejecutar(connection, Migration001_Esquema.Sql);
                Ejecutar(connection, Migration002_CamposDeCarpeta.Sql);
                Ejecutar(connection, Migration003_UsuarioYPuertoPorProtocolo.Sql);
                Ejecutar(connection, Datos);
                Ejecutar(connection, "PRAGMA user_version = 3;");
            }

            await db.CreateInitializer().InitializeAsync();
            return db;
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }

    private const string Datos = $$"""
        UPDATE tags SET created_at = '2026-09-07 20:41:41', updated_at = '2026-09-07 20:41:41';

        INSERT INTO connection_folders (id, name, created_at, updated_at) VALUES
            ('f1', 'Producción', '{{Fecha}}', '{{Fecha}}'),
            ('f2', 'Desarrollo', '{{Fecha}}', '{{Fecha}}'),
            ('f3', 'Pruebas',    '{{Fecha}}', '{{Fecha}}');

        INSERT INTO folder_settings (folder_id, username, ssh_username, port) VALUES
            ('f1', 'operador',   NULL,   NULL),
            ('f2', 'compartido', 'root', NULL),
            ('f3', NULL,         NULL,   2222);

        INSERT INTO connections
            (id, folder_id, name, protocol, host, created_at, updated_at, custom_fields,
             parent_connection_id, documentation_url, last_connected_at, notes)
        VALUES
            ('c1', 'f1', 'Uno',    'Rdp', '192.0.2.10', '{{Fecha}}', '{{Fecha}}',
             '{"cmc:conexionRapida":"True","cmc:rdpRendimiento":"7"}', NULL, 'http://x', '{{Fecha}}',
             'Importado de Rdm como RDPConfigured.'),
            ('c2', 'f1', 'Dos',    'Ssh', '192.0.2.11', '{{Fecha}}', '{{Fecha}}',
             '{"cmc:conexionRapida":"True"}', NULL, NULL, NULL, 'Pedir el token al de guardia.'),
            ('c3', 'f1', 'Tres',   'Ssh', '192.0.2.12', '{{Fecha}}', '{{Fecha}}',
             'esto no es json', NULL, NULL, NULL, NULL),
            ('p1', 'f1', 'Padre',  'Ssh', '192.0.2.13', '{{Fecha}}', '{{Fecha}}', NULL, NULL, NULL, NULL, NULL),
            ('h1', 'f2', 'Hija',   'Ssh', '192.0.2.14', '{{Fecha}}', '{{Fecha}}', NULL, 'p1', NULL, NULL, NULL),
            ('p2', NULL, 'Suelto', 'Ssh', '192.0.2.15', '{{Fecha}}', '{{Fecha}}', NULL, NULL, NULL, NULL, NULL),
            ('h2', 'f1', 'Hija2',  'Ssh', '192.0.2.16', '{{Fecha}}', '{{Fecha}}', NULL, 'p2', NULL, NULL, NULL);

        INSERT INTO rdp_settings (connection_id, start_full_screen, fit_to_tab)
        VALUES ('c1', 1, 1);

        INSERT INTO ssh_settings (connection_id, encoding) VALUES ('c2', 'UTF-8');

        INSERT INTO connection_history (id, connection_id, attempted_at, outcome, failure_reason)
        VALUES
            ('e1', 'c1', '{{Fecha}}', 'Success',   'UnexpectedDisconnect'),
            ('e2', 'c1', '{{Fecha}}', 'Failed',    NULL),
            ('e3', 'c1', '{{Fecha}}', 'Failed',    'HostUnreachable'),
            ('e4', 'c1', '{{Fecha}}', 'Cancelled', NULL);
        """;

    private static void Ejecutar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string? Texto(TempDatabase db, string columna, string carpeta) =>
        Escalar(db, $"SELECT {columna} FROM folder_settings WHERE folder_id = '{carpeta}';");

    private static string? Escalar(TempDatabase db, string sql)
    {
        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var valor = cmd.ExecuteScalar();

        return valor is null or DBNull ? null : Convert.ToString(valor);
    }

    private static List<string> Columnas(TempDatabase db, string tabla) =>
        Lista(db, $"SELECT name FROM pragma_table_info('{tabla}');");

    private static List<string> Indices(TempDatabase db) =>
        Lista(
            db,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL "
            + "ORDER BY name;");

    private static List<string> Lista(TempDatabase db, string sql)
    {
        using var connection = db.Factory.Create();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();

        var valores = new List<string>();

        while (reader.Read())
        {
            valores.Add(reader.GetString(0));
        }

        return valores;
    }
}
