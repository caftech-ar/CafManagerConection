namespace CafManagerConection.App.Services;

/// <summary>Lo que la barra inferior dice de la sesión activa: cuánto tardó en abrir, a qué hora abrió y hace cuánto.</summary>
public static class TiempoDeSesion
{
    public static string Componer(TimeSpan? tardo, DateTimeOffset? abiertaA, DateTimeOffset ahora)
    {
        if (tardo is null || abiertaA is null)
        {
            return string.Empty;
        }

        return $"abrió en {Apertura(tardo.Value)} · {abiertaA.Value:HH:mm:ss} · "
               + $"hace {Antiguedad(ahora - abiertaA.Value)}";
    }

    /// <summary>Milisegundos hasta el segundo, y de ahí en adelante segundos: «1543 ms» se lee peor que «1,5 s».</summary>
    public static string Apertura(TimeSpan tardo)
    {
        var ms = tardo.TotalMilliseconds;

        if (ms < 0)
        {
            return "0 ms";
        }

        return ms < 1000
            ? $"{Math.Round(ms)} ms"
            : $"{ms / 1000:0.0} s".Replace(".", ",", StringComparison.Ordinal);
    }

    public static string Antiguedad(TimeSpan desde)
    {
        if (desde < TimeSpan.Zero)
        {
            desde = TimeSpan.Zero;
        }

        if (desde.TotalSeconds < 60)
        {
            return "menos de un minuto";
        }

        if (desde.TotalMinutes < 60)
        {
            var minutos = (int)desde.TotalMinutes;
            return minutos == 1 ? "1 minuto" : $"{minutos} minutos";
        }

        var horas = (int)desde.TotalHours;
        var resto = (int)(desde.TotalMinutes - (horas * 60));

        var texto = horas == 1 ? "1 hora" : $"{horas} horas";

        return resto == 0 ? texto : $"{texto} y {(resto == 1 ? "1 minuto" : $"{resto} minutos")}";
    }
}
