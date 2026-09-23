namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Traduce las claves de icono del juego anterior a las del catálogo. Corresponde a <c>user_version = 5</c>.</summary>
public static class Migration005_ClavesDeIconoDelCatalogo
{
    public const int Version = 5;

    // Las 16 claves que el juego anterior sabía escribir. Sin esto la elección se pierde en
    // silencio: NodoArbol no reconoce la clave y cae en el icono del protocolo.
    public const string Sql = """
        CREATE TEMP TABLE claves_de_icono (vieja TEXT PRIMARY KEY NOT NULL, nueva TEXT NOT NULL);

        INSERT INTO claves_de_icono (vieja, nueva) VALUES
            ('carpeta',       'folder'),
            ('escritorio',    'device-desktop-share'),
            ('terminal',      'terminal-2'),
            ('web',           'world-www'),
            ('base-de-datos', 'database'),
            ('correo',        'mail'),
            ('archivos',      'folder-open'),
            ('respaldo',      'archive'),
            ('contenedor',    'brand-docker'),
            ('cortafuegos',   'wall'),
            ('monitoreo',     'activity'),
            ('proxy',         'nginx'),
            ('servicios',     'settings-automation'),
            ('red',           'route'),
            ('puertos',       'plug'),
            ('aplicacion',    'box');

        UPDATE connections
           SET icon_key = (SELECT nueva FROM claves_de_icono WHERE vieja = connections.icon_key)
         WHERE icon_key IN (SELECT vieja FROM claves_de_icono);

        UPDATE connection_folders
           SET icon_key = (SELECT nueva FROM claves_de_icono
                            WHERE vieja = connection_folders.icon_key)
         WHERE icon_key IN (SELECT vieja FROM claves_de_icono);

        DROP TABLE claves_de_icono;
        """;
}
