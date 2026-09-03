using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.UseCases.Folders;

public sealed record FolderDeletionImpact(int FolderCount, int ConnectionCount);

public sealed class FolderService
{
    private readonly IFolderRepository _folders;
    private readonly IConnectionRepository _connections;
    private readonly ICredentialStore _credentials;

    public FolderService(
        IFolderRepository folders,
        IConnectionRepository connections,
        ICredentialStore credentials)
    {
        _folders = folders;
        _connections = connections;
        _credentials = credentials;
    }

    public Task<IReadOnlyList<Folder>> GetAllAsync(CancellationToken ct = default) =>
        _folders.GetAllAsync(ct);

    public async Task<OperationResult<Folder>> CreateAsync(
        string name, Guid? parentId, CancellationToken ct = default)
    {
        Folder folder;
        try
        {
            folder = new Folder(Guid.NewGuid(), name, parentId);
        }
        catch (ArgumentException ex)
        {
            return OperationResult<Folder>.Fail(ex.Message);
        }

        var hermanas = await HermanasDeAsync(parentId, folder.Id, ct).ConfigureAwait(false);
        folder.SortOrder = await AbrirLugarAsync(hermanas, folder, null, ct).ConfigureAwait(false);

        await _folders.AddAsync(folder, ct).ConfigureAwait(false);
        return OperationResult<Folder>.Ok(folder);
    }

    public Task ReorderAsync(
        Guid? parentId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default) =>
        _folders.ReorderAsync(parentId, orderedIds, ct);

    /// <summary>Ordena alfabéticamente los hijos directos y nada más: el contenido de las subcarpetas conserva su orden manual (FR-193c).</summary>
    public async Task<OperationResult> OrdenarHijosAsync(
        Guid? folderId, CancellationToken ct = default)
    {
        var carpetas = await _folders.GetAllAsync(ct).ConfigureAwait(false);
        var conexiones = await _connections.GetAllAsync(ct).ConfigureAwait(false);

        var subcarpetas = OrdenAlfabetico.Ordenar(
            carpetas.Where(f => f.ParentId == folderId), f => f.Name, f => f.Id);

        var propias = OrdenAlfabetico.Ordenar(
            conexiones.Where(c => c.FolderId == folderId && c.ParentConnectionId is null),
            c => c.Name, c => c.Id);

        await _folders.ReorderAsync(folderId, subcarpetas, ct).ConfigureAwait(false);
        await _connections.ReorderAsync(folderId, propias, ct).ConfigureAwait(false);

        return OperationResult.Ok();
    }

    /// <summary>Cambia la etiqueta sin abrir el editor; <c>null</c> la quita (FR-190).</summary>
    public async Task<OperationResult> SetTagAsync(
        Guid id, Guid? tagId, CancellationToken ct = default)
    {
        var folder = await _folders.GetByIdAsync(id, ct).ConfigureAwait(false);

        if (folder is null)
        {
            return OperationResult.Fail("La carpeta ya no existe.");
        }

        folder.Settings.TagId = tagId;
        await _folders.UpdateAsync(folder, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    private async Task<List<Folder>> HermanasDeAsync(
        Guid? parentId, Guid excepto, CancellationToken ct) =>
        [.. (await _folders.GetAllAsync(ct).ConfigureAwait(false))
            .Where(f => f.ParentId == parentId && f.Id != excepto)];

    /// <summary>Renumera a las hermanas dejando libre el lugar que le toca, y devuelve cuál es.</summary>
    private async Task<int> AbrirLugarAsync(
        List<Folder> hermanas, Folder folder, int? posicion, CancellationToken ct)
    {
        var lugar = Math.Clamp(
            posicion ?? OrdenAlfabetico.Posicion([.. hermanas.Select(f => f.Name)], folder.Name),
            0,
            hermanas.Count);

        var orden = hermanas.Select(f => f.Id).ToList();
        orden.Insert(lugar, folder.Id);

        await _folders.ReorderAsync(folder.ParentId, orden, ct).ConfigureAwait(false);
        return lugar;
    }

    public async Task<OperationResult> RenameAsync(
        Guid id, string newName, CancellationToken ct = default)
    {
        var folder = await _folders.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (folder is null)
        {
            return OperationResult.Fail("La carpeta ya no existe.");
        }

        try
        {
            folder.Rename(newName);
        }
        catch (ArgumentException ex)
        {
            return OperationResult.Fail(ex.Message);
        }

        await _folders.UpdateAsync(folder, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    /// <summary>Mueve la carpeta y le deja su lugar dentro del destino; sin <paramref name="posicion"/> va al alfabético (FR-193, FR-193b).</summary>
    public async Task<OperationResult> MoveAsync(
        Guid id, Guid? newParentId, int? posicion = null, CancellationToken ct = default)
    {
        var all = await _folders.GetAllAsync(ct).ConfigureAwait(false);
        var folder = all.FirstOrDefault(f => f.Id == id);

        if (folder is null)
        {
            return OperationResult.Fail("La carpeta ya no existe.");
        }

        if (newParentId == id)
        {
            return OperationResult.Fail("Una carpeta no puede contenerse a sí misma.");
        }

        if (newParentId is { } target && IsDescendant(all, target, id))
        {
            return OperationResult.Fail(
                "No se puede mover una carpeta dentro de una de sus propias subcarpetas.");
        }

        var hermanas = all.Where(f => f.ParentId == newParentId && f.Id != id).ToList();

        folder.MoveTo(newParentId);
        folder.SortOrder = await AbrirLugarAsync(hermanas, folder, posicion, ct)
            .ConfigureAwait(false);

        await _folders.UpdateAsync(folder, ct).ConfigureAwait(false);
        return OperationResult.Ok();
    }

    public async Task UpdateSettingsAsync(Folder folder, CancellationToken ct = default) =>
        await _folders.UpdateAsync(folder, ct).ConfigureAwait(false);

    // FR-063: cuántas conexiones descendientes cambian de valor efectivo con estos ajustes.
    public async Task<int> GetUpdateImpactAsync(
        Guid folderId,
        FolderSettings proposedSettings,
        IReadOnlySet<Protocol>? credencialesCambiadas = null,
        CancellationToken ct = default)
    {
        var folders = await _folders.GetAllAsync(ct).ConfigureAwait(false);
        var folder = folders.FirstOrDefault(f => f.Id == folderId);

        if (folder is null)
        {
            return 0;
        }

        var affected = DescendantIds(folders, folderId);
        var connections = await _connections.GetAllAsync(ct).ConfigureAwait(false);
        var enRama = connections.Where(c => c.FolderId is { } f && affected.Contains(f)).ToList();

        if (enRama.Count == 0)
        {
            return 0;
        }

        var folderDespues = new Folder(folder.Id, folder.Name, folder.ParentId)
        {
            Settings = proposedSettings,
        };
        var foldersDespues = folders.Select(f => f.Id == folderId ? folderDespues : f).ToList();

        var resolverAntes = new SettingsResolver(folders);
        var resolverDespues = new SettingsResolver(foldersDespues);

        var cambios = 0;

        foreach (var connection in enRama)
        {
            RdpSettings? rdp = null;

            if (connection.Protocol == Protocol.Rdp)
            {
                var registro = await _connections.GetByIdAsync(connection.Id, ct)
                    .ConfigureAwait(false);
                rdp = registro?.Rdp;
            }

            var antes = resolverAntes.Resolve(connection, rdp);
            var despues = resolverDespues.Resolve(connection, rdp);

            if (Difieren(antes, despues) ||
                HeredaCredencialCambiada(antes, folderId, credencialesCambiadas))
            {
                cambios++;
            }
        }

        return cambios;
    }

    private static bool Difieren(EffectiveSettings antes, EffectiveSettings despues) =>
        antes.ResolvedPort != despues.ResolvedPort ||
        antes.UserName.Value != despues.UserName.Value ||
        antes.CredentialKey.Value != despues.CredentialKey.Value ||
        antes.Domain.Value != despues.Domain.Value;

    private static bool HeredaCredencialCambiada(
        EffectiveSettings antes, Guid folderId, IReadOnlySet<Protocol>? credencialesCambiadas) =>
        credencialesCambiadas is { Count: > 0 } &&
        credencialesCambiadas.Contains(antes.Protocol) &&
        antes.CredentialKey.SourceFolderId == folderId;

    public async Task<FolderDeletionImpact> GetDeletionImpactAsync(
        Guid id, CancellationToken ct = default)
    {
        var folders = await _folders.GetAllAsync(ct).ConfigureAwait(false);
        var connections = await _connections.GetAllAsync(ct).ConfigureAwait(false);

        var affected = DescendantIds(folders, id);
        var count = connections.Count(c => c.FolderId is { } f && affected.Contains(f));

        return new FolderDeletionImpact(affected.Count, count);
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var folders = await _folders.GetAllAsync(ct).ConfigureAwait(false);
        var affected = DescendantIds(folders, id);

        var connections = await _connections.GetAllAsync(ct).ConfigureAwait(false);
        var claves = connections
            .Where(c => c.FolderId is { } f && affected.Contains(f))
            .Select(c => c.CredentialKey)
            .Where(k => k is not null)
            .Concat(folders
                .Where(f => affected.Contains(f.Id))
                .SelectMany(f => new[]
                {
                    f.Settings.RdpCredentialKey,
                    f.Settings.SshCredentialKey,
                    f.Settings.WebCredentialKey,
                }))
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .ToList();

        var sobreviven = connections
            .Where(c => c.FolderId is not { } f || !affected.Contains(f))
            .Select(c => c.CredentialKey)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet(StringComparer.Ordinal);

        claves.RemoveAll(k => sobreviven.Contains(k!));

        await _folders.DeleteAsync(id, ct).ConfigureAwait(false);

        foreach (var clave in claves)
        {
            try
            {
                await _credentials.DeleteAsync(clave!, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        return OperationResult.Ok();
    }

    private static bool IsDescendant(IReadOnlyList<Folder> all, Guid candidate, Guid ancestor)
    {
        var visited = new HashSet<Guid>();
        var current = all.FirstOrDefault(f => f.Id == candidate);

        while (current is not null && visited.Add(current.Id))
        {
            if (current.ParentId == ancestor)
            {
                return true;
            }

            current = current.ParentId is { } p ? all.FirstOrDefault(f => f.Id == p) : null;
        }

        return false;
    }

    private static HashSet<Guid> DescendantIds(IReadOnlyList<Folder> all, Guid root)
    {
        var result = new HashSet<Guid> { root };
        var pending = new Queue<Guid>([root]);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var child in all.Where(f => f.ParentId == current))
            {
                if (result.Add(child.Id))
                {
                    pending.Enqueue(child.Id);
                }
            }
        }

        return result;
    }
}
