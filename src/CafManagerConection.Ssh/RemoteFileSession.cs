using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace CafManagerConection.Ssh;

public sealed record RemoteEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long SizeBytes,
    DateTimeOffset ModifiedAt);

public readonly record struct TransferProgress(long BytesTransferred, long TotalBytes)
{
    public double Percent => TotalBytes == 0 ? 0 : BytesTransferred * 100.0 / TotalBytes;
}

public enum ConflictResolution
{
    Ask,
    Overwrite,
    Skip,
    KeepBoth,
}

public sealed record TransferOutcome(string File, bool Success, string? Error, bool Skipped);

public sealed record DirectoryTransferOutcome(int Transferred, int Failed, string? Error);

/// <summary>Lo que el servidor devuelve de un nivel, antes de decidir qué se ofrece.</summary>
public readonly record struct EntradaCruda(
    string Name,
    string FullPath,
    bool IsDirectory,
    bool IsSymbolicLink,
    long SizeBytes,
    DateTimeOffset ModifiedAt);

public sealed record RemoteListing(IReadOnlyList<RemoteEntry> Entries, int SymbolicLinksOmitted)
{
    public static RemoteListing Vacio { get; } = new([], 0);
}

/// <summary>FR-078: un enlace simbólico no se ofrece, y FR-189c exige decir cuántos se sacaron.</summary>
public static class ListadoRemoto
{
    public static RemoteListing Filtrar(IEnumerable<EntradaCruda> crudas)
    {
        var utiles = crudas.Where(e => e.Name is not "." and not "..").ToList();

        var entradas = utiles
            .Where(e => !e.IsSymbolicLink)
            .Select(e => new RemoteEntry(
                e.Name,
                e.FullPath,
                e.IsDirectory,
                e.IsDirectory ? 0 : e.SizeBytes,
                e.ModifiedAt))
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RemoteListing(entradas, utiles.Count(e => e.IsSymbolicLink));
    }
}

public static class RutaRemota
{
    public static string Combinar(string carpeta, string nombre) =>
        carpeta.TrimEnd('/') + "/" + nombre.TrimStart('/');

    public static string Padre(string ruta)
    {
        var limpia = ruta.TrimEnd('/');
        var corte = limpia.LastIndexOf('/');

        return corte <= 0 ? "/" : limpia[..corte];
    }

    public static string Nombre(string ruta)
    {
        var limpia = ruta.TrimEnd('/');

        return limpia.Length == 0 ? "/" : limpia[(limpia.LastIndexOf('/') + 1)..];
    }

    public static string DesdeRutaLocal(string relativa) => relativa.Replace('\\', '/');

    public static IEnumerable<string> Ascendencia(string ruta)
    {
        var segmentos = ruta.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var acumulada = string.Empty;

        foreach (var segmento in segmentos)
        {
            acumulada += "/" + segmento;
            yield return acumulada;
        }
    }
}

public sealed class RemoteFileSession : IAsyncDisposable
{
    private readonly SshSessionRequest _request;
    private readonly IHostKeyVerifier _verifier;
    private readonly StoredCredential? _credential;

    private SftpClient? _client;

    public RemoteFileSession(
        SshSessionRequest request, IHostKeyVerifier verifier, StoredCredential? credential)
    {
        _request = request;
        _verifier = verifier;
        _credential = credential;
    }

    public bool IsConnected => _client is { IsConnected: true };

    public async Task<string?> ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected)
        {
            return null;
        }

        try
        {
            var sesion = new SshSession(_request, _verifier);
            _client = sesion.CreateSftpClient(_credential);

            await Task.Run(() => _client.Connect(), ct).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return SshSession.Map(ex).UserMessage;
        }
    }

    public string HomeDirectory => _client?.WorkingDirectory ?? "/";

    public async Task<RemoteListing> ListAsync(string path, CancellationToken ct = default)
    {
        if (!IsConnected)
        {
            return RemoteListing.Vacio;
        }

        var crudas = await Task.Run(
            () => _client!.ListDirectory(path)
                .Select(e => new EntradaCruda(
                    e.Name,
                    e.FullName,
                    e.IsDirectory,
                    e.IsSymbolicLink,
                    e.IsDirectory ? 0 : e.Length,
                    new DateTimeOffset(e.LastWriteTimeUtc, TimeSpan.Zero)))
                .ToList(),
            ct).ConfigureAwait(false);

        return ListadoRemoto.Filtrar(crudas);
    }

    public async Task<string?> CreateDirectoryAsync(string path, CancellationToken ct = default)
    {
        try
        {
            await Task.Run(() => _client!.CreateDirectory(path), ct).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return Traducir(ex, path);
        }
    }

    public async Task<string?> RenameAsync(string from, string to, CancellationToken ct = default)
    {
        try
        {
            await Task.Run(() => _client!.RenameFile(from, to), ct).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return Traducir(ex, from);
        }
    }

    public async Task<string?> DeleteAsync(
        string path, bool isDirectory, CancellationToken ct = default)
    {
        try
        {
            await Task.Run(
                () =>
                {
                    if (isDirectory)
                    {
                        BorrarRecursivo(path);
                    }
                    else
                    {
                        _client!.DeleteFile(path);
                    }
                },
                ct).ConfigureAwait(false);

            return null;
        }
        catch (Exception ex)
        {
            return Traducir(ex, path);
        }
    }

    /// <summary>SFTP sólo borra directorios vacíos, así que hay que vaciarlos primero.</summary>
    private void BorrarRecursivo(string path)
    {
        foreach (var e in _client!.ListDirectory(path))
        {
            if (e.Name is "." or "..")
            {
                continue;
            }

            if (e.IsDirectory)
            {
                BorrarRecursivo(e.FullName);
            }
            else
            {
                _client.DeleteFile(e.FullName);
            }
        }

        _client.DeleteDirectory(path);
    }

    public bool Exists(string path)
    {
        try
        {
            return _client?.Exists(path) ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<TransferOutcome> UploadAsync(
        string localPath,
        string remotePath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var nombre = Path.GetFileName(localPath);

        try
        {
            var total = new FileInfo(localPath).Length;

            await Task.Run(
                () =>
                {
                    using var stream = File.OpenRead(localPath);
                    _client!.UploadFile(
                        stream,
                        remotePath,
                        canOverride: true,
                        uploaded =>
                        {
                            ct.ThrowIfCancellationRequested();
                            progress?.Report(new TransferProgress((long)uploaded, total));
                        });
                },
                ct).ConfigureAwait(false);

            return new TransferOutcome(nombre, true, null, false);
        }
        catch (OperationCanceledException)
        {
            return new TransferOutcome(nombre, false, "Cancelado.", false);
        }
        catch (Exception ex)
        {
            return new TransferOutcome(nombre, false, Traducir(ex, remotePath), false);
        }
    }

    public async Task<TransferOutcome> DownloadAsync(
        string remotePath,
        string localPath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var nombre = Path.GetFileName(remotePath);

        try
        {
            var total = await Task.Run(
                () => _client!.GetAttributes(remotePath).Size, ct).ConfigureAwait(false);

            await Task.Run(
                () =>
                {
                    using var stream = File.Create(localPath);
                    _client!.DownloadFile(
                        remotePath,
                        stream,
                        downloaded =>
                        {
                            ct.ThrowIfCancellationRequested();
                            progress?.Report(new TransferProgress((long)downloaded, total));
                        });
                },
                ct).ConfigureAwait(false);

            return new TransferOutcome(nombre, true, null, false);
        }
        catch (OperationCanceledException)
        {
            TryDelete(localPath);
            return new TransferOutcome(nombre, false, "Cancelado.", false);
        }
        catch (Exception ex)
        {
            TryDelete(localPath);
            return new TransferOutcome(nombre, false, Traducir(ex, remotePath), false);
        }
    }

    public Task<DirectoryTransferOutcome> UploadDirectoryAsync(
        string localDirectory,
        string remoteParent,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default) =>
        IsConnected
            ? Task.Run(() => SubirCarpetaAsync(localDirectory, remoteParent, progress, ct), ct)
            : Task.FromResult(
                new DirectoryTransferOutcome(0, 0, "No hay una sesión SFTP abierta."));

    private async Task<DirectoryTransferOutcome> SubirCarpetaAsync(
        string localDirectory,
        string remoteParent,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            var raiz = RutaRemota.Combinar(
                remoteParent, Path.GetFileName(SinBarraFinal(localDirectory)));

            var archivos = ArchivosLocales(localDirectory).ToList();

            await AsegurarDirectorioAsync(raiz, ct).ConfigureAwait(false);

            return await TransferirAsync(
                archivos.Count,
                progress,
                async hecho =>
                {
                    foreach (var local in archivos)
                    {
                        ct.ThrowIfCancellationRequested();

                        var remoto = RutaRemota.Combinar(
                            raiz,
                            RutaRemota.DesdeRutaLocal(
                                Path.GetRelativePath(localDirectory, local)));

                        await AsegurarDirectorioAsync(RutaRemota.Padre(remoto), ct)
                            .ConfigureAwait(false);

                        var resultado = await UploadAsync(local, remoto, null, ct)
                            .ConfigureAwait(false);

                        hecho(resultado.Success);
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new DirectoryTransferOutcome(0, 0, "Cancelado.");
        }
        catch (Exception ex)
        {
            return new DirectoryTransferOutcome(0, 0, Traducir(ex, localDirectory));
        }
    }

    public Task<DirectoryTransferOutcome> DownloadDirectoryAsync(
        string remoteDirectory,
        string localParent,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default) =>
        IsConnected
            ? Task.Run(() => BajarCarpetaAsync(remoteDirectory, localParent, progress, ct), ct)
            : Task.FromResult(
                new DirectoryTransferOutcome(0, 0, "No hay una sesión SFTP abierta."));

    private async Task<DirectoryTransferOutcome> BajarCarpetaAsync(
        string remoteDirectory,
        string localParent,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            var raiz = Path.Combine(localParent, RutaRemota.Nombre(remoteDirectory));
            var pares = new List<(string Remoto, string Local)>();

            await RecolectarAsync(remoteDirectory, raiz, pares, ct).ConfigureAwait(false);

            return await TransferirAsync(
                pares.Count,
                progress,
                async hecho =>
                {
                    foreach (var (remoto, local) in pares)
                    {
                        ct.ThrowIfCancellationRequested();

                        var resultado = await DownloadAsync(remoto, local, null, ct)
                            .ConfigureAwait(false);

                        hecho(resultado.Success);
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new DirectoryTransferOutcome(0, 0, "Cancelado.");
        }
        catch (Exception ex)
        {
            return new DirectoryTransferOutcome(0, 0, Traducir(ex, remoteDirectory));
        }
    }

    private static async Task<DirectoryTransferOutcome> TransferirAsync(
        int total, IProgress<TransferProgress>? progress, Func<Action<bool>, Task> recorrer)
    {
        var hechos = 0;
        var fallados = 0;

        await recorrer(exito =>
        {
            if (exito)
            {
                hechos++;
            }
            else
            {
                fallados++;
            }

            progress?.Report(new TransferProgress(hechos + fallados, total));
        }).ConfigureAwait(false);

        return new DirectoryTransferOutcome(hechos, fallados, null);
    }

    private async Task RecolectarAsync(
        string remoto,
        string local,
        List<(string Remoto, string Local)> pares,
        CancellationToken ct)
    {
        Directory.CreateDirectory(local);

        var listado = await ListAsync(remoto, ct).ConfigureAwait(false);

        foreach (var entrada in listado.Entries)
        {
            if (entrada.IsDirectory)
            {
                await RecolectarAsync(
                        entrada.FullPath, Path.Combine(local, entrada.Name), pares, ct)
                    .ConfigureAwait(false);
            }
            else
            {
                pares.Add((entrada.FullPath, Path.Combine(local, entrada.Name)));
            }
        }
    }

    private async Task AsegurarDirectorioAsync(string path, CancellationToken ct)
    {
        foreach (var nivel in RutaRemota.Ascendencia(path))
        {
            if (!Exists(nivel))
            {
                await Task.Run(() => _client!.CreateDirectory(nivel), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Un punto de reanálisis se saltea: seguirlo puede volver sobre sí mismo (FR-078).</summary>
    private static IEnumerable<string> ArchivosLocales(string carpeta)
    {
        if ((File.GetAttributes(carpeta) & FileAttributes.ReparsePoint) != 0)
        {
            yield break;
        }

        foreach (var archivo in Directory.EnumerateFiles(carpeta))
        {
            if ((File.GetAttributes(archivo) & FileAttributes.ReparsePoint) == 0)
            {
                yield return archivo;
            }
        }

        foreach (var hija in Directory.EnumerateDirectories(carpeta))
        {
            foreach (var archivo in ArchivosLocales(hija))
            {
                yield return archivo;
            }
        }
    }

    private static string SinBarraFinal(string ruta) =>
        ruta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    public string UniqueName(string remotePath)
    {
        var directorio = Path.GetDirectoryName(remotePath)?.Replace('\\', '/') ?? "/";
        var sinExtension = Path.GetFileNameWithoutExtension(remotePath);
        var extension = Path.GetExtension(remotePath);

        for (var i = 1; i < 1000; i++)
        {
            var candidato = $"{directorio}/{sinExtension} ({i}){extension}";

            if (!Exists(candidato))
            {
                return candidato;
            }
        }

        return $"{directorio}/{sinExtension} ({Guid.NewGuid():N}){extension}";
    }

    private static string Traducir(Exception ex, string path) => ex switch
    {
        SftpPermissionDeniedException =>
            $"Sin permisos sobre {path}.",

        SftpPathNotFoundException =>
            $"No se encontró {path}.",

        SshConnectionException =>
            "Se perdió la conexión con el servidor.",

        IOException e when e.Message.Contains("space", StringComparison.OrdinalIgnoreCase) =>
            "No hay espacio suficiente en el destino.",

        _ => ex.Message,
    };

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client is { IsConnected: true })
            {
                await Task.Run(() => _client.Disconnect()).ConfigureAwait(false);
            }

            _client?.Dispose();
            _client = null;
        }
        catch (Exception)
        {
        }
    }
}
