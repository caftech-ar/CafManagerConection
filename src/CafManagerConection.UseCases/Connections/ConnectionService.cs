using System.Globalization;
using System.Text;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Folders;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Connections;

public sealed record ConnectionSummary(
    Guid Id,
    Guid? FolderId,
    string Name,
    Protocol Protocol,
    string Host,
    int EffectivePort,
    string? EffectiveUserName,
    DateTimeOffset? LastConnectedAt,
    int SortOrder,
    Guid? ParentConnectionId = null,
    string? Description = null,
    string? ClaveDeColor = null,
    bool IsFavorite = false,
    Etiqueta? Etiqueta = null,
    string? ClaveDeIcono = null)
{
    public bool TieneEtiqueta => Etiqueta is not null;
}

public sealed class ConnectionService
{
    /// <summary>Campo propio que marca una conexión rápida mientras dura su sesión (FR-149, FR-133).</summary>
    private const string ClaveDeConexionRapida = "cmc:conexionRapida";

    /// <summary>Entrada de <see cref="Exception.Data"/> con el nombre de la clave que quedó huérfana; nunca el secreto (Principio II, FR-158).</summary>
    public const string DatoDeCredencialHuerfana = "cmc:credencialHuerfana";

    private readonly IConnectionRepository _connections;
    private readonly IFolderRepository _folders;
    private readonly ICredentialStore _credentials;
    private readonly ITagRepository? _tags;
    private readonly IAppLogger? _registro;

    public ConnectionService(
        IConnectionRepository connections,
        IFolderRepository folders,
        ICredentialStore credentials,
        ITagRepository? tags = null,
        IAppLogger? registro = null)
    {
        _connections = connections;
        _folders = folders;
        _credentials = credentials;
        _tags = tags;
        _registro = registro;
    }

    public async Task<SettingsResolver> CreateResolverAsync(CancellationToken ct = default) =>
        new(await _folders.GetAllAsync(ct).ConfigureAwait(false));

    public async Task<IReadOnlyList<ConnectionSummary>> GetTreeAsync(CancellationToken ct = default)
    {
        var resolver = await CreateResolverAsync(ct).ConfigureAwait(false);
        var connections = await _connections.GetAllAsync(ct).ConfigureAwait(false);
        var catalogo = await LeerCatalogoAsync(ct).ConfigureAwait(false);

        // La conexión rápida vive en el mismo repositorio pero no es una entrada del árbol; se filtra acá (FR-149).
        return connections
            .Where(c => !EsConexionRapida(c))
            .Select(c => ToSummary(c, resolver, catalogo))
            .ToList();
    }

    /// <summary>Arma el destino de una conexión rápida sin dejar rastro en el árbol (FR-149).</summary>
    public async Task<OperationResult<Guid>> CreateQuickAsync(
        string? userName, string host, int port, CancellationToken ct = default)
    {
        var nombre = string.IsNullOrWhiteSpace(userName) ? host : $"{userName}@{host}";

        if (nombre.Length > Connection.MaxNameLength)
        {
            nombre = nombre[..Connection.MaxNameLength];
        }

        var conexion = new Connection(Guid.NewGuid(), nombre, Protocol.Ssh, host)
        {
            UserName = userName,
        };

        if (port != Connection.DefaultPortFor(Protocol.Ssh))
        {
            conexion.SetPort(port);
        }

        conexion.SetCustomField(ClaveDeConexionRapida, bool.TrueString);

        var record = new ConnectionRecord(conexion);
        var validation = ConnectionValidator.Validate(record);

        if (!validation.IsValid)
        {
            return OperationResult<Guid>.Fail(validation.ToMessage());
        }

        await _connections.AddAsync(record, ct).ConfigureAwait(false);
        return OperationResult<Guid>.Ok(conexion.Id);
    }

    /// <summary>Apaga la marca que la escondía del árbol; el resto lo define el editor de siempre (FR-149).</summary>
    public async Task<OperationResult> MarkAsSavedAsync(Guid id, CancellationToken ct = default)
    {
        var record = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);

        if (record is null)
        {
            return OperationResult.Fail("La conexión ya no existe.");
        }

        record.Connection.SetCustomField(ClaveDeConexionRapida, null);
        await _connections.UpdateAsync(record, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    public async Task<int> LimpiarConexionesRapidasAsync(CancellationToken ct = default)
    {
        var todas = await _connections.GetAllAsync(ct).ConfigureAwait(false);
        var huerfanas = todas.Where(EsConexionRapida).ToList();

        foreach (var conexion in huerfanas)
        {
            await DeleteAsync(conexion.Id, ct).ConfigureAwait(false);
        }

        return huerfanas.Count;
    }

    private static bool EsConexionRapida(Connection c) =>
        c.CustomFields.ContainsKey(ClaveDeConexionRapida);

    /// <summary>Filtra por nombre, host o usuario sin distinguir mayúsculas ni acentos (FR-007).</summary>
    public async Task<IReadOnlyList<ConnectionSummary>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        var all = await GetTreeAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(query))
        {
            return all;
        }

        var needle = Normalize(query);

        return all.Where(c => Coincide(c, needle)).ToList();
    }

    /// <summary>Busca también en descripción, etiqueta y entorno por su nombre, no por la sigla (FR-129, FR-131).</summary>
    private static bool Coincide(ConnectionSummary c, string needle) =>
        Contiene(c.Name, needle) ||
        Contiene(c.Host, needle) ||
        Contiene(c.EffectiveUserName, needle) ||
        Contiene(c.Description, needle) ||
        Contiene(c.Etiqueta?.Nombre, needle) ||
        Contiene(c.Etiqueta?.Codigo, needle);

    private static bool Contiene(string? valor, string needle) =>
        !string.IsNullOrEmpty(valor) &&
        Normalize(valor).Contains(needle, StringComparison.Ordinal);

    public Task<ConnectionRecord?> GetDetailAsync(Guid id, CancellationToken ct = default) =>
        _connections.GetByIdAsync(id, ct);

    public async Task<OperationResult<Guid>> CreateAsync(
        ConnectionRecord record,
        CredentialPromptResult? credential = null,
        CancellationToken ct = default)
    {
        var validation = ConnectionValidator.Validate(record);
        if (!validation.IsValid)
        {
            return OperationResult<Guid>.Fail(validation.ToMessage());
        }

        string? claveEscrita = null;

        if (credential is not null)
        {
            var key = CredentialKey.ForConnection(record.Connection.Id, record.Connection.Protocol);
            using var stored = new StoredCredential(
                credential.UserName, credential.Domain, credential.Secret);

            await _credentials.WriteAsync(key.Value, stored, ct).ConfigureAwait(false);
            claveEscrita = key.Value;
            record.Connection.CredentialKey = key.Value;
            record.Connection.UserName ??= credential.UserName;
        }

        try
        {
            record.Connection.SortOrder =
                await AbrirLugarAsync(record.Connection, null, ct).ConfigureAwait(false);

            await _connections.AddAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // La conexión es nueva: la clave recién escrita no la referencia nadie más (Principio II).
            if (claveEscrita is not null)
            {
                await CompensarCredencialAsync(claveEscrita, ex).ConfigureAwait(false);
            }

            throw;
        }

        return OperationResult<Guid>.Ok(record.Connection.Id);
    }

    public async Task<OperationResult> UpdateAsync(
        ConnectionRecord record,
        CredentialPromptResult? credential = null,
        CancellationToken ct = default)
    {
        var validation = ConnectionValidator.Validate(record);
        if (!validation.IsValid)
        {
            return OperationResult.Fail(validation.ToMessage());
        }

        var claveOriginal = record.Connection.CredentialKey;
        string? claveNueva = null;

        if (credential is not null)
        {
            var key = CredentialKey.ForConnection(record.Connection.Id, record.Connection.Protocol);
            var existiaAntes = await _credentials.ExistsAsync(key.Value, ct).ConfigureAwait(false);

            using var stored = new StoredCredential(
                credential.UserName, credential.Domain, credential.Secret);

            await _credentials.WriteAsync(key.Value, stored, ct).ConfigureAwait(false);
            record.Connection.CredentialKey = key.Value;

            if (!existiaAntes)
            {
                claveNueva = key.Value;
            }
        }

        record.Connection.Touch();

        try
        {
            await _connections.UpdateAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            record.Connection.CredentialKey = claveOriginal;

            if (claveNueva is not null)
            {
                await CompensarCredencialAsync(claveNueva, ex).ConfigureAwait(false);
            }

            throw;
        }

        return OperationResult.Ok();
    }

    private async Task CompensarCredencialAsync(string clave, Exception causa)
    {
        try
        {
            await _credentials.DeleteAsync(clave, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception falla)
        {
            // La clave es la referencia opaca, no el secreto: registrarla es lo que permite limpiarla.
            _registro?.TechnicalError($"limpiar la credencial huérfana {clave}", falla);
            causa.Data[DatoDeCredencialHuerfana] = clave;
        }
    }

    /// <summary>Elimina la credencial y después la conexión: si la credencial falla, la conexión queda (FR-038).</summary>
    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var record = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (record is null)
        {
            return OperationResult.Fail("La conexión ya no existe.");
        }

        if (record.Connection.CredentialKey is { } key
            && !await OtraLaUsaAsync(key, id, ct).ConfigureAwait(false))
        {
            try
            {
                await _credentials.DeleteAsync(key, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(
                    "No se pudo borrar la credencial guardada, así que la conexión no se " +
                    $"eliminó para no dejarla huérfana. Detalle: {ex.Message}");
            }
        }

        await _connections.DeleteAsync(id, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ClearCredentialAsync(Guid id, CancellationToken ct = default)
    {
        var record = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (record is null)
        {
            return OperationResult.Fail("La conexión ya no existe.");
        }

        if (record.Connection.CredentialKey is { } key
            && !await OtraLaUsaAsync(key, id, ct).ConfigureAwait(false))
        {
            await _credentials.DeleteAsync(key, ct).ConfigureAwait(false);
        }

        record.Connection.CredentialKey = null;
        await _connections.UpdateAsync(record, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    private async Task<bool> OtraLaUsaAsync(string key, Guid excepto, CancellationToken ct)
    {
        var todas = await _connections.GetAllAsync(ct).ConfigureAwait(false);

        return todas.Any(c =>
            c.Id != excepto &&
            string.Equals(c.CredentialKey, key, StringComparison.Ordinal));
    }

    public async Task<bool> HasStoredCredentialAsync(Guid id, CancellationToken ct = default)
    {
        var record = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);

        return record?.Connection.CredentialKey is { } key &&
               await _credentials.ExistsAsync(key, ct).ConfigureAwait(false);
    }

    /// <summary>Se advierte, no se impide (FR-053).</summary>
    public async Task<bool> IsNameDuplicatedAsync(
        Guid? folderId, string name, Guid? excludingId = null, CancellationToken ct = default)
    {
        var all = await _connections.GetAllAsync(ct).ConfigureAwait(false);

        return all.Any(c =>
            c.FolderId == folderId &&
            c.Id != excludingId &&
            string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<OperationResult<Guid>> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var original = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (original is null)
        {
            return OperationResult<Guid>.Fail("La conexión ya no existe.");
        }

        var o = original.Connection;
        var copia = new Connection(Guid.NewGuid(), $"{o.Name} (copia)", o.Protocol, o.Host)
        {
            FolderId = o.FolderId,
            UserName = o.UserName,
            Notes = o.Notes,
            SortOrder = o.SortOrder + 1,
            Description = o.Description,
            ClaveDeColor = o.ClaveDeColor,
            ClaveDeIcono = o.ClaveDeIcono,
            IsFavorite = o.IsFavorite,
            TagId = o.TagId,
            DocumentationUrl = o.DocumentationUrl,
        };
        copia.SetPort(o.Port);

        foreach (var (nombre, valor) in o.CustomFields)
        {
            copia.SetCustomField(nombre, valor);
        }

        if (o.CredentialKey is { } claveOriginal)
        {
            using var secreto = await _credentials.ReadAsync(claveOriginal, ct).ConfigureAwait(false);
            if (secreto is not null)
            {
                var claveNueva = CredentialKey.ForConnection(copia.Id, copia.Protocol);
                await _credentials.WriteAsync(claveNueva.Value, secreto, ct).ConfigureAwait(false);
                copia.CredentialKey = claveNueva.Value;
            }
        }

        var record = new ConnectionRecord(
            copia,
            original.Rdp is null ? null : Clone(original.Rdp, copia.Id),
            original.Ssh is null ? null : Clone(original.Ssh, copia.Id),
            original.Web is null ? null : Clone(original.Web, copia.Id));

        await _connections.AddAsync(record, ct).ConfigureAwait(false);
        return OperationResult<Guid>.Ok(copia.Id);
    }

    /// <summary>Mueve la conexión y le deja su lugar dentro de la carpeta; sin <paramref name="posicion"/> va al alfabético (FR-193b, FR-194).</summary>
    public async Task<OperationResult> MoveAsync(
        Guid id, Guid? folderId, int? posicion = null, CancellationToken ct = default)
    {
        var record = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (record is null)
        {
            return OperationResult.Fail("La conexión ya no existe.");
        }

        record.Connection.FolderId = folderId;
        record.Connection.ParentConnectionId = null;

        record.Connection.SortOrder =
            await AbrirLugarAsync(record.Connection, posicion, ct).ConfigureAwait(false);

        record.Connection.Touch();
        await _connections.UpdateAsync(record, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    /// <summary>Cambia la etiqueta sin abrir el editor; <c>null</c> la quita (FR-190).</summary>
    public async Task<OperationResult> SetTagAsync(
        Guid id, Guid? tagId, CancellationToken ct = default)
    {
        var record = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (record is null)
        {
            return OperationResult.Fail("La conexión ya no existe.");
        }

        record.Connection.TagId = tagId;
        record.Connection.Touch();
        await _connections.UpdateAsync(record, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    /// <summary>Renumera a las hermanas dejando libre el lugar que le toca, y devuelve cuál es.</summary>
    private async Task<int> AbrirLugarAsync(
        Connection conexion, int? posicion, CancellationToken ct)
    {
        var hermanas = (await _connections.GetAllAsync(ct).ConfigureAwait(false))
            .Where(c => c.Id != conexion.Id
                        && c.FolderId == conexion.FolderId
                        && c.ParentConnectionId == conexion.ParentConnectionId
                        && !EsConexionRapida(c))
            .ToList();

        var lugar = Math.Clamp(
            posicion ?? OrdenAlfabetico.Posicion(
                [.. hermanas.Select(c => c.Name)], conexion.Name),
            0,
            hermanas.Count);

        var orden = hermanas.Select(c => c.Id).ToList();
        orden.Insert(lugar, conexion.Id);

        await _connections.ReorderAsync(conexion.FolderId, orden, ct).ConfigureAwait(false);
        return lugar;
    }

    /// <summary>Cuelga una conexión de otra, o la suelta si <paramref name="parentId"/> es <c>null</c> (FR-125).</summary>
    public async Task<OperationResult> SetParentAsync(
        Guid id, Guid? parentId, CancellationToken ct = default)
    {
        var record = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (record is null)
        {
            return OperationResult.Fail("La conexión ya no existe.");
        }

        Connection? padre = null;

        if (parentId is { } pid)
        {
            var registroPadre = await _connections.GetByIdAsync(pid, ct).ConfigureAwait(false);
            if (registroPadre is null)
            {
                return OperationResult.Fail("La conexión de destino ya no existe.");
            }

            padre = registroPadre.Connection;
        }

        var todas = await _connections.GetAllAsync(ct).ConfigureAwait(false);
        var tieneHijas = todas.Any(c => c.ParentConnectionId == id);

        var validacion = ConnectionValidator.ValidateParent(record.Connection, padre, tieneHijas);

        if (!validacion.IsValid)
        {
            return OperationResult.Fail(validacion.ToMessage());
        }

        record.Connection.ParentConnectionId = parentId;

        if (padre is not null)
        {
            record.Connection.FolderId = padre.FolderId;
        }

        record.Connection.Touch();
        await _connections.UpdateAsync(record, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    public async Task<IReadOnlyList<string>> PreviewMoveAsync(
        Guid id, Guid? newFolderId, CancellationToken ct = default)
    {
        var record = await _connections.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (record is null)
        {
            return [];
        }

        var resolver = await CreateResolverAsync(ct).ConfigureAwait(false);
        return resolver.DiffOnMove(record.Connection, newFolderId, record.Rdp, record.Ssh);
    }

    public Task ReorderAsync(
        Guid? folderId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default) =>
        _connections.ReorderAsync(folderId, orderedIds, ct);

    private async Task<CatalogoDeEtiquetas> LeerCatalogoAsync(CancellationToken ct) =>
        _tags is null
            ? new CatalogoDeEtiquetas()
            : new CatalogoDeEtiquetas(await _tags.GetAllAsync(ct).ConfigureAwait(false));

    private static ConnectionSummary ToSummary(
        Connection c, SettingsResolver resolver, CatalogoDeEtiquetas catalogo)
    {
        var efectivo = resolver.Resolve(c);

        var etiqueta = efectivo.TagId.IsDefined ? catalogo.Por(efectivo.TagId.Value) : null;

        return new ConnectionSummary(
            c.Id, c.FolderId, c.Name, c.Protocol, c.Host,
            efectivo.ResolvedPort, efectivo.UserName.Value, c.LastConnectedAt, c.SortOrder,
            c.ParentConnectionId,
            c.Description,
            c.ClaveDeColor,
            c.IsFavorite,
            etiqueta,
            c.ClaveDeIcono);
    }

    internal static string Normalize(string value)
    {
        var descompuesto = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);

        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static RdpSettings Clone(RdpSettings s, Guid newId) => new()
    {
        ConnectionId = newId,
        Domain = s.Domain,
        ClipboardEnabled = s.ClipboardEnabled,
        FitToTab = s.FitToTab,
        IgnoreCertificateWarnings = s.IgnoreCertificateWarnings,
        StartFullScreen = s.StartFullScreen,
    };

    private static SshSettings Clone(SshSettings s, Guid newId) => new()
    {
        ConnectionId = newId,
        AuthMethod = s.AuthMethod,
        PrivateKeyPath = s.PrivateKeyPath,
        CertificatePath = s.CertificatePath,
        KnownHostFingerprint = s.KnownHostFingerprint,
        KeepAliveSeconds = s.KeepAliveSeconds,
        Encoding = s.Encoding,
    };

    private static WebSettings Clone(WebSettings s, Guid newId) => new()
    {
        ConnectionId = newId,
        Url = s.Url,
        Browser = s.Browser,
        PrivateWindow = s.PrivateWindow,
    };
}
