namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Retira las columnas e índices sin lector, cierra las duplicaciones y sube al esquema las restricciones que vivían en código. Corresponde a <c>user_version = 4</c>.</summary>
public static class Migration004_Saneamiento
{
    public const int Version = 4;

    // Las tablas que cambian un CHECK o pierden una columna indexada se recrean con el
    // procedimiento que exige SQLite: crear, copiar, bajar, renombrar y recrear los indices. El
    // motor apaga las claves foraneas antes de abrir la transaccion, porque si no el DROP TABLE
    // dispara las cascadas y se lleva las filas hijas.
    public const string Sql = """
        DROP INDEX ix_connections_search;
        DROP INDEX ix_connections_favorite;

        ALTER TABLE ssh_settings DROP COLUMN encoding;

        -- La semilla las escribia con datetime('now'), que no lleva T ni Z y ordena distinto que
        -- el resto de las fechas de la base.
        UPDATE tags SET created_at = REPLACE(created_at, ' ', 'T') || '.0000000Z'
         WHERE created_at NOT LIKE '%T%';
        UPDATE tags SET updated_at = REPLACE(updated_at, ' ', 'T') || '.0000000Z'
         WHERE updated_at NOT LIKE '%T%';

        CREATE TABLE connection_folders_nueva (
            id          TEXT PRIMARY KEY NOT NULL,
            parent_id   TEXT NULL REFERENCES connection_folders(id) ON DELETE CASCADE,
            name        TEXT NOT NULL CHECK (TRIM(name) <> ''),
            sort_order  INTEGER NOT NULL DEFAULT 0,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL,
            icon_color  TEXT NULL,
            description TEXT NULL,
            icon_key    TEXT NULL
        );

        INSERT INTO connection_folders_nueva (
            id, parent_id, name, sort_order, created_at, updated_at,
            icon_color, description, icon_key)
        SELECT id, parent_id, name, sort_order, created_at, updated_at,
               icon_color, description, icon_key
          FROM connection_folders;

        DROP TABLE connection_folders;
        ALTER TABLE connection_folders_nueva RENAME TO connection_folders;
        CREATE INDEX ix_folders_parent ON connection_folders(parent_id, sort_order);

        -- El usuario y el puerto compartidos se promueven a los tres protocolos y se van: el valor
        -- por protocolo ya existia y ninguna ventana podia escribir el compartido.
        CREATE TABLE folder_settings_nueva (
            folder_id                        TEXT PRIMARY KEY NOT NULL
                                             REFERENCES connection_folders(id) ON DELETE CASCADE,
            domain                           TEXT NULL,
            rdp_secreto                      BLOB NULL,
            rdp_secreto_nonce                BLOB NULL,
            ssh_secreto                      BLOB NULL,
            ssh_secreto_nonce                BLOB NULL,
            web_secreto                      BLOB NULL,
            web_secreto_nonce                BLOB NULL,
            rdp_clipboard_enabled            INTEGER NULL,
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
            custom_fields                    TEXT NULL
                                             CHECK (custom_fields IS NULL
                                                    OR json_valid(custom_fields)),
            rdp_username                     TEXT NULL,
            ssh_username                     TEXT NULL,
            web_username                     TEXT NULL,
            rdp_port                         INTEGER NULL
                                             CHECK (rdp_port IS NULL
                                                    OR rdp_port BETWEEN 1 AND 65535),
            ssh_port                         INTEGER NULL
                                             CHECK (ssh_port IS NULL
                                                    OR ssh_port BETWEEN 1 AND 65535),
            web_port                         INTEGER NULL
                                             CHECK (web_port IS NULL
                                                    OR web_port BETWEEN 1 AND 65535),

            CHECK (rdp_secreto IS NOT NULL OR rdp_secreto_nonce IS NULL),
            CHECK (ssh_secreto IS NOT NULL OR ssh_secreto_nonce IS NULL),
            CHECK (web_secreto IS NOT NULL OR web_secreto_nonce IS NULL)
        );

        INSERT INTO folder_settings_nueva (
            folder_id, domain,
            rdp_secreto, rdp_secreto_nonce, ssh_secreto, ssh_secreto_nonce,
            web_secreto, web_secreto_nonce,
            rdp_clipboard_enabled, rdp_ignore_certificate_warnings,
            ssh_auth_method, ssh_private_key_path, ssh_certificate_path, ssh_keep_alive_seconds,
            tag_id, custom_fields,
            rdp_username, ssh_username, web_username, rdp_port, ssh_port, web_port)
        SELECT folder_id, domain,
               rdp_secreto, rdp_secreto_nonce, ssh_secreto, ssh_secreto_nonce,
               web_secreto, web_secreto_nonce,
               rdp_clipboard_enabled, rdp_ignore_certificate_warnings,
               ssh_auth_method, ssh_private_key_path, ssh_certificate_path, ssh_keep_alive_seconds,
               tag_id,
               CASE WHEN custom_fields IS NULL OR NOT json_valid(custom_fields) THEN NULL
                    ELSE custom_fields END,
               COALESCE(rdp_username, username),
               COALESCE(ssh_username, username),
               COALESCE(web_username, username),
               COALESCE(rdp_port, port),
               COALESCE(ssh_port, port),
               COALESCE(web_port, port)
          FROM folder_settings;

        DROP TABLE folder_settings;
        ALTER TABLE folder_settings_nueva RENAME TO folder_settings;
        CREATE INDEX ix_folder_settings_tag ON folder_settings(tag_id);

        -- es_rapida sale de la clave reservada porque decide si la conexion se ve en el arbol y si
        -- se barre al arrancar, y eso no puede depender de que el JSON se pueda leer.
        CREATE TABLE connections_nueva (
            id                   TEXT PRIMARY KEY NOT NULL,
            folder_id            TEXT NULL REFERENCES connection_folders(id) ON DELETE CASCADE,
            name                 TEXT NOT NULL CHECK (TRIM(name) <> ''),
            protocol             TEXT NOT NULL CHECK (protocol IN ('Rdp', 'Ssh', 'Web')),
            host                 TEXT NOT NULL CHECK (TRIM(host) <> ''),
            port                 INTEGER NULL CHECK (port IS NULL OR port BETWEEN 1 AND 65535),
            username             TEXT NULL,
            secreto              BLOB NULL,
            secreto_nonce        BLOB NULL,
            notes                TEXT NULL,
            created_at           TEXT NOT NULL,
            updated_at           TEXT NOT NULL,
            sort_order           INTEGER NOT NULL DEFAULT 0,
            icon_color           TEXT NULL,
            parent_connection_id TEXT NULL REFERENCES connections(id) ON DELETE CASCADE,
            description          TEXT NULL,
            is_favorite          INTEGER NOT NULL DEFAULT 0 CHECK (is_favorite IN (0, 1)),
            es_rapida            INTEGER NOT NULL DEFAULT 0 CHECK (es_rapida IN (0, 1)),
            custom_fields        TEXT NULL
                                 CHECK (custom_fields IS NULL OR json_valid(custom_fields)),
            tag_id               TEXT NULL REFERENCES tags(id) ON DELETE SET NULL,
            icon_key             TEXT NULL,

            CHECK (secreto IS NOT NULL OR secreto_nonce IS NULL)
        );

        INSERT INTO connections_nueva (
            id, folder_id, name, protocol, host, port, username,
            secreto, secreto_nonce, notes, created_at, updated_at,
            sort_order, icon_color, parent_connection_id, description,
            is_favorite, es_rapida, custom_fields, tag_id, icon_key)
        SELECT c.id,
               CASE WHEN c.parent_connection_id IS NULL THEN c.folder_id
                    ELSE (SELECT p.folder_id FROM connections p
                           WHERE p.id = c.parent_connection_id) END,
               c.name, c.protocol, c.host, c.port, c.username,
               c.secreto, c.secreto_nonce,
               -- 114 de 114 notas de la base eran esta plantilla del importador viejo; el de hoy
               -- ya no la escribe.
               CASE WHEN c.notes LIKE 'Importado de Rdm como %.' THEN NULL ELSE c.notes END,
               c.created_at, c.updated_at,
               c.sort_order, c.icon_color, c.parent_connection_id, c.description,
               c.is_favorite,
               CASE WHEN json_valid(c.custom_fields)
                         AND json_extract(c.custom_fields, '$."cmc:conexionRapida"') IS NOT NULL
                    THEN 1 ELSE 0 END,
               CASE WHEN c.custom_fields IS NULL OR NOT json_valid(c.custom_fields) THEN NULL
                    ELSE NULLIF(json_remove(c.custom_fields, '$."cmc:conexionRapida"'), '{}') END,
               c.tag_id, c.icon_key
          FROM connections c;

        DROP TABLE connections;
        ALTER TABLE connections_nueva RENAME TO connections;
        CREATE INDEX ix_connections_folder ON connections(folder_id, sort_order);
        CREATE INDEX ix_connections_parent ON connections(parent_connection_id, sort_order);
        CREATE INDEX ix_connections_tag    ON connections(tag_id);

        CREATE TABLE rdp_settings_nueva (
            connection_id               TEXT PRIMARY KEY NOT NULL
                                        REFERENCES connections(id) ON DELETE CASCADE,
            domain                      TEXT NULL,
            clipboard_enabled           INTEGER NULL,
            ignore_certificate_warnings INTEGER NULL,
            abre_en_ventana_propia      INTEGER NOT NULL DEFAULT 0
                                        CHECK (abre_en_ventana_propia IN (0, 1))
        );

        INSERT INTO rdp_settings_nueva (
            connection_id, domain, clipboard_enabled, ignore_certificate_warnings,
            abre_en_ventana_propia)
        SELECT connection_id, domain, clipboard_enabled, ignore_certificate_warnings,
               start_full_screen
          FROM rdp_settings;

        DROP TABLE rdp_settings;
        ALTER TABLE rdp_settings_nueva RENAME TO rdp_settings;

        CREATE TABLE web_settings_nueva (
            connection_id  TEXT PRIMARY KEY NOT NULL
                           REFERENCES connections(id) ON DELETE CASCADE,
            url            TEXT NOT NULL CHECK (TRIM(url) <> ''),
            browser        TEXT NULL,
            private_window INTEGER NOT NULL DEFAULT 0 CHECK (private_window IN (0, 1))
        );

        INSERT INTO web_settings_nueva (connection_id, url, browser, private_window)
        SELECT connection_id, url, browser, private_window FROM web_settings;

        DROP TABLE web_settings;
        ALTER TABLE web_settings_nueva RENAME TO web_settings;

        CREATE TABLE ssh_tunnels_nueva (
            id            TEXT PRIMARY KEY NOT NULL,
            connection_id TEXT NOT NULL REFERENCES connections(id) ON DELETE CASCADE,
            name          TEXT NOT NULL,
            local_port    INTEGER NOT NULL CHECK (local_port BETWEEN 1 AND 65535),
            remote_host   TEXT NOT NULL,
            remote_port   INTEGER NOT NULL CHECK (remote_port BETWEEN 1 AND 65535),
            auto_start    INTEGER NOT NULL DEFAULT 0 CHECK (auto_start IN (0, 1))
        );

        INSERT INTO ssh_tunnels_nueva (
            id, connection_id, name, local_port, remote_host, remote_port, auto_start)
        SELECT id, connection_id, name, local_port, remote_host, remote_port, auto_start
          FROM ssh_tunnels;

        DROP TABLE ssh_tunnels;
        ALTER TABLE ssh_tunnels_nueva RENAME TO ssh_tunnels;
        CREATE INDEX ix_tunnels_connection ON ssh_tunnels(connection_id);

        -- Una sesion que conecto y despues se cayo se anotaba Success con el motivo de la caida.
        CREATE TABLE connection_history_nueva (
            id               TEXT PRIMARY KEY NOT NULL,
            connection_id    TEXT NOT NULL REFERENCES connections(id) ON DELETE CASCADE,
            attempted_at     TEXT NOT NULL,
            outcome          TEXT NOT NULL CHECK (outcome IN ('Success', 'Failed', 'Cancelled')),
            failure_reason   TEXT NULL,
            duration_seconds INTEGER NULL,

            CHECK (outcome <> 'Success' OR failure_reason IS NULL),
            CHECK (outcome <> 'Failed' OR failure_reason IS NOT NULL)
        );

        INSERT INTO connection_history_nueva (
            id, connection_id, attempted_at, outcome, failure_reason, duration_seconds)
        SELECT id, connection_id, attempted_at, outcome,
               CASE WHEN outcome = 'Success' THEN NULL
                    WHEN outcome = 'Failed' THEN COALESCE(failure_reason, 'Other')
                    ELSE failure_reason END,
               duration_seconds
          FROM connection_history;

        DROP TABLE connection_history;
        ALTER TABLE connection_history_nueva RENAME TO connection_history;
        CREATE INDEX ix_history_connection ON connection_history(connection_id, attempted_at DESC);
        """;
}
