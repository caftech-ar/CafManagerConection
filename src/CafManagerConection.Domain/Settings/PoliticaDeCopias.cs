using System.Globalization;

namespace CafManagerConection.Domain.Settings;

public sealed record CopiaDeSeguridad(
    string Ruta,
    DateTimeOffset Momento,
    long Bytes,
    string Huella);

public sealed record AjustesDeCopia(
    bool Activas = true,
    string Carpeta = "",
    int CuantasGuardar = 10)
{
    public const int MinimoAGuardar = 1;

    public const int MaximoAGuardar = 100;

    public static AjustesDeCopia Default { get; } = new();

    public AjustesDeCopia Normalizados() => this with
    {
        CuantasGuardar = Math.Clamp(CuantasGuardar, MinimoAGuardar, MaximoAGuardar),
        Carpeta = (Carpeta ?? string.Empty).Trim(),
    };
}

public static class PoliticaDeCopias
{
    public const string FormatoDeSello = "yyyyMMdd-HHmmss";

    public static bool HayQueCopiar(
        IReadOnlyList<CopiaDeSeguridad> existentes,
        string huellaActual,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(existentes);

        if (existentes.Count == 0)
        {
            return true;
        }

        var ultima = existentes.MaxBy(c => c.Momento)!;

        if (ultima.Momento.LocalDateTime.Date == ahora.LocalDateTime.Date)
        {
            return false;
        }

        return !string.Equals(ultima.Huella, huellaActual, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<CopiaDeSeguridad> Sobrantes(
        IReadOnlyList<CopiaDeSeguridad> existentes, int cuantasGuardar)
    {
        ArgumentNullException.ThrowIfNull(existentes);

        var tope = Math.Clamp(
            cuantasGuardar, AjustesDeCopia.MinimoAGuardar, AjustesDeCopia.MaximoAGuardar);

        return existentes.Count <= tope
            ? []
            : [.. existentes.OrderByDescending(c => c.Momento).Skip(tope)];
    }

    public static string NombreDeArchivo(DateTimeOffset momento) =>
        $"cmc-{momento.ToLocalTime().ToString(FormatoDeSello, CultureInfo.InvariantCulture)}.db";

    public static DateTimeOffset? MomentoDe(string nombreDeArchivo)
    {
        var nombre = Path.GetFileNameWithoutExtension(nombreDeArchivo ?? string.Empty);

        if (!nombre.StartsWith("cmc-", StringComparison.Ordinal))
        {
            return null;
        }

        return DateTime.TryParseExact(
            nombre[4..],
            FormatoDeSello,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fecha)
            ? new DateTimeOffset(fecha, TimeZoneInfo.Local.GetUtcOffset(fecha))
            : null;
    }
}
