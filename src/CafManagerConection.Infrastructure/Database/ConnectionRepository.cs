using System.Globalization;
using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class ConnectionRepository : IConnectionRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IAppLogger? _logger;

    public ConnectionRepository(ISqliteConnectionFactory factory, IAppLogger? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var rows = db.Query<ConnectionRow>(
            ColumnasDeConexion + " FROM connections ORDER BY sort_order, name;").ToList();

        return Task.FromResult<IReadOnlyList<Connection>>(
            rows.Select(r => ADominio(r, _logger)).ToList());
    }

    public Task<ConnectionRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var key = id.ToString("D");

        var row = db.QuerySingleOrDefault<ConnectionRow>(
            ColumnasDeConexion + " FROM connections WHERE id = @Id;", new { Id = key });

        if (row is null)
        {
            return Task.FromResult<ConnectionRecord?>(null);
        }

        var connection = ADominio(row, _logger);

        var record = connection.Protocol switch
        {
            Protocol.Rdp => new ConnectionRecord(connection, Rdp: ReadRdp(db, key)),
            Protocol.Ssh => new ConnectionRecord(connection, Ssh: ReadSsh(db, key, _logger)),
            Protocol.Web => new ConnectionRecord(connection, Web: ReadWeb(db, key)),
            _ => new ConnectionRecord(connection),
        };

        return Task.FromResult<ConnectionRecord?>(record);
    }

    /// <summary>Escribe la conexión y su configuración en una sola transacción: una conexión sin su fila de configuración es un estado inválido.</summary>
    public Task AddAsync(ConnectionRecord record, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        using var tx = db.BeginTransaction();
        var c = record.Connection;

        db.Execute("""
            INSERT INTO connections (
                id, folder_id, name, protocol, host, port, username, notes,
                created_at, updated_at, sort_order,
                icon_color, icon_key, parent_connection_id, description,
                is_favorite, es_rapida, custom_fields, tag_id)
            VALUES (
                @Id, @FolderId, @Name, @Protocol, @Host, @Port, @UserName, @Notes,
                @CreatedAt, @UpdatedAt, @SortOrder,
                @ClaveDeColor, @ClaveDeIcono, @ParentConnectionId, @Description,
                @IsFavorite, @EsRapida, @CustomFields, @TagId);
            """, ToParams(c), tx);

        WriteSettings(db, tx, record);
        tx.Commit();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ConnectionRecord record, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        using var tx = db.BeginTransaction();
        var c = record.Connection;

        db.Execute("""
            UPDATE connections SET
                folder_id = @FolderId, name = @Name, host = @Host, port = @Port,
                username = @UserName, notes = @Notes,
                updated_at = @UpdatedAt, sort_order = @SortOrder,
                icon_color = @ClaveDeColor, icon_key = @ClaveDeIcono,
                parent_connection_id = @ParentConnectionId,
                description = @Description,
                is_favorite = @IsFavorite, es_rapida = @EsRapida,
                custom_fields = @CustomFields, tag_id = @TagId
            WHERE id = @Id;
            """, ToParams(c), tx);

        WriteSettings(db, tx, record);
        tx.Commit();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute("DELETE FROM connections WHERE id = @Id;", new { Id = id.ToString("D") });
        return Task.CompletedTask;
    }

    /// <summary>Guarda la huella del host aceptada en la sesión, sin tocar el resto de la conexión.</summary>
    public Task SetKnownHostFingerprintAsync(
        Guid id, string? fingerprint, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute(
            "UPDATE ssh_settings SET known_host_fingerprint = @Fingerprint "
            + "WHERE connection_id = @Id;",
            new { Id = id.ToString("D"), Fingerprint = fingerprint });
        return Task.CompletedTask;
    }

    /// <summary>Guarda que la sesión abre en ventana propia, sin tocar el resto de la conexión.</summary>
    public Task SetAbreEnVentanaPropiaAsync(
        Guid id, bool abre, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute(
            "UPDATE rdp_settings SET abre_en_ventana_propia = @Abre WHERE connection_id = @Id;",
            new { Id = id.ToString("D"), Abre = abre ? 1 : 0 });
        return Task.CompletedTask;
    }

    public Task ReorderAsync(
        Guid? folderId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        using var tx = db.BeginTransaction();

        for (var i = 0; i < orderedIds.Count; i++)
        {
            db.Execute(
                "UPDATE connections SET sort_order = @Order WHERE id = @Id;",
                new { Order = i, Id = orderedIds[i].ToString("D") }, tx);
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    private static object ToParams(Connection c) => new
    {
        Id = c.Id.ToString("D"),
        FolderId = c.FolderId?.ToString("D"),
        c.Name,
        Protocol = c.Protocol.ToString(),
        c.Host,
        c.Port,
        c.UserName,
        c.Notes,
        CreatedAt = FolderRepository.Iso(c.CreatedAt),
        UpdatedAt = FolderRepository.Iso(c.UpdatedAt),
        c.SortOrder,
        c.ClaveDeColor,
        c.ClaveDeIcono,
        ParentConnectionId = c.ParentConnectionId?.ToString("D"),
        c.Description,
        IsFavorite = c.IsFavorite ? 1 : 0,
        EsRapida = c.EsRapida ? 1 : 0,
        CustomFields = Serializacion.CamposATexto(c.CustomFields),
        TagId = c.TagId?.ToString(),
    };

    // Upsert y no borrar mas insertar: un registro que llega sin sus ajustes de protocolo perdia
    // en silencio la huella del host y la url (FolderRepository.WriteSettings paga el mismo defecto).
    private static void WriteSettings(
        System.Data.IDbConnection db, System.Data.IDbTransaction tx, ConnectionRecord record)
    {
        var key = record.Connection.Id.ToString("D");

        if (record.Rdp is { } rdp)
        {
            db.Execute("""
                INSERT INTO rdp_settings (
                    connection_id, domain, clipboard_enabled,
                    ignore_certificate_warnings, abre_en_ventana_propia)
                VALUES (@Id, @Domain, @Clipboard, @Ignore, @Propia)
                ON CONFLICT(connection_id) DO UPDATE SET
                    domain = @Domain,
                    clipboard_enabled = @Clipboard,
                    ignore_certificate_warnings = @Ignore,
                    abre_en_ventana_propia = @Propia;
                """,
                new
                {
                    Id = key,
                    rdp.Domain,
                    Clipboard = FolderRepository.ToDb(rdp.ClipboardEnabled),
                    Ignore = FolderRepository.ToDb(rdp.IgnoreCertificateWarnings),
                    Propia = rdp.AbreEnVentanaPropia ? 1 : 0,
                }, tx);
        }

        if (record.Ssh is { } ssh)
        {
            db.Execute("""
                INSERT INTO ssh_settings (
                    connection_id, auth_method, private_key_path, ssh_certificate_path,
                    known_host_fingerprint, keep_alive_seconds)
                VALUES (@Id, @Auth, @KeyPath, @CertificatePath, @Fingerprint, @KeepAlive)
                ON CONFLICT(connection_id) DO UPDATE SET
                    auth_method = @Auth,
                    private_key_path = @KeyPath,
                    ssh_certificate_path = @CertificatePath,
                    known_host_fingerprint = @Fingerprint,
                    keep_alive_seconds = @KeepAlive;
                """,
                new
                {
                    Id = key,
                    Auth = ssh.AuthMethod?.ToString(),
                    KeyPath = ssh.PrivateKeyPath,
                    CertificatePath = ssh.CertificatePath,
                    Fingerprint = ssh.KnownHostFingerprint,
                    KeepAlive = ssh.KeepAliveSeconds,
                }, tx);
        }

        if (record.Web is { } web)
        {
            db.Execute("""
                INSERT INTO web_settings (connection_id, url, browser, private_window)
                VALUES (@Id, @Url, @Browser, @Private)
                ON CONFLICT(connection_id) DO UPDATE SET
                    url = @Url, browser = @Browser, private_window = @Private;
                """,
                new
                {
                    Id = key,
                    web.Url,
                    web.Browser,
                    Private = web.PrivateWindow ? 1 : 0,
                }, tx);
        }
    }

    private static RdpSettings? ReadRdp(System.Data.IDbConnection db, string id)
    {
        var r = db.QuerySingleOrDefault<RdpRow>("""
            SELECT domain, clipboard_enabled, ignore_certificate_warnings, abre_en_ventana_propia
              FROM rdp_settings WHERE connection_id = @Id;
            """, new { Id = id });

        return r is null ? null : new RdpSettings
        {
            ConnectionId = Guid.Parse(id),
            Domain = r.Domain,
            ClipboardEnabled = FromDb(r.ClipboardEnabled),
            IgnoreCertificateWarnings = FromDb(r.IgnoreCertificateWarnings),
            AbreEnVentanaPropia = r.AbreEnVentanaPropia != 0,
        };
    }

    private static SshSettings? ReadSsh(
        System.Data.IDbConnection db, string id, IAppLogger? logger)
    {
        var r = db.QuerySingleOrDefault<SshRow>("""
            SELECT auth_method, private_key_path, ssh_certificate_path,
                   known_host_fingerprint, keep_alive_seconds
              FROM ssh_settings WHERE connection_id = @Id;
            """, new { Id = id });

        return r is null ? null : new SshSettings
        {
            ConnectionId = Guid.Parse(id),
            AuthMethod = LeerEnum<SshAuthMethod>(r.AuthMethod, id, "auth_method", logger),
            PrivateKeyPath = r.PrivateKeyPath,
            CertificatePath = r.SshCertificatePath,
            KnownHostFingerprint = r.KnownHostFingerprint,
            KeepAliveSeconds = r.KeepAliveSeconds,
        };
    }

    private static WebSettings? ReadWeb(System.Data.IDbConnection db, string id)
    {
        var r = db.QuerySingleOrDefault<WebRow>(
            "SELECT url, browser, private_window FROM web_settings WHERE connection_id = @Id;",
            new { Id = id });

        return r is null ? null : new WebSettings
        {
            ConnectionId = Guid.Parse(id),
            Url = r.Url,
            Browser = r.Browser,
            PrivateWindow = r.PrivateWindow != 0,
        };
    }

    private static bool? FromDb(long? value) => value is null ? null : value != 0;

    /// <summary>Lee un enum guardado como texto; un valor que el enum ya no tiene se registra y vuelve nulo, en vez de tirar la carga entera del árbol.</summary>
    private static T? LeerEnum<T>(string? texto, string id, string columna, IAppLogger? logger)
        where T : struct, Enum
    {
        if (string.IsNullOrEmpty(texto))
        {
            return null;
        }

        if (Enum.TryParse<T>(texto, out var valor) && Enum.IsDefined(valor))
        {
            return valor;
        }

        logger?.TechnicalError(
            $"leer {columna} de la conexión {id}: «{texto}» no es un valor conocido",
            new InvalidOperationException(texto));

        return null;
    }

    private static Connection ADominio(ConnectionRow r, IAppLogger? logger)
    {
        var protocolo = LeerEnum<Protocol>(r.Protocol, r.Id, "protocol", logger) ?? Protocol.Ssh;

        var c = new Connection(Guid.Parse(r.Id), r.Name, protocolo, r.Host)
        {
            FolderId = r.FolderId is null ? null : Guid.Parse(r.FolderId),
            UserName = r.Username,
            TieneSecreto = r.TieneSecreto == 1,
            Notes = r.Notes,
            SortOrder = r.SortOrder,
            CreatedAt = DateTimeOffset.Parse(r.CreatedAt, CultureInfo.InvariantCulture),
            ParentConnectionId = r.ParentConnectionId is null
                ? null
                : Guid.Parse(r.ParentConnectionId),
            ClaveDeColor = r.IconColor,
            ClaveDeIcono = r.IconKey,
            Description = r.Description,
            IsFavorite = r.IsFavorite != 0,
            EsRapida = r.EsRapida != 0,
            TagId = Guid.TryParse(r.TagId, out var etiqueta) ? etiqueta : null,
        };

        c.SetPort(r.Port);

        foreach (var (nombre, valor) in Serializacion.TextoACampos(r.CustomFields, r.Id, logger))
        {
            c.SetCustomField(nombre, valor);
        }

        c.RestituirModificacion(
            DateTimeOffset.Parse(r.UpdatedAt, CultureInfo.InvariantCulture));

        return c;
    }

    // El secreto y su nonce quedan afuera a proposito: los lee y los escribe el vault, y de
    // aca solo interesa si hay uno para que la herencia sepa si tiene que seguir subiendo.
    private const string ColumnasDeConexion = """
        SELECT id, folder_id, name, protocol, host, port, username, notes,
               created_at, updated_at, sort_order, icon_color, icon_key,
               parent_connection_id, description, is_favorite, es_rapida, custom_fields,
               tag_id, secreto IS NOT NULL AS tiene_secreto
        """;

    private sealed class ConnectionRow
    {
        public string Id { get; init; } = string.Empty;
        public string? FolderId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Protocol { get; init; } = string.Empty;
        public string Host { get; init; } = string.Empty;
        public int? Port { get; init; }
        public string? Username { get; init; }
        public long TieneSecreto { get; init; }
        public string? Notes { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public string? IconColor { get; init; }
        public string? IconKey { get; init; }
        public string? ParentConnectionId { get; init; }
        public string? Description { get; init; }
        public long IsFavorite { get; init; }
        public long EsRapida { get; init; }
        public string? CustomFields { get; init; }
        public string? TagId { get; init; }
    }

    private sealed class RdpRow
    {
        public string? Domain { get; init; }
        public long? ClipboardEnabled { get; init; }
        public long? IgnoreCertificateWarnings { get; init; }
        public long AbreEnVentanaPropia { get; init; }
    }

    private sealed class SshRow
    {
        public string? AuthMethod { get; init; }
        public string? PrivateKeyPath { get; init; }
        public string? SshCertificatePath { get; init; }
        public string? KnownHostFingerprint { get; init; }
        public int? KeepAliveSeconds { get; init; }
    }

    private sealed class WebRow
    {
        public string Url { get; init; } = string.Empty;
        public string? Browser { get; init; }
        public long PrivateWindow { get; init; }
    }
}
