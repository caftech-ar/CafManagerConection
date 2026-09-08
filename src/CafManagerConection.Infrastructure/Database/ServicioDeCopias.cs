using System.Security.Cryptography;
using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.UseCases.Abstractions;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Database;

public sealed record ResultadoDeCopia(bool Hecha, string? Ruta, string? Motivo)
{
    public static ResultadoDeCopia Ok(string ruta) => new(true, ruta, null);

    public static ResultadoDeCopia NoHizoFalta(string motivo) => new(false, null, motivo);

    public static ResultadoDeCopia Fallo(string error) => new(false, null, error);
}

public sealed class ServicioDeCopias
{
    private readonly AppPaths _rutas;
    private readonly IAppLogger? _logger;

    public ServicioDeCopias(AppPaths rutas, IAppLogger? logger = null)
    {
        _rutas = rutas;
        _logger = logger;
    }

    public string CarpetaDe(AjustesDeCopia ajustes)
    {
        ArgumentNullException.ThrowIfNull(ajustes);

        return string.IsNullOrWhiteSpace(ajustes.Carpeta)
            ? Path.Combine(_rutas.Root, "copias")
            : ajustes.Carpeta.Trim();
    }

    public IReadOnlyList<CopiaDeSeguridad> Listar(AjustesDeCopia ajustes)
    {
        var carpeta = CarpetaDe(ajustes);

        if (!Directory.Exists(carpeta))
        {
            return [];
        }

        var copias = new List<CopiaDeSeguridad>();

        foreach (var archivo in Directory.EnumerateFiles(carpeta, "cmc-*.db"))
        {
            if (PoliticaDeCopias.MomentoDe(archivo) is not { } momento)
            {
                continue;
            }

            var info = new FileInfo(archivo);

            copias.Add(new CopiaDeSeguridad(
                archivo, momento, info.Length, LeerHuellaGuardada(archivo)));
        }

        return [.. copias.OrderByDescending(c => c.Momento)];
    }

    public ResultadoDeCopia CopiarSiCorresponde(AjustesDeCopia ajustes, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(ajustes);

        if (!ajustes.Activas)
        {
            return ResultadoDeCopia.NoHizoFalta("Las copias automáticas están desactivadas.");
        }

        try
        {
            var existentes = Listar(ajustes);
            var huella = Huella();

            if (!PoliticaDeCopias.HayQueCopiar(existentes, huella, ahora))
            {
                return ResultadoDeCopia.NoHizoFalta(
                    existentes.Count > 0 && existentes[0].Momento.LocalDateTime.Date
                        == ahora.LocalDateTime.Date
                        ? "Ya hay una copia de hoy."
                        : "La base no cambió desde la última copia.");
            }

            return Copiar(ajustes, ahora, huella);
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError("hacer la copia de seguridad de la base", ex);
            return ResultadoDeCopia.Fallo(ex.Message);
        }
    }

    public ResultadoDeCopia CopiarAhora(AjustesDeCopia ajustes, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(ajustes);

        try
        {
            return Copiar(ajustes, ahora, Huella());
        }
        catch (Exception ex)
        {
            _logger?.TechnicalError("hacer la copia de seguridad de la base", ex);
            return ResultadoDeCopia.Fallo(ex.Message);
        }
    }

    private ResultadoDeCopia Copiar(AjustesDeCopia ajustes, DateTimeOffset ahora, string huella)
    {
        var carpeta = CarpetaDe(ajustes);
        Directory.CreateDirectory(carpeta);

        var destino = Path.Combine(carpeta, PoliticaDeCopias.NombreDeArchivo(ahora));

        Exportar(destino);
        GuardarHuella(destino, huella);
        Rotar(ajustes);

        _logger?.PlatformActionPerformed(Guid.Empty, "copia de seguridad de la base");

        return ResultadoDeCopia.Ok(destino);
    }

    /// <summary>Escribe una copia consistente con la API de respaldo de SQLite y no con <c>File.Copy</c>, que deja afuera el <c>-wal</c>.</summary>
    public void Exportar(string destino)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destino);

        var carpeta = Path.GetDirectoryName(destino);

        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        // Sin agrupar: Microsoft.Data.Sqlite conserva la conexión en un grupo y con ella el descriptor, y la rotación fallaba al borrar por archivo en uso.
        using var origen = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = _rutas.DatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());

        using var copia = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = destino,
                Pooling = false,
            }.ToString());

        origen.Open();
        copia.Open();

        origen.BackupDatabase(copia);
    }

    // La huella incluye el -wal: los cambios recientes viven ahí antes de volcarse y sin él una base con trabajo pendiente se ve idéntica a la de ayer.
    public string Huella()
    {
        using var sha = SHA256.Create();
        using var flujo = new MemoryStream();

        foreach (var archivo in new[] { _rutas.DatabasePath, _rutas.DatabasePath + "-wal" })
        {
            if (!File.Exists(archivo))
            {
                continue;
            }

            using var lector = new FileStream(
                archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            lector.CopyTo(flujo);
        }

        flujo.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(flujo));
    }

    private void Rotar(AjustesDeCopia ajustes)
    {
        foreach (var sobrante in PoliticaDeCopias.Sobrantes(Listar(ajustes), ajustes.CuantasGuardar))
        {
            try
            {
                File.Delete(sobrante.Ruta);
                File.Delete(HuellaDe(sobrante.Ruta));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger?.TechnicalError($"borrar la copia {Path.GetFileName(sobrante.Ruta)}", ex);
            }
        }
    }

    private static string HuellaDe(string copia) => copia + ".huella";

    private static void GuardarHuella(string copia, string huella)
    {
        try
        {
            File.WriteAllText(HuellaDe(copia), huella);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string LeerHuellaGuardada(string copia)
    {
        try
        {
            var archivo = HuellaDe(copia);
            return File.Exists(archivo) ? File.ReadAllText(archivo).Trim() : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
