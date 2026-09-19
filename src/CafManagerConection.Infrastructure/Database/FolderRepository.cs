using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using Dapper;

namespace CafManagerConection.Infrastructure.Database;

public sealed class FolderRepository : IFolderRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IAppLogger? _logger;

    public FolderRepository(ISqliteConnectionFactory factory, IAppLogger? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    // La lista de columnas tiene que seguir a la de UpdateAsync y WriteSettings: guardar lee y reescribe entero, y una columna que falte acá vuelve como null en el próximo renombre.
    public Task<IReadOnlyList<Folder>> GetAllAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();

        var rows = db.Query<FolderRow>("""
            SELECT f.id, f.parent_id, f.name, f.sort_order, f.created_at, f.updated_at,
                   f.icon_color, f.icon_key, f.description,
                   s.domain,
                   s.rdp_secreto IS NOT NULL AS rdp_tiene_secreto,
                   s.ssh_secreto IS NOT NULL AS ssh_tiene_secreto,
                   s.web_secreto IS NOT NULL AS web_tiene_secreto,
                   s.rdp_clipboard_enabled, s.rdp_ignore_certificate_warnings,
                   s.ssh_auth_method, s.ssh_private_key_path, s.ssh_certificate_path,
                   s.ssh_keep_alive_seconds,
                   s.tag_id, s.custom_fields,
                   s.rdp_username, s.ssh_username, s.web_username,
                   s.rdp_port, s.ssh_port, s.web_port
            FROM connection_folders f
            LEFT JOIN folder_settings s ON s.folder_id = f.id
            ORDER BY f.sort_order, f.name;
            """).ToList();

        return Task.FromResult<IReadOnlyList<Folder>>(
            rows.Select(r => r.ADominio(_logger)).ToList());
    }

    public async Task<Folder?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct).ConfigureAwait(false);
        return all.FirstOrDefault(f => f.Id == id);
    }

    public Task AddAsync(Folder folder, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        using var tx = db.BeginTransaction();

        db.Execute("""
            INSERT INTO connection_folders (id, parent_id, name, sort_order, created_at, updated_at,
                                            icon_color, icon_key, description)
            VALUES (@Id, @ParentId, @Name, @SortOrder, @CreatedAt, @UpdatedAt,
                    @ClaveDeColor, @ClaveDeIcono, @Description);
            """,
            new
            {
                Id = folder.Id.ToString("D"),
                ParentId = folder.ParentId?.ToString("D"),
                folder.Name,
                folder.SortOrder,
                CreatedAt = Iso(folder.CreatedAt),
                UpdatedAt = Iso(folder.UpdatedAt),
                folder.ClaveDeColor,
                folder.ClaveDeIcono,
                folder.Description,
            }, tx);

        WriteSettings(db, tx, folder);
        tx.Commit();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Folder folder, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        using var tx = db.BeginTransaction();

        db.Execute("""
            UPDATE connection_folders
            SET parent_id = @ParentId, name = @Name, sort_order = @SortOrder, updated_at = @UpdatedAt,
                icon_color = @ClaveDeColor, icon_key = @ClaveDeIcono, description = @Description
            WHERE id = @Id;
            """,
            new
            {
                Id = folder.Id.ToString("D"),
                ParentId = folder.ParentId?.ToString("D"),
                folder.Name,
                folder.SortOrder,
                UpdatedAt = Iso(folder.UpdatedAt),
                folder.ClaveDeColor,
                folder.ClaveDeIcono,
                folder.Description,
            }, tx);

        WriteSettings(db, tx, folder);
        tx.Commit();
        return Task.CompletedTask;
    }

    public Task ReorderAsync(
        Guid? parentId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        using var tx = db.BeginTransaction();

        for (var i = 0; i < orderedIds.Count; i++)
        {
            db.Execute(
                "UPDATE connection_folders SET sort_order = @Order WHERE id = @Id;",
                new { Order = i, Id = orderedIds[i].ToString("D") }, tx);
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    /// <summary>Borra la carpeta y lo que cuelga: el <c>DELETE</c> nombra sólo la raíz y la cascada de SQLite arrastra el resto. Devuelve los identificadores de las carpetas y las conexiones borradas.</summary>
    public Task<DeletionResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        using var tx = db.BeginTransaction();

        var folderIds = DescendantFolderIds(db, tx, id);

        var connectionIds = db.Query<string>($"""
            SELECT id FROM connections
            WHERE folder_id IN ({Placeholders(folderIds.Count)});
            """, ToParams(folderIds), tx).ToList();

        db.Execute("DELETE FROM connection_folders WHERE id = @Id;",
            new { Id = id.ToString("D") }, tx);

        tx.Commit();

        return Task.FromResult(new DeletionResult(
            folderIds.Select(Guid.Parse).ToList(),
            connectionIds.Select(Guid.Parse).ToList()));
    }

    private static List<string> DescendantFolderIds(
        System.Data.IDbConnection db, System.Data.IDbTransaction tx, Guid root)
    {
        var result = new List<string> { root.ToString("D") };
        var pending = new Queue<string>(result);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var children = db.Query<string>(
                "SELECT id FROM connection_folders WHERE parent_id = @Parent;",
                new { Parent = current }, tx).ToList();

            foreach (var child in children.Where(c => !result.Contains(c)))
            {
                result.Add(child);
                pending.Enqueue(child);
            }
        }

        return result;
    }

    private static void WriteSettings(
        System.Data.IDbConnection db, System.Data.IDbTransaction tx, Folder folder)
    {
        // Sin la guarda de vacio y sin borrar antes: los secretos viven en esta tabla, y el
        // borrado los perdia en cada guardado de ajustes. La fila con todo en nulo se comporta
        // igual que la fila ausente, porque la lectura es un LEFT JOIN.
        var s = folder.Settings;
        db.Execute("""
            INSERT INTO folder_settings (
                folder_id, domain,
                rdp_clipboard_enabled, rdp_ignore_certificate_warnings,
                ssh_auth_method, ssh_private_key_path, ssh_certificate_path,
                ssh_keep_alive_seconds, tag_id, custom_fields,
                rdp_username, ssh_username, web_username, rdp_port, ssh_port, web_port)
            VALUES (
                @FolderId, @Domain,
                @RdpClipboardEnabled, @RdpIgnoreCertificateWarnings,
                @SshAuthMethod, @SshPrivateKeyPath, @SshCertificatePath,
                @SshKeepAliveSeconds, @TagId, @CustomFields,
                @RdpUserName, @SshUserName, @WebUserName, @RdpPort, @SshPort, @WebPort)
            ON CONFLICT(folder_id) DO UPDATE SET
                domain = @Domain,
                rdp_clipboard_enabled = @RdpClipboardEnabled,
                rdp_ignore_certificate_warnings = @RdpIgnoreCertificateWarnings,
                ssh_auth_method = @SshAuthMethod,
                ssh_private_key_path = @SshPrivateKeyPath,
                ssh_certificate_path = @SshCertificatePath,
                ssh_keep_alive_seconds = @SshKeepAliveSeconds,
                tag_id = @TagId,
                custom_fields = @CustomFields,
                rdp_username = @RdpUserName,
                ssh_username = @SshUserName,
                web_username = @WebUserName,
                rdp_port = @RdpPort,
                ssh_port = @SshPort,
                web_port = @WebPort;
            """,
            new
            {
                FolderId = folder.Id.ToString("D"),
                s.Domain,
                RdpClipboardEnabled = ToDb(s.RdpClipboardEnabled),
                RdpIgnoreCertificateWarnings = ToDb(s.RdpIgnoreCertificateWarnings),
                SshAuthMethod = s.SshAuthMethod?.ToString(),
                s.SshPrivateKeyPath,
                s.SshCertificatePath,
                s.SshKeepAliveSeconds,
                TagId = s.TagId?.ToString(),
                CustomFields = Serializacion.CamposATexto(s.CustomFields),
                s.RdpUserName,
                s.SshUserName,
                s.WebUserName,
                s.RdpPort,
                s.SshPort,
                s.WebPort,
            }, tx);
    }

    internal static long? ToDb(bool? value) => value is null ? null : value.Value ? 1L : 0L;

    internal static string Iso(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static string Placeholders(int count) =>
        string.Join(",", Enumerable.Range(0, count).Select(i => $"@p{i}"));

    private static DynamicParameters ToParams(IReadOnlyList<string> values)
    {
        var p = new DynamicParameters();
        for (var i = 0; i < values.Count; i++)
        {
            p.Add($"p{i}", values[i]);
        }

        return p;
    }

    private sealed class FolderRow
    {
        public string Id { get; init; } = string.Empty;
        public string? ParentId { get; init; }
        public string Name { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;

        public string? Domain { get; init; }
        public long RdpTieneSecreto { get; init; }
        public long SshTieneSecreto { get; init; }
        public long WebTieneSecreto { get; init; }
        public long? RdpClipboardEnabled { get; init; }
        public long? RdpIgnoreCertificateWarnings { get; init; }
        public string? SshAuthMethod { get; init; }
        public string? SshPrivateKeyPath { get; init; }
        public string? SshCertificatePath { get; init; }
        public int? SshKeepAliveSeconds { get; init; }
        public string? TagId { get; init; }
        public string? CustomFields { get; init; }
        public string? RdpUsername { get; init; }
        public string? SshUsername { get; init; }
        public string? WebUsername { get; init; }
        public int? RdpPort { get; init; }
        public int? SshPort { get; init; }
        public int? WebPort { get; init; }
        public string? IconColor { get; init; }
        public string? IconKey { get; init; }
        public string? Description { get; init; }

        public Folder ADominio(IAppLogger? logger) => new(
            Guid.Parse(Id),
            Name,
            ParentId is null ? null : Guid.Parse(ParentId),
            SortOrder)
        {
            ClaveDeColor = IconColor,
            ClaveDeIcono = IconKey,
            Description = Description,
            CreatedAt = Fecha(CreatedAt),
            UpdatedAt = Fecha(UpdatedAt),
            Settings = new FolderSettings
            {
                Domain = Domain,
                RdpTieneSecreto = RdpTieneSecreto == 1,
                SshTieneSecreto = SshTieneSecreto == 1,
                WebTieneSecreto = WebTieneSecreto == 1,
                RdpClipboardEnabled = FromDb(RdpClipboardEnabled),
                RdpIgnoreCertificateWarnings = FromDb(RdpIgnoreCertificateWarnings),
                SshAuthMethod = LeerMetodo(SshAuthMethod, Id, logger),
                SshPrivateKeyPath = SshPrivateKeyPath,
                SshCertificatePath = SshCertificatePath,
                SshKeepAliveSeconds = SshKeepAliveSeconds,
                TagId = Guid.TryParse(TagId, out var etiqueta) ? etiqueta : null,
                CustomFields = Serializacion.TextoACampos(CustomFields, Id, logger),
                RdpUserName = RdpUsername,
                SshUserName = SshUsername,
                WebUserName = WebUsername,
                RdpPort = RdpPort,
                SshPort = SshPort,
                WebPort = WebPort,
            },
        };

        private static DateTimeOffset Fecha(string valor) =>
            DateTimeOffset.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Un método de autenticación que el enum ya no tiene se registra y vuelve nulo, en vez de tirar la carga del árbol.</summary>
        private static Domain.Connections.SshAuthMethod? LeerMetodo(
            string? texto, string carpeta, IAppLogger? logger)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return null;
            }

            if (Enum.TryParse<Domain.Connections.SshAuthMethod>(texto, out var valor)
                && Enum.IsDefined(valor))
            {
                return valor;
            }

            logger?.TechnicalError(
                $"leer ssh_auth_method de la carpeta {carpeta}: «{texto}» no es un valor conocido",
                new InvalidOperationException(texto));

            return null;
        }

        private static bool? FromDb(long? value) => value is null ? null : value != 0;
    }
}
