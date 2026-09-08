namespace CafManagerConection.Terminal;

[Flags]
public enum CellFlags : byte
{
    None = 0,
    Bold = 1,
    Underline = 2,
    Inverse = 4,
    Dim = 8,
}

public struct TerminalCell
{
    public char Char;
    public short Foreground;
    public short Background;
    public CellFlags Flags;

    public const short DefaultColor = -1;

    public static TerminalCell Empty => new()
    {
        Char = ' ',
        Foreground = DefaultColor,
        Background = DefaultColor,
        Flags = CellFlags.None,
    };
}

public sealed class TerminalBuffer
{
    private TerminalCell[,] _cells;
    private readonly List<TerminalCell[]> _scrollback = [];

    public TerminalBuffer(int columns, int rows, int scrollbackLimit = 10_000)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);
        ScrollbackLimit = scrollbackLimit;
        _cells = new TerminalCell[Rows, Columns];
        _vistaHistorial = new VistaFinal(this);
        Clear();
    }

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public int ScrollbackLimit { get; set; }

    public int CursorX { get; set; }

    public int CursorY { get; set; }

    public bool CursorVisible { get; set; } = true;

    public IReadOnlyList<TerminalCell[]> Scrollback => _vistaHistorial;

    private readonly VistaFinal _vistaHistorial;

    /// <summary>Las últimas N entradas de una lista, sin copiarlas.</summary>
    private sealed class VistaFinal(TerminalBuffer dueño) : IReadOnlyList<TerminalCell[]>
    {
        private int Desde => Math.Max(0, dueño._scrollback.Count - Count);

        public int Count => Math.Min(dueño._scrollback.Count, dueño.ScrollbackLimit);

        public TerminalCell[] this[int index] => dueño._scrollback[Desde + index];

        public IEnumerator<TerminalCell[]> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    /// <summary>Descarta el historial. Lo que está en pantalla no se toca.</summary>
    public void ClearScrollback() => _scrollback.Clear();

    public int ScrollTop { get; set; }

    public int ScrollBottom { get; set; }

    public ref TerminalCell At(int row, int column) => ref _cells[row, column];

    public void Clear()
    {
        for (var y = 0; y < Rows; y++)
        {
            ClearLine(y);
        }

        CursorX = 0;
        CursorY = 0;
        ScrollTop = 0;
        ScrollBottom = Rows - 1;
    }

    public void ClearLine(int row, int fromColumn = 0, int toColumn = -1)
    {
        if (row < 0 || row >= Rows)
        {
            return;
        }

        var hasta = toColumn < 0 ? Columns - 1 : Math.Min(toColumn, Columns - 1);

        for (var x = Math.Max(0, fromColumn); x <= hasta; x++)
        {
            _cells[row, x] = TerminalCell.Empty;
        }
    }

    public void Write(char c, short foreground, short background, CellFlags flags)
    {
        if (CursorX >= Columns)
        {
            CursorX = 0;
            LineFeed();
        }

        if (CursorY < 0 || CursorY >= Rows)
        {
            return;
        }

        _cells[CursorY, CursorX] = new TerminalCell
        {
            Char = c,
            Foreground = foreground,
            Background = background,
            Flags = flags,
        };

        CursorX++;
    }

    public void LineFeed()
    {
        if (CursorY == ScrollBottom)
        {
            ScrollUp();
        }
        else if (CursorY < Rows - 1)
        {
            CursorY++;
        }
    }

    /// <summary>Desplaza la región activa una línea; la que sale por arriba va al historial.</summary>
    public void ScrollUp(int lines = 1)
    {
        for (var n = 0; n < lines; n++)
        {
            if (ScrollTop == 0 && ScrollBottom == Rows - 1)
            {
                // Se archiva hasta la última columna con contenido: con 220 columnas y TerminalCell de 8 bytes, el relleno cuesta ~1,8 KB por línea y ~18 MB con el tope de 10.000.
                var ultimaConContenido = 0;

                for (var x = Columns - 1; x > 0; x--)
                {
                    var celda = _cells[0, x];
                    var esRelleno = celda.Char is (' ' or '\0')
                        && celda.Flags == CellFlags.None
                        && celda.Foreground == TerminalCell.DefaultColor
                        && celda.Background == TerminalCell.DefaultColor;

                    if (!esRelleno)
                    {
                        ultimaConContenido = x;
                        break;
                    }
                }

                var salida = new TerminalCell[ultimaConContenido + 1];
                for (var x = 0; x < salida.Length; x++)
                {
                    salida[x] = _cells[0, x];
                }

                _scrollback.Add(salida);

                // Se recorta por tandas del 10 %: con el tope en 10.000 líneas, quitar el primer elemento en cada línea movía 10.000 elementos.
                var margen = Math.Max(ScrollbackLimit / 10, 1);

                if (_scrollback.Count > ScrollbackLimit + margen)
                {
                    _scrollback.RemoveRange(0, _scrollback.Count - ScrollbackLimit);
                }
            }

            var ancho = ScrollBottom - ScrollTop;

            if (ancho > 0)
            {
                Array.Copy(
                    _cells,
                    (ScrollTop + 1) * Columns,
                    _cells,
                    ScrollTop * Columns,
                    ancho * Columns);
            }

            ClearLine(ScrollBottom);
        }
    }

    public void ScrollDown(int lines = 1)
    {
        for (var n = 0; n < lines; n++)
        {
            for (var y = ScrollBottom; y > ScrollTop; y--)
            {
                for (var x = 0; x < Columns; x++)
                {
                    _cells[y, x] = _cells[y - 1, x];
                }
            }

            ClearLine(ScrollTop);
        }
    }

    public void InsertLines(int count)
    {
        for (var n = 0; n < count && CursorY <= ScrollBottom; n++)
        {
            for (var y = ScrollBottom; y > CursorY; y--)
            {
                for (var x = 0; x < Columns; x++)
                {
                    _cells[y, x] = _cells[y - 1, x];
                }
            }

            ClearLine(CursorY);
        }
    }

    public void DeleteLines(int count)
    {
        for (var n = 0; n < count && CursorY <= ScrollBottom; n++)
        {
            for (var y = CursorY; y < ScrollBottom; y++)
            {
                for (var x = 0; x < Columns; x++)
                {
                    _cells[y, x] = _cells[y + 1, x];
                }
            }

            ClearLine(ScrollBottom);
        }
    }

    public void DeleteChars(int count)
    {
        for (var x = CursorX; x < Columns; x++)
        {
            var origen = x + count;
            _cells[CursorY, x] = origen < Columns ? _cells[CursorY, origen] : TerminalCell.Empty;
        }
    }

    public void InsertChars(int count)
    {
        for (var x = Columns - 1; x >= CursorX; x--)
        {
            var origen = x - count;
            _cells[CursorY, x] = origen >= CursorX ? _cells[CursorY, origen] : TerminalCell.Empty;
        }
    }

    /// <summary>Cambia el tamaño conservando las líneas de arriba; al achicar se pierde lo de abajo.</summary>
    public void Resize(int columns, int rows)
    {
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);

        if (columns == Columns && rows == Rows)
        {
            return;
        }

        var nuevo = new TerminalCell[rows, columns];

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                nuevo[y, x] = y < Rows && x < Columns ? _cells[y, x] : TerminalCell.Empty;
            }
        }

        _cells = nuevo;
        Columns = columns;
        Rows = rows;
        CursorX = Math.Min(CursorX, columns - 1);
        CursorY = Math.Min(CursorY, rows - 1);
        ScrollTop = 0;
        ScrollBottom = rows - 1;
    }

    public string LineText(int row)
    {
        if (row < 0 || row >= Rows)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(Columns);
        for (var x = 0; x < Columns; x++)
        {
            sb.Append(_cells[row, x].Char);
        }

        return sb.ToString().TrimEnd();
    }
}
