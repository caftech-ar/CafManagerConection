namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Suma usuario y puerto por protocolo a <c>folder_settings</c>. El <c>username</c>/<c>port</c> compartidos quedan como reserva. Corresponde a <c>user_version = 3</c>.</summary>
public static class Migration003_UsuarioYPuertoPorProtocolo
{
    public const int Version = 3;

    // Sólo ADD COLUMN: no toca las filas existentes, que siguen usando username/port compartidos por fallback.
    public const string Sql = """
        ALTER TABLE folder_settings ADD COLUMN rdp_username TEXT NULL;
        ALTER TABLE folder_settings ADD COLUMN ssh_username TEXT NULL;
        ALTER TABLE folder_settings ADD COLUMN web_username TEXT NULL;
        ALTER TABLE folder_settings ADD COLUMN rdp_port INTEGER NULL;
        ALTER TABLE folder_settings ADD COLUMN ssh_port INTEGER NULL;
        ALTER TABLE folder_settings ADD COLUMN web_port INTEGER NULL;
        """;
}
