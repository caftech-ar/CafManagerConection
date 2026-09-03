namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Las dos tablas del vault. Aditiva: no toca ni borra ninguna columna, y las cuatro <c>*_credential_key</c> siguen siendo la clave lógica que resuelve la herencia.</summary>
internal static class Migration007_Vault
{
    public const int Version = 7;

    public const string Sql = """
        CREATE TABLE vault (
            id                    INTEGER PRIMARY KEY CHECK (id = 1),
            formato               INTEGER NOT NULL,
            clave_dpapi           BLOB    NULL,
            kdf_sal               BLOB    NULL,
            kdf_iteraciones       INTEGER NULL,
            clave_maestra_nonce   BLOB    NULL,
            clave_maestra_envuelta BLOB   NULL,
            creado_en             TEXT    NOT NULL
        );

        CREATE TABLE vault_credenciales (
            clave         TEXT PRIMARY KEY,
            usuario       TEXT NOT NULL,
            dominio       TEXT NULL,
            secreto_nonce BLOB NOT NULL,
            secreto       BLOB NOT NULL,
            guardado_en   TEXT NOT NULL
        );
        """;
}
