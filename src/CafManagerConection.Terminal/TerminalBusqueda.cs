namespace CafManagerConection.Terminal;

public readonly record struct TerminalCoincidencia(
    TerminalCell[]? LineaHistorial, int Fila, int Columna, int Longitud)
{
    public static TerminalCoincidencia EnHistorial(TerminalCell[] linea, int columna, int longitud) =>
        new(linea, -1, columna, longitud);

    public static TerminalCoincidencia EnPantalla(int fila, int columna, int longitud) =>
        new(null, fila, columna, longitud);

    /// <summary>Si la coincidencia es del búfer vivo y no del historial.</summary>
    public bool EsPantalla => LineaHistorial is null;
}

public static class BuscadorDeTerminal
{
    public static IReadOnlyList<TerminalCoincidencia> Buscar(TerminalBuffer buffer, string texto)
    {
        var resultado = new List<TerminalCoincidencia>();

        if (string.IsNullOrEmpty(texto))
        {
            return resultado;
        }

        foreach (var linea in buffer.Scrollback)
        {
            BuscarEnLineaDeHistorial(linea, texto, resultado);
        }

        for (var fila = 0; fila < buffer.Rows; fila++)
        {
            BuscarEnFilaDePantalla(buffer, fila, texto, resultado);
        }

        return resultado;
    }

    private static void BuscarEnLineaDeHistorial(
        TerminalCell[] linea, string texto, List<TerminalCoincidencia> resultado)
    {
        var cadena = ATexto(linea);
        var desde = 0;

        while (true)
        {
            var indice = cadena.IndexOf(texto, desde, StringComparison.OrdinalIgnoreCase);

            if (indice < 0)
            {
                break;
            }

            resultado.Add(TerminalCoincidencia.EnHistorial(linea, indice, texto.Length));
            desde = indice + texto.Length;
        }
    }

    private static void BuscarEnFilaDePantalla(
        TerminalBuffer buffer, int fila, string texto, List<TerminalCoincidencia> resultado)
    {
        var cadena = buffer.LineText(fila);
        var desde = 0;

        while (true)
        {
            var indice = cadena.IndexOf(texto, desde, StringComparison.OrdinalIgnoreCase);

            if (indice < 0)
            {
                break;
            }

            resultado.Add(TerminalCoincidencia.EnPantalla(fila, indice, texto.Length));
            desde = indice + texto.Length;
        }
    }

    private static string ATexto(TerminalCell[] linea)
    {
        var sb = new System.Text.StringBuilder(linea.Length);

        foreach (var celda in linea)
        {
            sb.Append(celda.Char);
        }

        return sb.ToString();
    }
}

public sealed class NavegadorDeBusqueda
{
    private IReadOnlyList<TerminalCoincidencia> _coincidencias = [];
    private int _actual = -1;

    public IReadOnlyList<TerminalCoincidencia> Coincidencias => _coincidencias;

    public int Total => _coincidencias.Count;

    public int IndiceActual => _actual;

    /// <summary>Posición de la coincidencia actual contada desde uno, o 0 si no hay ninguna.</summary>
    public int Posicion => _actual + 1;

    public TerminalCoincidencia? Actual =>
        _actual >= 0 && _actual < _coincidencias.Count ? _coincidencias[_actual] : null;

    public void Establecer(IReadOnlyList<TerminalCoincidencia> coincidencias)
    {
        var previa = Actual;
        _coincidencias = coincidencias;

        _actual = previa is { } p && IndiceDe(p) is var indice && indice >= 0
            ? indice
            : (_coincidencias.Count > 0 ? 0 : -1);
    }

    private int IndiceDe(TerminalCoincidencia buscada)
    {
        for (var i = 0; i < _coincidencias.Count; i++)
        {
            if (_coincidencias[i].Equals(buscada))
            {
                return i;
            }
        }

        return -1;
    }

    public TerminalCoincidencia? Siguiente()
    {
        if (_coincidencias.Count == 0)
        {
            _actual = -1;
            return null;
        }

        _actual = (_actual + 1) % _coincidencias.Count;
        return _coincidencias[_actual];
    }

    public TerminalCoincidencia? Anterior()
    {
        if (_coincidencias.Count == 0)
        {
            _actual = -1;
            return null;
        }

        _actual = _actual <= 0 ? _coincidencias.Count - 1 : _actual - 1;
        return _coincidencias[_actual];
    }
}
