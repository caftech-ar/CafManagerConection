namespace CafManagerConection.Platform;

public enum GravedadDeLinea
{
    Normal,
    Advertencia,
    Error,
}

/// <summary>Clasifica las líneas que no traen color propio; sólo marcas en inglés y de syslog (FR-100f).</summary>
public static class NivelDeLinea
{
    // Los bordes importan: una ruta como /var/log/error.log aparece en líneas perfectamente normales.
    private static readonly string[] Errores =
    [
        "error", "err", "fatal", "critical", "crit", "alert", "emerg", "panic", "severe",
        "exception", "failed", "failure",
    ];

    private static readonly string[] Advertencias = ["warn", "warning", "notice", "deprecated"];

    public static GravedadDeLinea De(string? linea)
    {
        if (string.IsNullOrWhiteSpace(linea))
        {
            return GravedadDeLinea.Normal;
        }

        // El error gana sobre la advertencia: lo que hay que ver primero es el error.
        return Contiene(linea, Errores) ? GravedadDeLinea.Error
            : Contiene(linea, Advertencias) ? GravedadDeLinea.Advertencia
            : GravedadDeLinea.Normal;
    }

    /// <summary>Si la marca aparece como palabra; un archivo llamado <c>error.log</c> queda marcado como error.</summary>
    private static bool Contiene(string linea, string[] marcas)
    {
        foreach (var marca in marcas)
        {
            var desde = 0;

            while (desde < linea.Length)
            {
                var i = linea.IndexOf(marca, desde, StringComparison.OrdinalIgnoreCase);

                if (i < 0)
                {
                    break;
                }

                if (BordeIzquierdo(linea, i) && BordeDerecho(linea, i + marca.Length))
                {
                    return true;
                }

                desde = i + 1;
            }
        }

        return false;
    }

    // El cambio de minúscula a mayúscula también vale: DeprecationWarning y ValueError son marcas de verdad.
    private static bool BordeIzquierdo(string linea, int i)
    {
        if (i == 0)
        {
            return true;
        }

        var anterior = linea[i - 1];

        return !char.IsLetterOrDigit(anterior)
               || (!char.IsUpper(anterior) && char.IsUpper(linea[i]));
    }

    private static bool BordeDerecho(string linea, int i) =>
        i >= linea.Length || !char.IsLetterOrDigit(linea[i]) || char.IsUpper(linea[i]);
}
