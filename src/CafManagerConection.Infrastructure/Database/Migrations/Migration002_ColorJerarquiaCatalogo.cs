namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Color de icono, conexiones hijas y metadatos de catálogo, sólo con columnas anulables e índices. Corresponde a <c>user_version = 2</c>.</summary>
public static class Migration002_ColorJerarquiaCatalogo
{
    public const int Version = 2;

    public const string Sql = """
        -- Color del icono, resuelto en cascada: propio -> carpeta -> global del protocolo.
        ALTER TABLE connection_folders ADD COLUMN icon_color TEXT NULL;
        ALTER TABLE connections        ADD COLUMN icon_color TEXT NULL;

        -- Conexiones hijas: los servicios que corren en un servidor.
        ALTER TABLE connections ADD COLUMN parent_connection_id TEXT NULL
                                REFERENCES connections(id) ON DELETE CASCADE;

        CREATE INDEX ix_connections_parent
            ON connections(parent_connection_id, sort_order);

        -- Metadatos de catálogo.
        ALTER TABLE connections ADD COLUMN description       TEXT NULL;
        ALTER TABLE connections ADD COLUMN tags              TEXT NULL;
        ALTER TABLE connections ADD COLUMN documentation_url TEXT NULL;
        ALTER TABLE connections ADD COLUMN is_favorite       INTEGER NOT NULL DEFAULT 0;
        ALTER TABLE connections ADD COLUMN custom_fields     TEXT NULL;

        ALTER TABLE connection_folders ADD COLUMN description TEXT NULL;
        ALTER TABLE connection_folders ADD COLUMN tags        TEXT NULL;

        -- El entorno se hereda: NULL en la conexión significa "el de mi carpeta".
        ALTER TABLE folder_settings ADD COLUMN environment TEXT NULL;
        ALTER TABLE connections     ADD COLUMN environment TEXT NULL;

        -- Índice parcial: sólo entran las favoritas, que son unas pocas de cientos.
        CREATE INDEX ix_connections_favorite
            ON connections(is_favorite) WHERE is_favorite = 1;
        """;
}
