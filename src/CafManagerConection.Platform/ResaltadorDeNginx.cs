namespace CafManagerConection.Platform;

public enum TipoDeTramo
{
    Normal,

    Directiva,

    Bloque,

    Cadena,

    Numero,

    Variable,

    Comentario,

    Puntuacion,
}

public readonly record struct TramoResaltado(int Desde, int Largo, TipoDeTramo Tipo);

/// <summary>Parte un archivo de nginx en tramos para colorearlo (FR-101a).</summary>
// Los tramos cubren el texto completo, en orden y sin superponerse: concatenarlos devuelve el archivo tal cual (FR-101b).
public static class ResaltadorDeNginx
{
    private static readonly HashSet<string> Bloques = new(StringComparer.Ordinal)
    {
        "http", "server", "location", "upstream", "events", "mail", "stream", "map", "if",
        "types", "limit_except", "geo", "split_clients",
    };

    public static IReadOnlyList<TramoResaltado> Analizar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return [];
        }

        var tramos = new List<TramoResaltado>();

        // La primera palabra de la sentencia decide: proxy_pass al principio es directiva, después de otra palabra es un valor.
        var esPrimera = true;
        var i = 0;

        while (i < texto.Length)
        {
            var c = texto[i];

            if (c == '#')
            {
                var fin = texto.IndexOf('\n', i);
                var largo = (fin < 0 ? texto.Length : fin) - i;
                tramos.Add(new TramoResaltado(i, largo, TipoDeTramo.Comentario));
                i += largo;
                continue;
            }

            if (c is '"' or '\'')
            {
                var largo = LargoDeCadena(texto, i, c);
                tramos.Add(new TramoResaltado(i, largo, TipoDeTramo.Cadena));
                i += largo;
                esPrimera = false;
                continue;
            }

            if (c is '{' or '}' or ';')
            {
                tramos.Add(new TramoResaltado(i, 1, TipoDeTramo.Puntuacion));
                i++;

                esPrimera = true;
                continue;
            }

            if (c == '$')
            {
                var largo = 1;
                while (i + largo < texto.Length && EsDeNombre(texto[i + largo]))
                {
                    largo++;
                }

                tramos.Add(new TramoResaltado(i, largo, TipoDeTramo.Variable));
                i += largo;
                esPrimera = false;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                var largo = 1;
                while (i + largo < texto.Length && char.IsWhiteSpace(texto[i + largo]))
                {
                    largo++;
                }

                tramos.Add(new TramoResaltado(i, largo, TipoDeTramo.Normal));
                i += largo;
                continue;
            }

            var fin2 = i;
            while (fin2 < texto.Length
                   && !char.IsWhiteSpace(texto[fin2])
                   && texto[fin2] is not ('{' or '}' or ';' or '#'))
            {
                fin2++;
            }

            var palabra = texto[i..fin2];
            var tipo = Clasificar(palabra, esPrimera);

            tramos.Add(new TramoResaltado(i, palabra.Length, tipo));
            i = fin2;
            esPrimera = false;
        }

        return tramos;
    }

    public static string Reconstruir(string texto, IReadOnlyList<TramoResaltado> tramos)
    {
        var sb = new System.Text.StringBuilder(texto.Length);

        foreach (var t in tramos)
        {
            sb.Append(texto.AsSpan(t.Desde, t.Largo));
        }

        return sb.ToString();
    }

    private static TipoDeTramo Clasificar(string palabra, bool esPrimera)
    {
        if (esPrimera)
        {
            return Bloques.Contains(palabra) ? TipoDeTramo.Bloque : TipoDeTramo.Directiva;
        }

        return EsNumero(palabra) ? TipoDeTramo.Numero : TipoDeTramo.Normal;
    }

    private static bool EsNumero(string palabra)
    {
        if (palabra.Length == 0 || !char.IsAsciiDigit(palabra[0]))
        {
            return false;
        }

        var i = 0;
        while (i < palabra.Length && char.IsAsciiDigit(palabra[i]))
        {
            i++;
        }

        return i == palabra.Length
               || (i == palabra.Length - 1 && char.IsAsciiLetter(palabra[i]));
    }

    private static bool EsDeNombre(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static int LargoDeCadena(string texto, int desde, char comilla)
    {
        var i = desde + 1;

        while (i < texto.Length && texto[i] != comilla && texto[i] != '\n')
        {
            if (texto[i] == '\\' && i + 1 < texto.Length)
            {
                i++;
            }

            i++;
        }

        return i < texto.Length && texto[i] == comilla
            ? i - desde + 1
            : i - desde;
    }
}
