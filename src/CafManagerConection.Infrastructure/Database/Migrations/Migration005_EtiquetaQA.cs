namespace CafManagerConection.Infrastructure.Database.Migrations;

/// <summary>Agrega la etiqueta QA y lleva los códigos de las dos últimas a CAPA y DESA. Corresponde a <c>user_version = 5</c>.</summary>
public static class Migration005_EtiquetaQA
{
    public const int Version = 5;

    public const string Sql = """
        -- El orden va descendente para no pasar por un estado con dos filas en el mismo lugar.
        UPDATE tags SET sort_order = 5, code = 'DESA', updated_at = datetime('now')
         WHERE id = '11111111-0000-4000-8000-000000000004' AND code = 'DEV' AND sort_order = 4;

        UPDATE tags SET sort_order = 4, code = 'CAPA', updated_at = datetime('now')
         WHERE id = '11111111-0000-4000-8000-000000000003' AND code = 'CAP' AND sort_order = 3;

        -- OR IGNORE: el identificador es fijo, asi que reaplicar esto no duplica ni falla contra
        -- el indice unico de code.
        INSERT OR IGNORE INTO tags (id, code, name, color, sort_order, created_at, updated_at)
        VALUES ('11111111-0000-4000-8000-000000000005', 'QA', 'Quality Assurance', 'violeta', 3,
                datetime('now'), datetime('now'));
        """;
}
