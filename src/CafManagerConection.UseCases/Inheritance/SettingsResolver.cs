using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;

namespace CafManagerConection.UseCases.Inheritance;

/// <summary>Recorre el árbol hacia arriba: conexión, carpeta contenedora, padre, raíz.</summary>
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

            Port = Resolve(connection.Port, ancestry, s => s.PuertoDe(connection.Protocol)),
            TagId = Resolve(connection.TagId, ancestry, s => s.TagId),
            UserName = ResolveRef(connection.UserName, ancestry, s => s.UsuarioDe(connection.Protocol)),
            Secreto = ResolverSecreto(connection, ancestry),
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

            Rendimiento = Convertir(
                ResolveReservado(connection, ancestry, AjustesReservados.Rendimiento),
                BanderasNoVacias),
            TipoDeRed = Convertir(
                ResolveReservado(connection, ancestry, AjustesReservados.TipoDeRed),
                AjustesReservados.ParseTipoDeRed),
            ModoDeTamano = Convertir(
                ResolveReservado(connection, ancestry, AjustesReservados.ModoDeTamano),
                AjustesReservados.ParseModoDeTamano),
            EscalaDeEscritorio = Convertir(
                ResolveReservado(connection, ancestry, AjustesReservados.EscalaDeEscritorio),
                EscalaDeEscritorio),
            EscalaDeDispositivo = Convertir(
                ResolveReservado(connection, ancestry, AjustesReservados.EscalaDeDispositivo),
                EscalaDeDispositivo),
            ProgramaInicial = ResolveReservado(
                connection, ancestry, AjustesReservados.ProgramaInicial),
            DirectorioDeTrabajo = ResolveReservado(
                connection, ancestry, AjustesReservados.DirectorioDeTrabajo),
            ComoRemoteApp = Convertir(
                ResolveReservado(connection, ancestry, AjustesReservados.ComoRemoteApp),
                RemoteAppVerdadero),
        };
    }

    private static BanderasDeRendimientoRdp? BanderasNoVacias(string texto)
    {
        var b = AjustesReservados.ParseBanderas(texto);
        return b == BanderasDeRendimientoRdp.Ninguna ? null : b;
    }

    private static bool? RemoteAppVerdadero(string texto) =>
        bool.TryParse(texto, out var v) && v ? true : null;

    private static int? EscalaDeEscritorio(string texto) =>
        int.TryParse(texto, out var v) && EscalasDeRdp.EscritorioEsValida(v) ? v : null;

    private static int? EscalaDeDispositivo(string texto) =>
        int.TryParse(texto, out var v) && EscalasDeRdp.DispositivoEsValida(v) ? v : null;

    /// <summary>Cascada de un campo reservado <c>cmc:</c>: gana el de la conexión, y si no, la primera carpeta que lo tenga.</summary>
    private static Inherited<string> ResolveReservado(
        Connection connection, IReadOnlyList<Folder> ancestry, string clave)
    {
        if (connection.CustomFields.TryGetValue(clave, out var propio)
            && !string.IsNullOrEmpty(propio))
        {
            return Inherited<string>.Own(propio);
        }

        foreach (var folder in ancestry)
        {
            if (folder.Settings.CustomFields.TryGetValue(clave, out var heredado)
                && !string.IsNullOrEmpty(heredado))
            {
                return Inherited<string>.From(heredado, folder.Id);
            }
        }

        return Inherited<string>.None;
    }

    /// <summary>Convierte el crudo resuelto a un tipo de valor conservando de dónde salió.</summary>
    private static Inherited<T> Convertir<T>(Inherited<string> crudo, Func<string, T?> parse)
        where T : struct =>
        crudo.IsDefined && parse(crudo.Value!) is { } valor
            ? new Inherited<T>(valor, crudo.Source, crudo.SourceFolderId)
            : Inherited<T>.None;

    // La propia gana; si no tiene, sube hasta la primera carpeta que tenga una para este
    // protocolo. Es la misma cascada que el resto, pero lo que se propaga es donde esta el
    // secreto y no un valor.
    private static Inherited<ReferenciaDeSecreto> ResolverSecreto(
        Connection connection, IReadOnlyList<Folder> ancestry)
    {
        if (connection.TieneSecreto)
        {
            return Inherited<ReferenciaDeSecreto>.Own(
                ReferenciaDeSecreto.DeConexion(connection.Id, connection.Protocol));
        }

        foreach (var folder in ancestry)
        {
            if (folder.Settings.TieneSecretoPara(connection.Protocol))
            {
                return Inherited<ReferenciaDeSecreto>.From(
                    ReferenciaDeSecreto.DeCarpeta(folder.Id, connection.Protocol), folder.Id);
            }
        }

        return Inherited<ReferenciaDeSecreto>.None;
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

    /// <summary>Si mover la conexión a otra carpeta cambia alguno de sus valores efectivos.</summary>
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

        if (antes.Secreto.Value != despues.Secreto.Value)
        {
            cambios.Add("La contraseña que se usará al conectar cambia");
        }

        return cambios;

        static string Show(string? v) => string.IsNullOrEmpty(v) ? "(sin definir)" : v;
    }
}
