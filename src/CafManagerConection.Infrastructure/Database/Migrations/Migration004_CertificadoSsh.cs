namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Certificado firmado por una CA como complemento de la clave privada SSH. Corresponde a <c>user_version = 4</c>.</summary>
public static class Migration004_CertificadoSsh
{
    public const int Version = 4;

    public const string Sql = """
        ALTER TABLE ssh_settings    ADD COLUMN ssh_certificate_path TEXT NULL;
        ALTER TABLE folder_settings ADD COLUMN ssh_certificate_path TEXT NULL;
        """;
}
