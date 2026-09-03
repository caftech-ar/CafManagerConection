using System.Text;

namespace CafManagerConection.Terminal;

/// <summary>Emulador VT100/XTerm: consume el flujo de bytes del servidor y lo aplica sobre un <see cref="TerminalBuffer"/>.</summary>
public sealed class VtEmulator
{
    private enum State
    {
        Ground,
        Escape,
        Csi,
        Osc,
        Charset,
        Hash,
    }

    /// <summary>Juego gráfico de DEC, de <c>0x5f</c> a <c>0x7e</c>: es lo que dibuja los cuadros de ncurses.</summary>
    private const string GraficosDec = " ◆▒␉␌␍␊°±␤␋┘┐┌└┼⎺⎻─⎼⎽├┤┴┬│≤≥π≠£·";

    private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
    private readonly char[] _chars = new char[4096];
    private readonly StringBuilder _sequence = new();

    private State _state = State.Ground;
    private short _foreground = TerminalCell.DefaultColor;
    private short _background = TerminalCell.DefaultColor;
    private CellFlags _flags = CellFlags.None;

    private char _registroPorDesignar;
    private bool _g0Grafico;
    private bool _g1Grafico;
    private bool _usandoG1;

    private int _savedX;
    private int _savedY;
    private bool _savedG0Grafico;
    private bool _savedG1Grafico;
    private bool _savedUsandoG1;

    private TerminalBuffer _main;
    private TerminalBuffer? _alternate;

    public VtEmulator(TerminalBuffer buffer)
    {
        _main = buffer;
        Buffer = buffer;
    }

    public TerminalBuffer Buffer { get; private set; }

    public string? Title { get; private set; }

    /// <summary>Modo DECCKM: cambia qué secuencia mandan las flechas.</summary>
    public bool ApplicationCursorKeys { get; private set; }

    /// <summary>Modo 2004: la aplicación remota quiere que el texto pegado llegue marcado (FR-030e).</summary>
    public bool BracketedPaste { get; private set; }

    /// <summary>DECKPAM/DECKPNM: qué secuencia manda el teclado numérico.</summary>
    public bool TecladoNumericoEnModoAplicacion { get; private set; }

    /// <summary>Modo ?1000: reporte de clic y suelte del mouse.</summary>
    public bool MouseTrackingNormal { get; private set; }

    /// <summary>Modo ?1002: como <see cref="MouseTrackingNormal"/> más el arrastre con botón apretado.</summary>
    public bool MouseTrackingButtonEvent { get; private set; }

    /// <summary>Modo ?1006: coordenadas del reporte de mouse en formato SGR, sin límite de columnas.</summary>
    public bool MouseTrackingSgr { get; private set; }

    public event EventHandler? Updated;

    public event EventHandler<byte[]>? ResponseRequested;

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return;
        }

        // Se procesa por tramos: el transporte lee de a 8 KB contra 4 K de búfer de caracteres y GetChars lanzaba ArgumentException.
        while (!data.IsEmpty)
        {
            var tramo = Math.Min(data.Length, _chars.Length);
            var count = _decoder.GetChars(data[..tramo], _chars, flush: false);

            for (var i = 0; i < count; i++)
            {
                Process(_chars[i]);
            }

            data = data[tramo..];
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    private void Process(char c)
    {
        switch (_state)
        {
            case State.Ground:
                Ground(c);
                break;

            case State.Escape:
                Escape(c);
                break;

            case State.Csi:
                Csi(c);
                break;

            case State.Osc:
                Osc(c);
                break;

            case State.Charset:
                DesignarJuego(c);
                break;

            case State.Hash:
                Hash(c);
                break;
        }
    }

    private void Ground(char c)
    {
        switch (c)
        {
            case '\x1b':
                _sequence.Clear();
                _state = State.Escape;
                break;

            case '\r':
                Buffer.CursorX = 0;
                break;

            case '\n':
            case '\v':
            case '\f':
                Buffer.LineFeed();
                break;

            case '\b':
                Buffer.CursorX = Math.Max(0, Buffer.CursorX - 1);
                break;

            case '\t':
                Buffer.CursorX = Math.Min(Buffer.Columns - 1, ((Buffer.CursorX / 8) + 1) * 8);
                break;

            case '\a':
                break;

            case '\x0e':
                _usandoG1 = true;
                break;

            case '\x0f':
                _usandoG1 = false;
                break;

            default:
                if (!char.IsControl(c))
                {
                    Buffer.Write(Traducir(c), _foreground, _background, _flags);
                }

                break;
        }
    }

    private char Traducir(char c) =>
        (_usandoG1 ? _g1Grafico : _g0Grafico) && c is >= '\x5f' and <= '\x7e'
            ? GraficosDec[c - '\x5f']
            : c;

    private void DesignarJuego(char final)
    {
        var grafico = final == '0';

        switch (_registroPorDesignar)
        {
            case '(': _g0Grafico = grafico; break;
            case ')': _g1Grafico = grafico; break;
        }

        _state = State.Ground;
    }

    /// <summary>DECALN (<c>ESC # 8</c>): llena la pantalla de "E" para alinear el monitor. Cualquier otro final se descarta.</summary>
    private void Hash(char final)
    {
        if (final == '8')
        {
            for (var y = 0; y < Buffer.Rows; y++)
            {
                for (var x = 0; x < Buffer.Columns; x++)
                {
                    Buffer.At(y, x) = new TerminalCell
                    {
                        Char = 'E',
                        Foreground = TerminalCell.DefaultColor,
                        Background = TerminalCell.DefaultColor,
                        Flags = CellFlags.None,
                    };
                }
            }

            Buffer.CursorX = 0;
            Buffer.CursorY = 0;
        }

        _state = State.Ground;
    }

    private void Escape(char c)
    {
        switch (c)
        {
            case '[':
                _sequence.Clear();
                _state = State.Csi;
                break;

            case ']':
                _sequence.Clear();
                _state = State.Osc;
                break;

            case '(' or ')' or '*' or '+':
                _registroPorDesignar = c;
                _state = State.Charset;
                break;

            case '#':
                _state = State.Hash;
                break;

            case '=':
                TecladoNumericoEnModoAplicacion = true;
                _state = State.Ground;
                break;

            case '>':
                TecladoNumericoEnModoAplicacion = false;
                _state = State.Ground;
                break;

            case '7':
                _savedX = Buffer.CursorX;
                _savedY = Buffer.CursorY;
                _savedG0Grafico = _g0Grafico;
                _savedG1Grafico = _g1Grafico;
                _savedUsandoG1 = _usandoG1;
                _state = State.Ground;
                break;

            case '8':
                Buffer.CursorX = _savedX;
                Buffer.CursorY = _savedY;
                _g0Grafico = _savedG0Grafico;
                _g1Grafico = _savedG1Grafico;
                _usandoG1 = _savedUsandoG1;
                _state = State.Ground;
                break;

            case 'M':
                if (Buffer.CursorY == Buffer.ScrollTop)
                {
                    Buffer.ScrollDown();
                }
                else
                {
                    Buffer.CursorY = Math.Max(0, Buffer.CursorY - 1);
                }

                _state = State.Ground;
                break;

            case 'D':
                Buffer.LineFeed();
                _state = State.Ground;
                break;

            case 'c':
                Reset();
                _state = State.Ground;
                break;

            default:
                _state = State.Ground;
                break;
        }
    }

    private void Csi(char c)
    {
        if (c is >= '\x20' and <= '\x3f')
        {
            _sequence.Append(c);
            return;
        }

        var texto = _sequence.ToString();
        var privado = texto.StartsWith('?');
        var cuerpo = privado ? texto[1..] : texto;

        var args = cuerpo
            .Split(';', StringSplitOptions.None)
            .Select(p => int.TryParse(p, out var v) ? v : 0)
            .ToArray();

        int Arg(int index, int fallback = 1)
        {
            if (index >= args.Length)
            {
                return fallback;
            }

            return args[index] == 0 ? fallback : args[index];
        }

        switch (c)
        {
            case 'A': Buffer.CursorY = Math.Max(Buffer.ScrollTop, Buffer.CursorY - Arg(0)); break;
            case 'B': Buffer.CursorY = Math.Min(Buffer.ScrollBottom, Buffer.CursorY + Arg(0)); break;
            case 'C': Buffer.CursorX = Math.Min(Buffer.Columns - 1, Buffer.CursorX + Arg(0)); break;
            case 'D': Buffer.CursorX = Math.Max(0, Buffer.CursorX - Arg(0)); break;

            case 'E':
                Buffer.CursorX = 0;
                Buffer.CursorY = Math.Min(Buffer.Rows - 1, Buffer.CursorY + Arg(0));
                break;

            case 'F':
                Buffer.CursorX = 0;
                Buffer.CursorY = Math.Max(0, Buffer.CursorY - Arg(0));
                break;

            case 'G' or '`':
                Buffer.CursorX = Math.Clamp(Arg(0) - 1, 0, Buffer.Columns - 1);
                break;

            case 'd':
                Buffer.CursorY = Math.Clamp(Arg(0) - 1, 0, Buffer.Rows - 1);
                break;

            case 'H' or 'f':
                Buffer.CursorY = Math.Clamp(Arg(0) - 1, 0, Buffer.Rows - 1);
                Buffer.CursorX = Math.Clamp(Arg(1) - 1, 0, Buffer.Columns - 1);
                break;

            case 'J': EraseInDisplay(args.Length > 0 ? args[0] : 0); break;
            case 'K': EraseInLine(args.Length > 0 ? args[0] : 0); break;

            case 'L': Buffer.InsertLines(Arg(0)); break;
            case 'M': Buffer.DeleteLines(Arg(0)); break;
            case 'P': Buffer.DeleteChars(Arg(0)); break;
            case '@': Buffer.InsertChars(Arg(0)); break;

            case 'S': Buffer.ScrollUp(Arg(0)); break;
            case 'T': Buffer.ScrollDown(Arg(0)); break;

            case 'X':
                Buffer.ClearLine(Buffer.CursorY, Buffer.CursorX, Buffer.CursorX + Arg(0) - 1);
                break;

            case 'm': ApplyGraphics(args); break;

            case 'r':
                Buffer.ScrollTop = Math.Clamp(Arg(0) - 1, 0, Buffer.Rows - 1);
                Buffer.ScrollBottom = Math.Clamp(Arg(1, Buffer.Rows) - 1, 0, Buffer.Rows - 1);
                Buffer.CursorX = 0;
                Buffer.CursorY = Buffer.ScrollTop;
                break;

            case 'h': SetMode(args, privado, true); break;
            case 'l': SetMode(args, privado, false); break;

            case 's':
                _savedX = Buffer.CursorX;
                _savedY = Buffer.CursorY;
                break;

            case 'u':
                Buffer.CursorX = _savedX;
                Buffer.CursorY = _savedY;
                break;

            case 'n' when args.Length > 0 && args[0] == 6:
                Respond($"\x1b[{Buffer.CursorY + 1};{Buffer.CursorX + 1}R");
                break;

            case 'c':
                Respond("\x1b[?6c");
                break;
        }

        _state = State.Ground;
    }

    private void SetMode(int[] args, bool privado, bool activar)
    {
        if (!privado)
        {
            return;
        }

        foreach (var modo in args)
        {
            switch (modo)
            {
                case 1:
                    ApplicationCursorKeys = activar;
                    break;

                case 25:
                    Buffer.CursorVisible = activar;
                    break;

                case 1049 or 47 or 1047:
                    UseAlternateScreen(activar);
                    break;

                case 2004:
                    BracketedPaste = activar;
                    break;

                case 1000:
                    MouseTrackingNormal = activar;
                    break;

                case 1002:
                    MouseTrackingButtonEvent = activar;
                    break;

                case 1006:
                    MouseTrackingSgr = activar;
                    break;
            }
        }
    }

    private void UseAlternateScreen(bool usar)
    {
        if (usar)
        {
            if (_alternate is not null)
            {
                return;
            }

            _savedX = Buffer.CursorX;
            _savedY = Buffer.CursorY;
            _alternate = new TerminalBuffer(Buffer.Columns, Buffer.Rows, scrollbackLimit: 0);
            Buffer = _alternate;
        }
        else
        {
            if (_alternate is null)
            {
                return;
            }

            _alternate = null;
            Buffer = _main;
            Buffer.CursorX = _savedX;
            Buffer.CursorY = _savedY;
        }
    }

    private void EraseInDisplay(int mode)
    {
        switch (mode)
        {
            case 0:
                Buffer.ClearLine(Buffer.CursorY, Buffer.CursorX);
                for (var y = Buffer.CursorY + 1; y < Buffer.Rows; y++)
                {
                    Buffer.ClearLine(y);
                }

                break;

            case 1:
                Buffer.ClearLine(Buffer.CursorY, 0, Buffer.CursorX);
                for (var y = 0; y < Buffer.CursorY; y++)
                {
                    Buffer.ClearLine(y);
                }

                break;

            default:
                for (var y = 0; y < Buffer.Rows; y++)
                {
                    Buffer.ClearLine(y);
                }

                break;
        }
    }

    private void EraseInLine(int mode)
    {
        switch (mode)
        {
            case 0: Buffer.ClearLine(Buffer.CursorY, Buffer.CursorX); break;
            case 1: Buffer.ClearLine(Buffer.CursorY, 0, Buffer.CursorX); break;
            default: Buffer.ClearLine(Buffer.CursorY); break;
        }
    }

    /// <summary>Atributos gráficos (SGR): colores y estilos.</summary>
    private void ApplyGraphics(int[] args)
    {
        if (args.Length == 0)
        {
            args = [0];
        }

        for (var i = 0; i < args.Length; i++)
        {
            var code = args[i];

            switch (code)
            {
                case 0:
                    _foreground = TerminalCell.DefaultColor;
                    _background = TerminalCell.DefaultColor;
                    _flags = CellFlags.None;
                    break;

                case 1: _flags |= CellFlags.Bold; break;
                case 2: _flags |= CellFlags.Dim; break;
                case 4: _flags |= CellFlags.Underline; break;
                case 7: _flags |= CellFlags.Inverse; break;
                case 22: _flags &= ~(CellFlags.Bold | CellFlags.Dim); break;
                case 24: _flags &= ~CellFlags.Underline; break;
                case 27: _flags &= ~CellFlags.Inverse; break;

                case >= 30 and <= 37: _foreground = (short)(code - 30); break;
                case >= 40 and <= 47: _background = (short)(code - 40); break;
                case >= 90 and <= 97: _foreground = (short)(code - 90 + 8); break;
                case >= 100 and <= 107: _background = (short)(code - 100 + 8); break;

                case 39: _foreground = TerminalCell.DefaultColor; break;
                case 49: _background = TerminalCell.DefaultColor; break;

                case 38 or 48:
                    {
                        var esFrente = code == 38;
                        if (i + 1 < args.Length && args[i + 1] == 5 && i + 2 < args.Length)
                        {
                            var indice = (short)args[i + 2];
                            if (esFrente) { _foreground = indice; } else { _background = indice; }
                            i += 2;
                        }
                        else if (i + 4 < args.Length && args[i + 1] == 2)
                        {
                            var rgb = (short)(256 + ((args[i + 2] >> 3) << 10 |
                                                     (args[i + 3] >> 3) << 5 |
                                                     (args[i + 4] >> 3)));
                            if (esFrente) { _foreground = rgb; } else { _background = rgb; }
                            i += 4;
                        }

                        break;
                    }
            }
        }
    }

    private void Osc(char c)
    {
        if (c == '\a' || c == '\x1b')
        {
            var texto = _sequence.ToString();
            var corte = texto.IndexOf(';', StringComparison.Ordinal);

            if (corte >= 0 && int.TryParse(texto[..corte], out var comando) &&
                comando is 0 or 2)
            {
                Title = texto[(corte + 1)..];
            }

            _sequence.Clear();

            // El terminador ST son 2 bytes (ESC y barra): volviendo a Ground, la barra caía como texto y se imprimía suelta.
            _state = c == '\x1b' ? State.Escape : State.Ground;
            return;
        }

        _sequence.Append(c);
    }

    public void Reset()
    {
        _foreground = TerminalCell.DefaultColor;
        _background = TerminalCell.DefaultColor;
        _flags = CellFlags.None;
        ApplicationCursorKeys = false;
        BracketedPaste = false;
        TecladoNumericoEnModoAplicacion = false;
        MouseTrackingNormal = false;
        MouseTrackingButtonEvent = false;
        MouseTrackingSgr = false;
        _g0Grafico = false;
        _g1Grafico = false;
        _usandoG1 = false;
        _savedG0Grafico = false;
        _savedG1Grafico = false;
        _savedUsandoG1 = false;
        _alternate = null;
        Buffer = _main;
        Buffer.Clear();
    }

    public void Rebind(TerminalBuffer buffer)
    {
        _main = buffer;
        _alternate = null;
        Buffer = buffer;
    }

    private void Respond(string text) =>
        ResponseRequested?.Invoke(this, Encoding.ASCII.GetBytes(text));
}
