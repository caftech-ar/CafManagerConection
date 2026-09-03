using CafManagerConection.Infrastructure.Actualizaciones;
using Microsoft.Win32;

namespace CafManagerConection.App.Services;

public enum TipoDeInstalador
{
    Liviano,
    Completo,
}

/// <summary>Cuál de los archivos adjuntos a una release es el instalador de Windows (FR-161).</summary>
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
