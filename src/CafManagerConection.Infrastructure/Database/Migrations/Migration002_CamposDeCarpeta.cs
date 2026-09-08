namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Suma <c>custom_fields</c> a <c>folder_settings</c> para que las carpetas hereden ajustes reservados <c>cmc:</c>. Corresponde a <c>user_version = 2</c>.</summary>
public static class Migration002_CamposDeCarpeta
{
    public const int Version = 2;

    // ADD COLUMN sobre una tabla existente: no toca las filas que ya están, que vuelven con la columna en null.
    public const string Sql = """
        ALTER TABLE folder_settings ADD COLUMN custom_fields TEXT NULL;
        """;
}
