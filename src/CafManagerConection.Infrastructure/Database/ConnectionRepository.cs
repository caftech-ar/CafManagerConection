using System.Globalization;
using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class ConnectionRepository : IConnectionRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public ConnectionRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var rows = db.Query<ConnectionRow>(
            "SELECT * FROM connections ORDER BY sort_order, name;").ToList();

        return Task.FromResult<IReadOnlyList<Connection>>(rows.Select(r => r.ToDomain()).ToList());
    }

    public Task<ConnectionRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var key = id.ToString("D");

        var row = db.QuerySingleOrDefault<ConnectionRow>(
            "SELECT * FROM connections WHERE id = @Id;", new { Id = key });

        if (row is null)
        {
            return Task.FromResult<ConnectionRecord?>(null);
        }

        var connection = row.ToDomain();

        var record = connection.Protocol switch
        {
            Protocol.Rdp => new ConnectionRecord(connection, Rdp: ReadRdp(db, key)),
            Protocol.Ssh => new ConnectionRecord(connection, Ssh: ReadSsh(db, key)),
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
                id, folder_id, name, protocol, host, port, username, credential_key, notes,
                created_at, updated_at, last_connected_at, sort_order,
                icon_color, icon_key, parent_connection_id, description, documentation_url,
                is_favorite, custom_fields, tag_id)
            VALUES (
                @Id, @FolderId, @Name, @Protocol, @Host, @Port, @UserName, @CredentialKey, @Notes,
                @CreatedAt, @UpdatedAt, @LastConnectedAt, @SortOrder,
                @ClaveDeColor, @ClaveDeIcono, @ParentConnectionId, @Description, @DocumentationUrl,
                @IsFavorite, @CustomFields, @TagId);
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
                username = @UserName, credential_key = @CredentialKey, notes = @Notes,
                updated_at = @UpdatedAt, last_connected_at = @LastConnectedAt,
                sort_order = @SortOrder,
                icon_color = @ClaveDeColor, icon_key = @ClaveDeIcono,
                parent_connection_id = @ParentConnectionId,
                description = @Description,
                documentation_url = @DocumentationUrl, is_favorite = @IsFavorite,
                custom_fields = @CustomFields, tag_id = @TagId
            WHERE id = @Id;
            """, ToParams(c), tx);

        var key = c.Id.ToString("D");
        db.Execute("DELETE FROM rdp_settings WHERE connection_id = @Id;", new { Id = key }, tx);
        db.Execute("DELETE FROM ssh_settings WHERE connection_id = @Id;", new { Id = key }, tx);
        db.Execute("DELETE FROM web_settings WHERE connection_id = @Id;", new { Id = key }, tx);

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

    public Task SetLastConnectedAsync(Guid id, DateTimeOffset when, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        db.Execute(
            "UPDATE connections SET last_connected_at = @When WHERE id = @Id;",
            new { Id = id.ToString("D"), When = FolderRepository.Iso(when) });
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
        c.CredentialKey,
        c.Notes,
        CreatedAt = FolderRepository.Iso(c.CreatedAt),
        UpdatedAt = FolderRepository.Iso(c.UpdatedAt),
        LastConnectedAt = c.LastConnectedAt is { } l ? FolderRepository.Iso(l) : null,
        c.SortOrder,
        c.ClaveDeColor,
        c.ClaveDeIcono,
        ParentConnectionId = c.ParentConnectionId?.ToString("D"),
        c.Description,
        c.DocumentationUrl,
        IsFavorite = c.IsFavorite ? 1 : 0,
        CustomFields = Serializacion.CamposATexto(c.CustomFields),
        TagId = c.TagId?.ToString(),
    };

    private static void WriteSettings(
        System.Data.IDbConnection db, System.Data.IDbTransaction tx, ConnectionRecord record)
    {
        var key = record.Connection.Id.ToString("D");

        if (record.Rdp is { } rdp)
        {
            db.Execute("""
                INSERT INTO rdp_settings (
                    connection_id, domain, clipboard_enabled, fit_to_tab,
                    ignore_certificate_warnings, start_full_screen)
                VALUES (@Id, @Domain, @Clipboard, @Fit, @Ignore, @Full);
                """,
                new
                {
                    Id = key,
                    rdp.Domain,
                    Clipboard = FolderRepository.ToDb(rdp.ClipboardEnabled),
                    Fit = FolderRepository.ToDb(rdp.FitToTab),
                    Ignore = FolderRepository.ToDb(rdp.IgnoreCertificateWarnings),
                    Full = rdp.StartFullScreen ? 1 : 0,
                }, tx);
        }

        if (record.Ssh is { } ssh)
        {
            db.Execute("""
                INSERT INTO ssh_settings (
                    connection_id, auth_method, private_key_path, ssh_certificate_path,
                    known_host_fingerprint, keep_alive_seconds, encoding)
                VALUES (@Id, @Auth, @KeyPath, @CertificatePath, @Fingerprint, @KeepAlive, @Encoding);
                """,
                new
                {
                    Id = key,
                    Auth = ssh.AuthMethod?.ToString(),
                    KeyPath = ssh.PrivateKeyPath,
                    CertificatePath = ssh.CertificatePath,
                    Fingerprint = ssh.KnownHostFingerprint,
                    KeepAlive = ssh.KeepAliveSeconds,
                    ssh.Encoding,
                }, tx);
        }

        if (record.Web is { } web)
        {
            db.Execute("""
                INSERT INTO web_settings (connection_id, url, browser, private_window)
                VALUES (@Id, @Url, @Browser, @Private);
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
        var r = db.QuerySingleOrDefault<RdpRow>(
            "SELECT * FROM rdp_settings WHERE connection_id = @Id;", new { Id = id });

        return r is null ? null : new RdpSettings
        {
            ConnectionId = Guid.Parse(id),
            Domain = r.Domain,
            ClipboardEnabled = FromDb(r.Clipboard_Enabled),
            FitToTab = FromDb(r.Fit_To_Tab),
            IgnoreCertificateWarnings = FromDb(r.Ignore_Certificate_Warnings),
            StartFullScreen = r.Start_Full_Screen != 0,
        };
    }

    private static SshSettings? ReadSsh(System.Data.IDbConnection db, string id)
    {
        var r = db.QuerySingleOrDefault<SshRow>(
            "SELECT * FROM ssh_settings WHERE connection_id = @Id;", new { Id = id });

        return r is null ? null : new SshSettings
        {
            ConnectionId = Guid.Parse(id),
            AuthMethod = r.Auth_Method is null ? null : Enum.Parse<SshAuthMethod>(r.Auth_Method),
            PrivateKeyPath = r.Private_Key_Path,
            CertificatePath = r.Ssh_Certificate_Path,
            KnownHostFingerprint = r.Known_Host_Fingerprint,
            KeepAliveSeconds = r.Keep_Alive_Seconds,
            Encoding = r.Encoding,
        };
    }

    private static WebSettings? ReadWeb(System.Data.IDbConnection db, string id)
    {
        var r = db.QuerySingleOrDefault<WebRow>(
            "SELECT * FROM web_settings WHERE connection_id = @Id;", new { Id = id });

        return r is null ? null : new WebSettings
        {
            ConnectionId = Guid.Parse(id),
            Url = r.Url,
            Browser = r.Browser,
            PrivateWindow = r.Private_Window != 0,
        };
    }

    private static bool? FromDb(long? value) => value is null ? null : value != 0;

    private sealed class ConnectionRow
    {
        public string Id { get; init; } = string.Empty;
        public string? Folder_Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Protocol { get; init; } = string.Empty;
        public string Host { get; init; } = string.Empty;
        public int? Port { get; init; }
        public string? Username { get; init; }
        public string? Credential_Key { get; init; }
        public string? Notes { get; init; }
        public string Created_At { get; init; } = string.Empty;
        public string Updated_At { get; init; } = string.Empty;
        public string? Last_Connected_At { get; init; }
        public int Sort_Order { get; init; }
        public string? Icon_Color { get; init; }
        public string? Icon_Key { get; init; }
        public string? Parent_Connection_Id { get; init; }
        public string? Description { get; init; }
        public string? Documentation_Url { get; init; }
        public long Is_Favorite { get; init; }
        public string? Custom_Fields { get; init; }
        // Se llama Tag_Id y no TagId: Dapper mapea por nombre exacto de columna y con TagId la etiqueta volvía siempre nula, sin error.
        public string? Tag_Id { get; init; }

        public Connection ToDomain()
        {
            var c = new Connection(
                Guid.Parse(Id),
                Name,
                Enum.Parse<Protocol>(Protocol),
                Host)
            {
                FolderId = Folder_Id is null ? null : Guid.Parse(Folder_Id),
                UserName = Username,
                CredentialKey = Credential_Key,
                Notes = Notes,
                SortOrder = Sort_Order,
                CreatedAt = DateTimeOffset.Parse(Created_At, CultureInfo.InvariantCulture),
                LastConnectedAt = Last_Connected_At is null
                    ? null
                    : DateTimeOffset.Parse(Last_Connected_At, CultureInfo.InvariantCulture),
                ParentConnectionId = Parent_Connection_Id is null
                    ? null
                    : Guid.Parse(Parent_Connection_Id),
                ClaveDeColor = Icon_Color,
                ClaveDeIcono = Icon_Key,
                Description = Description,
                DocumentationUrl = Documentation_Url,
                IsFavorite = Is_Favorite != 0,
                TagId = Guid.TryParse(Tag_Id, out var etiqueta) ? etiqueta : null,
            };

            c.SetPort(Port);

            foreach (var (nombre, valor) in Serializacion.TextoACampos(Custom_Fields))
            {
                c.SetCustomField(nombre, valor);
            }

            return c;
        }
    }

    private sealed class RdpRow
    {
        public string? Domain { get; init; }
        public long? Clipboard_Enabled { get; init; }
        public long? Fit_To_Tab { get; init; }
        public long? Ignore_Certificate_Warnings { get; init; }
        public long Start_Full_Screen { get; init; }
    }

    private sealed class SshRow
    {
        public string? Auth_Method { get; init; }
        public string? Private_Key_Path { get; init; }
        public string? Ssh_Certificate_Path { get; init; }
        public string? Known_Host_Fingerprint { get; init; }
        public int? Keep_Alive_Seconds { get; init; }
        public string Encoding { get; init; } = "UTF-8";
    }

    private sealed class WebRow
    {
        public string Url { get; init; } = string.Empty;
        public string? Browser { get; init; }
        public long Private_Window { get; init; }
    }
}
