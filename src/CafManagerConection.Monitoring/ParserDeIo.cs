namespace CafManagerConection.Monitoring;

public readonly record struct EntradaSalidaDeProceso(long? BytesLeidos, long? BytesEscritos)
{
    public static EntradaSalidaDeProceso Desconocida => new(null, null);

    public bool EsConocida => BytesLeidos is not null || BytesEscritos is not null;
}

// /proc/<pid>/io de un proceso ajeno no se lee sin privilegios: la ausencia es normal y la fila sale igual, sin E/S.
public static class ParserDeIo
{
    public static EntradaSalidaDeProceso Parse(string bloque)
    {
        long? leidos = null;
        long? escritos = null;

        foreach (var linea in bloque.ReplaceLineEndings("\n").Split('\n'))
        {
            var dosPuntos = linea.IndexOf(':');

            if (dosPuntos < 0)
            {
                continue;
            }

            var clave = linea.AsSpan(0, dosPuntos).Trim();
            var valor = Numero(linea.AsSpan(dosPuntos + 1));

            if (clave.SequenceEqual("read_bytes"))
            {
                leidos = valor;
            }
            else if (clave.SequenceEqual("write_bytes"))
            {
                escritos = valor;
            }
        }

        return new EntradaSalidaDeProceso(leidos, escritos);
    }

    private static long? Numero(ReadOnlySpan<char> texto) =>
        long.TryParse(texto.Trim(), out var valor) && valor >= 0 ? valor : null;
}
