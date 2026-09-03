using CafManagerConection.Domain.Connections;

namespace CafManagerConection.UseCases.Inheritance;

/// <summary>Recorre el árbol hacia arriba: conexión, carpeta contenedora, padre, raíz (FR-060).</summary>
public sealed class SettingsResolver
{
    private readonly IReadOnlyDictionary<Guid, Folder> _folders;

    public SettingsResolver(IEnumerable<Folder> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        _folders = folders.ToDictionary(f => f.Id);
    }

    /// <summary>Cadena de carpetas hasta la raíz; corta si detecta un ciclo.</summary>
    public IReadOnlyList<Folder> AncestryOf(Guid? folderId)
    {
        var chain = new List<Folder>();
        var visited = new HashSet<Guid>();
        var current = folderId;

        while (current is { } id && _folders.TryGetValue(id, out var folder))
        {
            if (!visited.Add(id))
            {
                break; // Ciclo: los datos estan corruptos, pero no colgamos la aplicacion.
            }

            chain.Add(folder);
            current = folder.ParentId;
        }

        return chain;
    }

    public EffectiveSettings Resolve(
        Connection connection,
        RdpSettings? rdp = null,
        SshSettings? ssh = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var ancestry = AncestryOf(connection.FolderId);

        return new EffectiveSettings
        {
            ConnectionId = connection.Id,
            Protocol = connection.Protocol,
            Host = connection.Host,

            Port = Resolve(connection.Port, ancestry, s => s.Port),
            TagId = Resolve(connection.TagId, ancestry, s => s.TagId),
            UserName = ResolveRef(connection.UserName, ancestry, s => s.UserName),
            CredentialKey = ResolveRef(
                connection.CredentialKey, ancestry, s => s.CredentialKeyFor(connection.Protocol)),
            Domain = ResolveRef(rdp?.Domain, ancestry, s => s.Domain),

            ClipboardEnabled = Resolve(rdp?.ClipboardEnabled, ancestry, s => s.RdpClipboardEnabled),
            FitToTab = Resolve(rdp?.FitToTab, ancestry, s => s.RdpFitToTab),
            IgnoreCertificateWarnings = Resolve(
                rdp?.IgnoreCertificateWarnings, ancestry, s => s.RdpIgnoreCertificateWarnings),
            UseWindowsIdentity = AjustesReservados.UsaIdentidadDeWindows(connection)
                ? Inherited<bool>.Own(true)
                : Inherited<bool>.None,

            AuthMethod = Resolve(ssh?.AuthMethod, ancestry, s => s.SshAuthMethod),
            PrivateKeyPath = ResolveRef(ssh?.PrivateKeyPath, ancestry, s => s.SshPrivateKeyPath),
            CertificatePath = ResolveRef(
                ssh?.CertificatePath, ancestry, s => s.SshCertificatePath),
            KeepAliveSeconds = Resolve(ssh?.KeepAliveSeconds, ancestry, s => s.SshKeepAliveSeconds),
        };
    }

    /// <summary>Resolución para tipos de valor: <c>null</c> significa heredar.</summary>
    private static Inherited<T> Resolve<T>(
        T? own,
        IReadOnlyList<Folder> ancestry,
        Func<FolderSettings, T?> selector)
        where T : struct
    {
        if (own is { } propio)
        {
            return Inherited<T>.Own(propio);
        }

        foreach (var folder in ancestry)
        {
            if (selector(folder.Settings) is { } heredado)
            {
                return Inherited<T>.From(heredado, folder.Id);
            }
        }

        return Inherited<T>.None;
    }

    /// <summary>Para cadenas; una cadena vacía cuenta como no definida.</summary>
    private static Inherited<string> ResolveRef(
        string? own,
        IReadOnlyList<Folder> ancestry,
        Func<FolderSettings, string?> selector)
    {
        if (!string.IsNullOrEmpty(own))
        {
            return Inherited<string>.Own(own);
        }

        foreach (var folder in ancestry)
        {
            var heredado = selector(folder.Settings);
            if (!string.IsNullOrEmpty(heredado))
            {
                return Inherited<string>.From(heredado, folder.Id);
            }
        }

        return Inherited<string>.None;
    }

    /// <summary>Si mover la conexión a otra carpeta cambia alguno de sus valores efectivos (FR-062).</summary>
    public IReadOnlyList<string> DiffOnMove(
        Connection connection,
        Guid? newFolderId,
        RdpSettings? rdp = null,
        SshSettings? ssh = null)
    {
        var antes = Resolve(connection, rdp, ssh);

        var originalFolder = connection.FolderId;
        connection.FolderId = newFolderId;
        var despues = Resolve(connection, rdp, ssh);
        connection.FolderId = originalFolder;

        var cambios = new List<string>();

        if (antes.ResolvedPort != despues.ResolvedPort)
        {
            cambios.Add($"Puerto: {antes.ResolvedPort} → {despues.ResolvedPort}");
        }

        if (antes.UserName.Value != despues.UserName.Value)
        {
            cambios.Add($"Usuario: {Show(antes.UserName.Value)} → {Show(despues.UserName.Value)}");
        }

        if (antes.CredentialKey.Value != despues.CredentialKey.Value)
        {
            cambios.Add("La credencial que se usará al conectar cambia");
        }

        return cambios;

        static string Show(string? v) => string.IsNullOrEmpty(v) ? "(sin definir)" : v;
    }
}
