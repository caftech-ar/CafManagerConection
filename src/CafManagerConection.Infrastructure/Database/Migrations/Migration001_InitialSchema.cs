namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Esquema inicial. Corresponde a <c>user_version = 1</c>.</summary>
public static class Migration001_InitialSchema
{
    public const int Version = 1;

    public const string Sql = """
        CREATE TABLE connection_folders (
            id          TEXT PRIMARY KEY NOT NULL,
            parent_id   TEXT NULL REFERENCES connection_folders(id) ON DELETE CASCADE,
            name        TEXT NOT NULL,
            sort_order  INTEGER NOT NULL DEFAULT 0,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL
        );
        CREATE INDEX ix_folders_parent ON connection_folders(parent_id, sort_order);

        CREATE TABLE folder_settings (
            folder_id                        TEXT PRIMARY KEY NOT NULL
                                             REFERENCES connection_folders(id) ON DELETE CASCADE,
            username                         TEXT NULL,
            domain                           TEXT NULL,
            port                             INTEGER NULL
                                             CHECK (port IS NULL OR port BETWEEN 1 AND 65535),
            rdp_credential_key               TEXT NULL,
            ssh_credential_key               TEXT NULL,
            web_credential_key               TEXT NULL,
            rdp_clipboard_enabled            INTEGER NULL,
            rdp_fit_to_tab                   INTEGER NULL,
            rdp_ignore_certificate_warnings  INTEGER NULL,
            ssh_auth_method                  TEXT NULL
                                             CHECK (ssh_auth_method IS NULL
                                                    OR ssh_auth_method IN ('Password', 'PrivateKey')),
            ssh_private_key_path             TEXT NULL,
            ssh_keep_alive_seconds           INTEGER NULL
                                             CHECK (ssh_keep_alive_seconds IS NULL
                                                    OR ssh_keep_alive_seconds BETWEEN 0 AND 3600)
        );

        CREATE TABLE connections (
            id                TEXT PRIMARY KEY NOT NULL,
            folder_id         TEXT NULL REFERENCES connection_folders(id) ON DELETE CASCADE,
            name              TEXT NOT NULL,
            protocol          TEXT NOT NULL CHECK (protocol IN ('Rdp', 'Ssh', 'Web')),
            host              TEXT NOT NULL,
            port              INTEGER NULL CHECK (port IS NULL OR port BETWEEN 1 AND 65535),
            username          TEXT NULL,
            credential_key    TEXT NULL,
            notes             TEXT NULL,
            created_at        TEXT NOT NULL,
            updated_at        TEXT NOT NULL,
            last_connected_at TEXT NULL,
            sort_order        INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX ix_connections_folder ON connections(folder_id, sort_order);
        CREATE INDEX ix_connections_search ON connections(name, host, username);

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
        """;
}
