namespace CafManagerConection.Monitoring;

/// <summary>Cómo se escriben los tamaños y las duraciones que muestran los paneles.</summary>
public static class Magnitudes
{
    private static readonly string[] Unidades = ["B", "KiB", "MiB", "GiB", "TiB"];

    public static string Tamano(long bytes)
    {
        double valor = bytes;
        var i = 0;

        while (valor >= 1024 && i < Unidades.Length - 1)
        {
            valor /= 1024;
            i++;
        }

        return i == 0 ? $"{bytes} B" : $"{valor:0.#} {Unidades[i]}";
    }

    public static string Duracion(TimeSpan t) => t.TotalDays >= 1
        ? $"{(int)t.TotalDays} día(s) y {t.Hours} h"
        : t.TotalHours >= 1
            ? $"{(int)t.TotalHours} h {t.Minutes} min"
            : $"{t.Minutes} min";
}
