using CafManagerConection.Infrastructure.Actualizaciones;
using Microsoft.Win32;

namespace CafManagerConection.App.Services;

public enum TipoDeInstalador
{
    Liviano,
    Completo,
}

/// <summary>Un instalador ofrecido por una publicación, con lo que hace falta para elegirlo.</summary>
public sealed record InstaladorDisponible(
    ActivoDeRelease Activo, TipoDeInstalador Tipo, bool EsElInstalado)
{
    public string Nombre => Tipo == TipoDeInstalador.Liviano ? "Liviano" : "Completo";

    public string Condicion => Tipo == TipoDeInstalador.Liviano
        ? "precisa .NET instalado en la máquina"
        : "incluye todo lo que precisa";

    /// <summary>El tamaño en megabytes, o <c>null</c> si la publicación no lo informa.</summary>
    public string? Tamano => Activo.Bytes is { } bytes && bytes > 0
        ? $"{bytes / (1024.0 * 1024):0.#} MB"
        : null;
}

/// <summary>Cuál de los archivos adjuntos a una release es el instalador de Windows.</summary>
public static class SelectorDeInstalador
{
    // La marca la escribe installer/CafManagerConection.nsi al instalar, elevado; acá sólo se lee.
    private const string ClaveDeLaInstalacion = @"Software\CafManagerConection";
    private const string ValorDelTipo = "TipoDeInstalador";

    public static ActivoDeRelease? Elegir(IReadOnlyList<ActivoDeRelease> activos) =>
        Elegir(activos, TipoInstalado());

    public static ActivoDeRelease? Elegir(
        IReadOnlyList<ActivoDeRelease> activos, TipoDeInstalador? preferido)
    {
        ArgumentNullException.ThrowIfNull(activos);

        var instaladores = activos.Where(EsInstalador).ToList();

        return DelTipo(instaladores, preferido ?? TipoDeInstalador.Liviano)
               ?? instaladores.FirstOrDefault();
    }

    /// <summary>Los instaladores que publica una release, con su tipo y cuál coincide con el instalado.</summary>
    /// <param name="activos">Los archivos adjuntos a la release.</param>
    public static IReadOnlyList<InstaladorDisponible> Disponibles(
        IReadOnlyList<ActivoDeRelease> activos) => Disponibles(activos, TipoInstalado());

    /// <summary>Los instaladores que publica una release, marcando los del tipo indicado.</summary>
    /// <param name="activos">Los archivos adjuntos a la release.</param>
    /// <param name="instalado">El tipo instalado en la máquina, o <c>null</c> si no se sabe.</param>
    public static IReadOnlyList<InstaladorDisponible> Disponibles(
        IReadOnlyList<ActivoDeRelease> activos, TipoDeInstalador? instalado)
    {
        ArgumentNullException.ThrowIfNull(activos);

        return
        [
            .. activos
                .Where(EsInstalador)
                .Select(a => new InstaladorDisponible(a, TipoDe(a), TipoDe(a) == instalado)),
        ];
    }

    public static TipoDeInstalador? TipoInstalado()
    {
        try
        {
            using var raiz = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var clave = raiz.OpenSubKey(ClaveDeLaInstalacion);

            return InterpretarMarca(clave?.GetValue(ValorDelTipo) as string);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static TipoDeInstalador? InterpretarMarca(string? marca) => marca?.Trim().ToLowerInvariant() switch
    {
        "liviano" => TipoDeInstalador.Liviano,
        "completo" => TipoDeInstalador.Completo,
        _ => null,
    };

    private static ActivoDeRelease? DelTipo(
        IEnumerable<ActivoDeRelease> instaladores, TipoDeInstalador tipo) =>
        instaladores.FirstOrDefault(a => TipoDe(a) == tipo);

    private static TipoDeInstalador TipoDe(ActivoDeRelease activo) =>
        activo.Nombre.Contains("completo", StringComparison.OrdinalIgnoreCase)
            ? TipoDeInstalador.Completo
            : TipoDeInstalador.Liviano;

    private static bool EsInstalador(ActivoDeRelease activo) =>
        activo.Nombre.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && activo.Nombre.Contains("setup", StringComparison.OrdinalIgnoreCase);
}
