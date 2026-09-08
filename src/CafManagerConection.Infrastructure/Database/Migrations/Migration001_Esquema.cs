namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>El esquema entero. Corresponde a <c>user_version = 1</c>.</summary>
public static class Migration001_Esquema
{
    public const int Version = 1;

    // Es el estado final de los ocho pasos anteriores, no su concatenacion: las columnas que un
    // paso creaba y otro borraba no estan, y las etiquetas semilla llegan con el codigo y el orden
    // que les dejaba el ultimo.
    public const string Sql = """
        CREATE TABLE tags (
            id          TEXT PRIMARY KEY NOT NULL,
            code        TEXT NOT NULL,
            name        TEXT NOT NULL,
            color       TEXT NOT NULL,
            sort_order  INTEGER NOT NULL DEFAULT 0,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL
        );

        -- NOCASE porque «prd» y «PRD» son el mismo codigo escrito de dos formas, y dos etiquetas
        -- con el mismo codigo serian indistinguibles justo donde menos espacio hay.
        CREATE UNIQUE INDEX ux_tags_code ON tags(code COLLATE NOCASE);
        CREATE UNIQUE INDEX ux_tags_name ON tags(name COLLATE NOCASE);

        -- Identificadores fijos: permiten que dos bases distintas hablen del mismo «Produccion».
        INSERT INTO tags (id, code, name, color, sort_order, created_at, updated_at) VALUES
            ('11111111-0000-4000-8000-000000000001', 'PRD',  'Producción',        'rojo',    1, datetime('now'), datetime('now')),
            ('11111111-0000-4000-8000-000000000002', 'PRE',  'PreProducción',     'ambar',   2, datetime('now'), datetime('now')),
            ('11111111-0000-4000-8000-000000000005', 'QA',   'Quality Assurance', 'violeta', 3, datetime('now'), datetime('now')),
            ('11111111-0000-4000-8000-000000000003', 'CAPA', 'Capacitación',      'cyan',    4, datetime('now'), datetime('now')),
            ('11111111-0000-4000-8000-000000000004', 'DESA', 'Desarrollo',        'verde',   5, datetime('now'), datetime('now'));

        CREATE TABLE connection_folders (
            id          TEXT PRIMARY KEY NOT NULL,
            parent_id   TEXT NULL REFERENCES connection_folders(id) ON DELETE CASCADE,
            name        TEXT NOT NULL,
            sort_order  INTEGER NOT NULL DEFAULT 0,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL,
            icon_color  TEXT NULL,
            description TEXT NULL,
            icon_key    TEXT NULL
        );
        CREATE INDEX ix_folders_parent ON connection_folders(parent_id, sort_order);

        -- Un usuario y un dominio para los tres secretos: la ventana de carpeta los pide una sola
        -- vez y los aplica al protocolo que se este cargando.
        --
        -- ON DELETE SET NULL en tag_id: borrar una etiqueta del catalogo no puede borrar los
        -- servidores que la usaban.
        CREATE TABLE folder_settings (
            folder_id                        TEXT PRIMARY KEY NOT NULL
                                             REFERENCES connection_folders(id) ON DELETE CASCADE,
            username                         TEXT NULL,
            domain                           TEXT NULL,
            port                             INTEGER NULL
                                             CHECK (port IS NULL OR port BETWEEN 1 AND 65535),
            rdp_secreto                      BLOB NULL,
            rdp_secreto_nonce                BLOB NULL,
            ssh_secreto                      BLOB NULL,
            ssh_secreto_nonce                BLOB NULL,
            web_secreto                      BLOB NULL,
            web_secreto_nonce                BLOB NULL,
            rdp_clipboard_enabled            INTEGER NULL,
            rdp_fit_to_tab                   INTEGER NULL,
            rdp_ignore_certificate_warnings  INTEGER NULL,
            ssh_auth_method                  TEXT NULL
                                             CHECK (ssh_auth_method IS NULL
                                                    OR ssh_auth_method IN ('Password', 'PrivateKey')),
            ssh_private_key_path             TEXT NULL,
            ssh_certificate_path             TEXT NULL,
            ssh_keep_alive_seconds           INTEGER NULL
                                             CHECK (ssh_keep_alive_seconds IS NULL
                                                    OR ssh_keep_alive_seconds BETWEEN 0 AND 3600),
            tag_id                           TEXT NULL REFERENCES tags(id) ON DELETE SET NULL,

            CHECK (rdp_secreto IS NOT NULL OR rdp_secreto_nonce IS NULL),
            CHECK (ssh_secreto IS NOT NULL OR ssh_secreto_nonce IS NULL),
            CHECK (web_secreto IS NOT NULL OR web_secreto_nonce IS NULL)
        );
        CREATE INDEX ix_folder_settings_tag ON folder_settings(tag_id);

        -- El nonce en NULL con secreto presente significa que el secreto esta en claro: es el modo
        -- sin clave maestra. Un nonce sin secreto no significa nada y el CHECK lo impide.
        CREATE TABLE connections (
            id                   TEXT PRIMARY KEY NOT NULL,
            folder_id            TEXT NULL REFERENCES connection_folders(id) ON DELETE CASCADE,
            name                 TEXT NOT NULL,
            protocol             TEXT NOT NULL CHECK (protocol IN ('Rdp', 'Ssh', 'Web')),
            host                 TEXT NOT NULL,
            port                 INTEGER NULL CHECK (port IS NULL OR port BETWEEN 1 AND 65535),
            username             TEXT NULL,
            secreto              BLOB NULL,
            secreto_nonce        BLOB NULL,
            notes                TEXT NULL,
            created_at           TEXT NOT NULL,
            updated_at           TEXT NOT NULL,
            last_connected_at    TEXT NULL,
            sort_order           INTEGER NOT NULL DEFAULT 0,
            icon_color           TEXT NULL,
            parent_connection_id TEXT NULL REFERENCES connections(id) ON DELETE CASCADE,
            description          TEXT NULL,
            documentation_url    TEXT NULL,
            is_favorite          INTEGER NOT NULL DEFAULT 0,
            custom_fields        TEXT NULL,
            tag_id               TEXT NULL REFERENCES tags(id) ON DELETE SET NULL,
            icon_key             TEXT NULL,

            CHECK (secreto IS NOT NULL OR secreto_nonce IS NULL)
        );
        CREATE INDEX ix_connections_folder ON connections(folder_id, sort_order);
        CREATE INDEX ix_connections_search ON connections(name, host, username);
        CREATE INDEX ix_connections_parent ON connections(parent_connection_id, sort_order);
        CREATE INDEX ix_connections_tag    ON connections(tag_id);

        -- Indice parcial: solo entran las favoritas, que son unas pocas de cientos.
        CREATE INDEX ix_connections_favorite ON connections(is_favorite) WHERE is_favorite = 1;

        CREATE TABLE rdp_settings (
            connection_id               TEXT PRIMARY KEY NOT NULL
                                        REFERENCES connections(id) ON DELETE CASCADE,
            domain                      TEXT NULL,
            clipboard_enabled           INTEGER NULL,
            fit_to_tab                  INTEGER NULL,
            ignore_certificate_warnings INTEGER NULL,
            start_full_screen           INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE ssh_settings (
            connection_id          TEXT PRIMARY KEY NOT NULL
                                   REFERENCES connections(id) ON DELETE CASCADE,
            auth_method            TEXT NULL
                                   CHECK (auth_method IS NULL
                                          OR auth_method IN ('Password', 'PrivateKey')),
            private_key_path       TEXT NULL,
            ssh_certificate_path   TEXT NULL,
            known_host_fingerprint TEXT NULL,
            keep_alive_seconds     INTEGER NULL
                                   CHECK (keep_alive_seconds IS NULL
                                          OR keep_alive_seconds BETWEEN 0 AND 3600),
            encoding               TEXT NOT NULL DEFAULT 'UTF-8'
        );

        CREATE TABLE web_settings (
            connection_id  TEXT PRIMARY KEY NOT NULL
                           REFERENCES connections(id) ON DELETE CASCADE,
            url            TEXT NOT NULL,
            browser        TEXT NULL,
            private_window INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE ssh_tunnels (
            id            TEXT PRIMARY KEY NOT NULL,
            connection_id TEXT NOT NULL REFERENCES connections(id) ON DELETE CASCADE,
            name          TEXT NOT NULL,
            local_port    INTEGER NOT NULL CHECK (local_port BETWEEN 1 AND 65535),
            remote_host   TEXT NOT NULL,
            remote_port   INTEGER NOT NULL CHECK (remote_port BETWEEN 1 AND 65535),
            auto_start    INTEGER NOT NULL DEFAULT 0,
            sort_order    INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX ix_tunnels_connection ON ssh_tunnels(connection_id, sort_order);

        CREATE TABLE connection_history (
            id               TEXT PRIMARY KEY NOT NULL,
            connection_id    TEXT NOT NULL REFERENCES connections(id) ON DELETE CASCADE,
            attempted_at     TEXT NOT NULL,
            outcome          TEXT NOT NULL CHECK (outcome IN ('Success', 'Failed', 'Cancelled')),
            failure_reason   TEXT NULL,
            duration_seconds INTEGER NULL
        );
        CREATE INDEX ix_history_connection ON connection_history(connection_id, attempted_at DESC);

        CREATE TABLE application_settings (
            key   TEXT PRIMARY KEY NOT NULL,
            value TEXT NOT NULL
        );

        -- Solo existe cuando hay clave maestra: que la fila este es lo que dice que hay que
        -- pedirla. Nada de esto es secreto salvo el verificador, y sin la sal y las iteraciones el
        -- vault no se abre nunca mas.
        CREATE TABLE vault (
            id                INTEGER PRIMARY KEY CHECK (id = 1),
            formato           INTEGER NOT NULL,
            kdf_hash          TEXT    NOT NULL,
            kdf_sal           BLOB    NOT NULL,
            kdf_iteraciones   INTEGER NOT NULL,
            verificador_nonce BLOB    NOT NULL,
            verificador       BLOB    NOT NULL,
            creado_en         TEXT    NOT NULL
        );
        """;
}
