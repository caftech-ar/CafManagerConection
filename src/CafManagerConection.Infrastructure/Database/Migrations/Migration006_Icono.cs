namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Clave del icono elegido, en paralelo al color que ya existe desde la 002. Corresponde a <c>user_version = 6</c>.</summary>
public static class Migration006_Icono
{
    public const int Version = 6;

    public const string Sql = """
        ALTER TABLE connection_folders ADD COLUMN icon_key TEXT NULL;
        ALTER TABLE connections        ADD COLUMN icon_key TEXT NULL;
        """;
}
