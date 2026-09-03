namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Etiquetas configurables con nombre, código y color. Corresponde a <c>user_version = 3</c>.</summary>
public static class Migration003_EtiquetasConfigurables
{
    public const int Version = 3;

    public const string Sql = """
        -- ---------------------------------------------------------------- catálogo

        CREATE TABLE tags (
            id          TEXT PRIMARY KEY NOT NULL,
            code        TEXT NOT NULL,
            name        TEXT NOT NULL,
            color       TEXT NOT NULL,
            sort_order  INTEGER NOT NULL DEFAULT 0,
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL
        );

        -- El código es la sigla que se muestra en el árbol y tiene que ser único: dos etiquetas
        -- con el mismo código serían indistinguibles justo donde menos espacio hay. NOCASE porque
        -- «prd» y «PRD» son el mismo código escrito de dos formas.
        CREATE UNIQUE INDEX ux_tags_code ON tags(code COLLATE NOCASE);
        CREATE UNIQUE INDEX ux_tags_name ON tags(name COLLATE NOCASE);

        -- ---------------------------------------------------------------- asignación

        -- Una columna y no una tabla de unión: es una etiqueta por elemento. Y se hereda igual
        -- que el entorno que reemplaza — NULL en la conexión significa «la de mi carpeta»—, que
        -- es la razón por la que va en folder_settings y no en connection_folders: ahí es donde
        -- vive todo lo heredable.
        --
        -- ON DELETE SET NULL y no CASCADE: borrar una etiqueta del catálogo no puede borrar los
        -- servidores que la usaban. Quedan sin etiqueta, que es lo que uno espera.
        ALTER TABLE connections     ADD COLUMN tag_id TEXT NULL REFERENCES tags(id) ON DELETE SET NULL;
        ALTER TABLE folder_settings ADD COLUMN tag_id TEXT NULL REFERENCES tags(id) ON DELETE SET NULL;

        CREATE INDEX ix_connections_tag     ON connections(tag_id);
        CREATE INDEX ix_folder_settings_tag ON folder_settings(tag_id);

        -- ---------------------------------------------------------------- iniciales

        -- Los cuatro que pidió el usuario, con identificadores fijos para que sean los mismos en
        -- toda instalación: eso permite que dos bases distintas hablen del mismo «Producción».
        INSERT INTO tags (id, code, name, color, sort_order, created_at, updated_at) VALUES
            ('11111111-0000-4000-8000-000000000001', 'PRD', 'Producción',    'rojo',  1, datetime('now'), datetime('now')),
            ('11111111-0000-4000-8000-000000000002', 'PRE', 'PreProducción', 'ambar', 2, datetime('now'), datetime('now')),
            ('11111111-0000-4000-8000-000000000003', 'CAP', 'Capacitación',  'cyan',  3, datetime('now'), datetime('now')),
            ('11111111-0000-4000-8000-000000000004', 'DEV', 'Desarrollo',    'verde', 4, datetime('now'), datetime('now'));

        -- ---------------------------------------------------------------- lo que se retira

        ALTER TABLE connections        DROP COLUMN environment;
        ALTER TABLE connections        DROP COLUMN tags;
        ALTER TABLE folder_settings    DROP COLUMN environment;
        ALTER TABLE connection_folders DROP COLUMN tags;
        """;
}
