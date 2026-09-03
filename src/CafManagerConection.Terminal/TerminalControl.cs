using System.Runtime.Versioning;
using System.Text;

namespace CafManagerConection.Terminal;

public readonly record struct TerminalSize(int Columns, int Rows);

/// <summary>Control de terminal: dibuja el búfer y traduce el teclado y el mouse.</summary>
[SupportedOSPlatform("windows")]
public sealed class TerminalControl : Control
{
    private readonly VtEmulator _emulator;
    private TerminalBuffer _buffer;
    private TerminalPalette _palette = TerminalPalette.Dark;

    private Font _font;
    private int _cellWidth;
    private int _cellHeight;

    private int _scrollOffset;
    private bool _cursorOn = true;
    private readonly System.Windows.Forms.Timer _blink;

    private Point? _selectionStart;
    private Point? _selectionEnd;

    private bool _seleccionRectangular;

    private bool _seleccionPorClics;

    internal Action<string> EscribirEnPortapapeles = texto => Clipboard.SetText(texto);

    internal Func<string?> LeerDelPortapapeles =
        () => Clipboard.ContainsText() ? Clipboard.GetText() : null;

    private readonly NavegadorDeBusqueda _busqueda = new();
    private string _textoBuscado = string.Empty;

    private readonly Dictionary<TerminalCell[], List<int>> _indicesPorLineaHistorial = [];

    private readonly Dictionary<int, List<int>> _indicesPorFilaPantalla = [];

    private ContextMenuStrip? _menu;

    public TerminalControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.Selectable, true);

        TabStop = true;
        _font = new Font("Cascadia Mono", 10f);
        _buffer = new TerminalBuffer(80, 24);
        _emulator = new VtEmulator(_buffer);
        _emulator.Updated += (_, _) => SafeInvalidate();
        _emulator.ResponseRequested += (_, data) => UserInput?.Invoke(this, data);

        MeasureCell();

        _blink = new System.Windows.Forms.Timer { Interval = 530 };
        _blink.Tick += (_, _) =>
        {
            _cursorOn = !_cursorOn;
            InvalidateCursor();
        };
    }

    public event EventHandler<byte[]>? UserInput;

    public event EventHandler<TerminalSize>? SizeChangedInCells;

    public int Columns => _buffer.Columns;

    public int Rows => _buffer.Rows;

    public string? Title => _emulator.Title;

    public void ApplyTheme(bool dark, string fontFamily, int fontSize, int scrollback)
    {
        _palette = TerminalPalette.For(dark);
        BackColor = _palette.Background;

        var nueva = new Font(fontFamily, fontSize);
        if (nueva.Name != _font.Name || Math.Abs(nueva.Size - _font.Size) > 0.01f)
        {
            _font.Dispose();
            _font = nueva;
            MeasureCell();
            RecalculateGrid();
        }
        else
        {
            nueva.Dispose();
        }

        _buffer.ScrollbackLimit = scrollback;
        Invalidate();
    }

    internal const float ZoomMinimo = 6f;

    internal const float ZoomMaximo = 32f;

    public float TamanoDeLetra => _font.Size;

    public bool Zoom(float puntos)
    {
        var destino = Math.Clamp(_font.Size + puntos, ZoomMinimo, ZoomMaximo);

        if (Math.Abs(destino - _font.Size) < 0.01f)
        {
            return false;
        }

        var nueva = new Font(_font.Name, destino);

        _font.Dispose();
        _font = nueva;

        MeasureCell();
        RecalculateGrid();
        Invalidate();

        return true;
    }

    public void Write(ReadOnlyMemory<byte> data)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => WriteCore(data));
        }
        else
        {
            WriteCore(data);
        }
    }

    private void WriteCore(ReadOnlyMemory<byte> data)
    {
        _emulator.Write(data.Span);
        _buffer = _emulator.Buffer;

        _scrollOffset = 0;
    }

    private void MeasureCell()
    {
        // Se mide una fila de 20 caracteres y se divide: el margen de un carácter aislado, por 80 columnas, corre el texto varios caracteres.
        const string muestra = "MMMMMMMMMMMMMMMMMMMM";
        var size = TextRenderer.MeasureText(
            muestra, _font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding);

        _cellWidth = Math.Max(1, size.Width / muestra.Length);
        _cellHeight = Math.Max(1, size.Height);
    }

    private void RecalculateGrid()
    {
        if (_cellWidth <= 0 || _cellHeight <= 0 || Width <= 0 || Height <= 0)
        {
            return;
        }

        var columnas = Math.Max(20, Width / _cellWidth);
        var filas = Math.Max(4, Height / _cellHeight);

        if (columnas == _buffer.Columns && filas == _buffer.Rows)
        {
            return;
        }

        _buffer.Resize(columnas, filas);
        SizeChangedInCells?.Invoke(this, new TerminalSize(columnas, filas));
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        RecalculateGrid();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _cursorOn = true;
        _blink.Start();
        InvalidateCursor();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _blink.Stop();

        InvalidateCursor();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var clip = e.ClipRectangle;

        g.Clear(_palette.Background);

        var desplazado = Math.Min(_scrollOffset, _buffer.Scrollback.Count);

        // Sin recortar por e.ClipRectangle, el parpadeo del cursor recorría las 55 filas de una grilla de 220x55 y asignaba ~95 KB dos veces por segundo.
        var filaDesde = _cellHeight > 0 ? Math.Max(0, clip.Top / _cellHeight) : 0;
        var filaHasta = _cellHeight > 0
            ? Math.Min(_buffer.Rows - 1, (clip.Bottom - 1) / _cellHeight)
            : _buffer.Rows - 1;

        for (var fila = filaDesde; fila <= filaHasta; fila++)
        {
            var (linea, esHistorial, filaDeBuffer) = FilaEnPantalla(fila);

            if (linea is not null)
            {
                DrawCells(g, linea, fila * _cellHeight, fila, esHistorial, filaDeBuffer);
            }
        }

        DrawCursor(g, desplazado);

        if (clip.IntersectsWith(CanalDeDesplazamiento))
        {
            DrawBarraDesplazamiento(g);
        }
    }

    private const int AnchoBarra = 9;

    private const int AltoMinimoPulgar = 26;

    private bool _arrastrandoBarra;
    private int _agarreEnPulgar;

    internal int TotalFilasDesplazables => _buffer.Scrollback.Count + _buffer.Rows;

    internal bool HayBarraDeDesplazamiento => _buffer.Scrollback.Count > 0 && Height > 0;

    internal Rectangle CanalDeDesplazamiento => new(Width - AnchoBarra, 0, AnchoBarra, Height);

    internal Rectangle PulgarDeDesplazamiento
    {
        get
        {
            var historial = _buffer.Scrollback.Count;
            var total = TotalFilasDesplazables;

            var alto = Math.Max(
                AltoMinimoPulgar,
                (int)Math.Round(Height * (_buffer.Rows / (double)total)));

            alto = Math.Min(alto, Height);

            var recorrido = Height - alto;
            var primeraVisible = historial - Math.Min(_scrollOffset, historial);

            var y = historial == 0
                ? 0
                : (int)Math.Round(recorrido * (primeraVisible / (double)historial));

            return new Rectangle(Width - AnchoBarra, y, AnchoBarra, alto);
        }
    }

    private void DrawBarraDesplazamiento(Graphics g)
    {
        if (!HayBarraDeDesplazamiento)
        {
            return;
        }

        using var canal = new SolidBrush(Color.FromArgb(34, _palette.Foreground));
        using var tinta = new SolidBrush(
            Color.FromArgb(_arrastrandoBarra ? 185 : 120, _palette.Foreground));

        g.FillRectangle(canal, CanalDeDesplazamiento);

        var pulgar = PulgarDeDesplazamiento;
        pulgar.Inflate(-2, 0);

        var antes = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using (var camino = Redondeado(pulgar, pulgar.Width / 2))
        {
            g.FillPath(tinta, camino);
        }

        g.SmoothingMode = antes;
    }

    private static System.Drawing.Drawing2D.GraphicsPath Redondeado(Rectangle r, int radio)
    {
        var camino = new System.Drawing.Drawing2D.GraphicsPath();
        var d = Math.Max(1, radio) * 2;

        if (d >= r.Height || d >= r.Width)
        {
            camino.AddRectangle(r);
            return camino;
        }

        camino.AddArc(r.X, r.Y, d, d, 180, 90);
        camino.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        camino.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        camino.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        camino.CloseFigure();

        return camino;
    }

    internal void DesplazarPorPulgar(int arriba)
    {
        var historial = _buffer.Scrollback.Count;
        var recorrido = Height - PulgarDeDesplazamiento.Height;

        if (historial == 0 || recorrido <= 0)
        {
            return;
        }

        var proporcion = Math.Clamp(arriba / (double)recorrido, 0, 1);
        var primeraVisible = (int)Math.Round(proporcion * historial);

        _scrollOffset = Math.Clamp(historial - primeraVisible, 0, historial);
        Invalidate();
    }

    private (TerminalCell[]? Linea, bool EsHistorial, int FilaDeBuffer) FilaEnPantalla(int pantallaFila)
    {
        var scrollback = _buffer.Scrollback;
        var desplazado = Math.Min(_scrollOffset, scrollback.Count);
        var desdeHistorial = desplazado - pantallaFila;

        if (desdeHistorial > 0)
        {
            var indice = scrollback.Count - desdeHistorial;

            return indice >= 0 && indice < scrollback.Count
                ? (scrollback[indice], true, -1)
                : (null, true, -1);
        }

        var fila = pantallaFila - desplazado;

        if (fila < 0 || fila >= _buffer.Rows)
        {
            return (null, false, -1);
        }

        var linea = new TerminalCell[_buffer.Columns];

        for (var x = 0; x < _buffer.Columns; x++)
        {
            linea[x] = _buffer.At(fila, x);
        }

        return (linea, false, fila);
    }

    private void DrawCells(
        Graphics g, TerminalCell[] linea, int y, int pantallaFila, bool esHistorial, int filaDeBuffer)
    {
        var indicesAqui = esHistorial
            ? (_indicesPorLineaHistorial.TryGetValue(linea, out var porHistorial) ? porHistorial : null)
            : (_indicesPorFilaPantalla.TryGetValue(filaDeBuffer, out var porFila) ? porFila : null);

        (bool Coincide, bool EsActual) Resaltado(int columna)
        {
            if (indicesAqui is null)
            {
                return (false, false);
            }

            foreach (var indice in indicesAqui)
            {
                var coincidencia = _busqueda.Coincidencias[indice];

                if (columna >= coincidencia.Columna && columna < coincidencia.Columna + coincidencia.Longitud)
                {
                    return (true, indice == _busqueda.IndiceActual);
                }
            }

            return (false, false);
        }

        var x = 0;

        while (x < linea.Length)
        {
            var inicio = x;
            var celda = linea[x];

            var seleccionado = EstaSeleccionado(pantallaFila, x);
            var resaltado = Resaltado(x);

            while (x < linea.Length
                   && MismaPresentacion(linea[x], celda)
                   && EstaSeleccionado(pantallaFila, x) == seleccionado
                   && Resaltado(x) == resaltado)
            {
                x++;
            }

            var texto = new StringBuilder(x - inicio);
            for (var i = inicio; i < x; i++)
            {
                texto.Append(linea[i].Char);
            }

            var frente = _palette.Resolve(celda.Foreground, false, celda.Flags);
            var fondo = _palette.Resolve(celda.Background, true, celda.Flags);

            if (celda.Flags.HasFlag(CellFlags.Inverse) ^ seleccionado)
            {
                (frente, fondo) = (fondo, frente);
            }

            if (celda.Flags.HasFlag(CellFlags.Dim))
            {
                frente = Color.FromArgb(160, frente);
            }

            var desvanecido = esHistorial;

            if (resaltado.Coincide)
            {
                fondo = resaltado.EsActual ? _palette.FondoCoincidenciaActual : _palette.FondoCoincidencia;
                frente = _palette.TextoSobreCoincidencia;
                desvanecido = false;
            }

            var rect = new Rectangle(inicio * _cellWidth, y, (x - inicio) * _cellWidth, _cellHeight);

            if (fondo != _palette.Background)
            {
                using var brush = new SolidBrush(fondo);
                g.FillRectangle(brush, rect);
            }

            var estilo = FontStyle.Regular;
            if (celda.Flags.HasFlag(CellFlags.Bold)) { estilo |= FontStyle.Bold; }
            if (celda.Flags.HasFlag(CellFlags.Underline)) { estilo |= FontStyle.Underline; }

            using var fuente = estilo == FontStyle.Regular
                ? null
                : new Font(_font, estilo);

            TextRenderer.DrawText(
                g, texto.ToString(), fuente ?? _font, rect.Location,
                desvanecido ? Color.FromArgb(210, frente) : frente,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    private static bool MismaPresentacion(TerminalCell a, TerminalCell b) =>
        a.Foreground == b.Foreground && a.Background == b.Background && a.Flags == b.Flags;

    private void DrawCursor(Graphics g, int desplazado)
    {
        if (!_buffer.CursorVisible || !_cursorOn || !Focused || desplazado > 0)
        {
            return;
        }

        var rect = new Rectangle(
            _buffer.CursorX * _cellWidth,
            _buffer.CursorY * _cellHeight,
            _cellWidth,
            _cellHeight);

        using var brush = new SolidBrush(_palette.Foreground);
        g.FillRectangle(brush, rect);

        var bajo = _buffer.At(_buffer.CursorY, _buffer.CursorX);
        TextRenderer.DrawText(
            g, bajo.Char.ToString(), _font, rect.Location, _palette.Background,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    private void InvalidateCursor()
    {
        if (!IsHandleCreated || _cellWidth <= 0)
        {
            return;
        }

        Invalidate(new Rectangle(
            _buffer.CursorX * _cellWidth,
            _buffer.CursorY * _cellHeight,
            _cellWidth + 1,
            _cellHeight + 1));
    }

    private void SafeInvalidate()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(Invalidate);
        }
        else
        {
            Invalidate();
        }
    }

    protected override bool IsInputKey(Keys keyData) => true;

    public event EventHandler? PidioDiagnostico;

    public event EventHandler? PidioPaleta;

    public event EventHandler? PidioBusqueda;

    /// <summary>Combinaciones que la aplicación se queda en lugar de mandarlas al servidor (FR-032).</summary>
    internal enum AccionDeTeclado
    {
        AlServidor,
        Diagnostico,
        Buscar,
        Copiar,
        Pegar,
        SeleccionarLoVisible,
        Paleta,
        ZoomMas,
        ZoomMenos,
        ZoomDeOrigen,
        HistorialPaginaArriba,
        HistorialPaginaAbajo,
        HistorialLineaArriba,
        HistorialLineaAbajo,
        HistorialAlPrincipio,
        HistorialAlFinal,
    }

    internal static AccionDeTeclado DecidirTeclado(Keys tecla, bool control, bool shift, bool alt)
    {
        if (alt)
        {
            return AccionDeTeclado.AlServidor;
        }

        if (tecla == Keys.F12 && !control && !shift)
        {
            return AccionDeTeclado.Diagnostico;
        }

        if (tecla == Keys.Insert)
        {
            return (control, shift) switch
            {
                (true, false) => AccionDeTeclado.Copiar,
                (false, true) => AccionDeTeclado.Pegar,
                _ => AccionDeTeclado.AlServidor,
            };
        }

        if (control && shift)
        {
            switch (tecla)
            {
                case Keys.C: return AccionDeTeclado.Copiar;
                case Keys.V: return AccionDeTeclado.Pegar;
                case Keys.A: return AccionDeTeclado.SeleccionarLoVisible;
                case Keys.P: return AccionDeTeclado.Paleta;

                case Keys.PageUp or Keys.Home: return AccionDeTeclado.HistorialAlPrincipio;
                case Keys.PageDown or Keys.End: return AccionDeTeclado.HistorialAlFinal;
            }
        }

        if (control && !shift && tecla == Keys.F)
        {
            return AccionDeTeclado.Buscar;
        }

        if (shift && !control && tecla is Keys.PageUp or Keys.PageDown)
        {
            return tecla == Keys.PageUp
                ? AccionDeTeclado.HistorialPaginaArriba
                : AccionDeTeclado.HistorialPaginaAbajo;
        }

        if (control && !shift && tecla is Keys.PageUp or Keys.PageDown)
        {
            return tecla == Keys.PageUp
                ? AccionDeTeclado.HistorialLineaArriba
                : AccionDeTeclado.HistorialLineaAbajo;
        }

        if (control)
        {
            switch (tecla)
            {
                case Keys.Add or Keys.Oemplus: return AccionDeTeclado.ZoomMas;
                case Keys.Subtract or Keys.OemMinus: return AccionDeTeclado.ZoomMenos;
                case Keys.D0 or Keys.NumPad0: return AccionDeTeclado.ZoomDeOrigen;
            }
        }

        return AccionDeTeclado.AlServidor;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var accion = DecidirTeclado(e.KeyCode, e.Control, e.Shift, e.Alt);

        if (accion != AccionDeTeclado.AlServidor)
        {
            Ejecutar(accion);
            e.Handled = e.SuppressKeyPress = true;
            return;
        }

        var bytes = KeyboardMapper.Map(
            e.KeyCode, e.Control, e.Alt, e.Shift,
            _emulator.ApplicationCursorKeys, _emulator.TecladoNumericoEnModoAplicacion);

        if (bytes is not null)
        {
            UserInput?.Invoke(this, bytes);
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    private void Ejecutar(AccionDeTeclado accion)
    {
        var pagina = Math.Max(1, _buffer.Rows - 1);

        switch (accion)
        {
            case AccionDeTeclado.Diagnostico: PidioDiagnostico?.Invoke(this, EventArgs.Empty); break;
            case AccionDeTeclado.Buscar: PidioBusqueda?.Invoke(this, EventArgs.Empty); break;
            case AccionDeTeclado.Paleta: PidioPaleta?.Invoke(this, EventArgs.Empty); break;
            case AccionDeTeclado.Copiar: CopySelection(); break;
            case AccionDeTeclado.Pegar: Paste(); break;
            case AccionDeTeclado.SeleccionarLoVisible: SelectAll(); break;
            case AccionDeTeclado.ZoomMas: AvisarZoom(Zoom(1f)); break;
            case AccionDeTeclado.ZoomMenos: AvisarZoom(Zoom(-1f)); break;
            case AccionDeTeclado.ZoomDeOrigen: PidioZoomDeOrigen?.Invoke(this, EventArgs.Empty); break;
            case AccionDeTeclado.HistorialPaginaArriba: ScrollBy(pagina); break;
            case AccionDeTeclado.HistorialPaginaAbajo: ScrollBy(-pagina); break;
            case AccionDeTeclado.HistorialLineaArriba: ScrollBy(1); break;
            case AccionDeTeclado.HistorialLineaAbajo: ScrollBy(-1); break;
            case AccionDeTeclado.HistorialAlPrincipio: ScrollBy(int.MaxValue / 2); break;
            case AccionDeTeclado.HistorialAlFinal: ScrollBy(int.MinValue / 2); break;
        }
    }

    internal void AvisarZoom(bool cambio)
    {
        if (cambio)
        {
            CambioElZoom?.Invoke(this, TamanoDeLetra);
        }
    }

    public event EventHandler? PidioZoomDeOrigen;

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
        {
            return;
        }

        UserInput?.Invoke(this, KeyboardMapper.MapText(e.KeyChar, alt: false));
        e.Handled = true;
    }

    // Sin la barra ni el punto entre los separadores, /etc/nginx/nginx.conf se parte en cinco pedazos al doble clic.
    private const string Separadores = " \t\"'`()[]{}<>,;|*!?$&^#\\";

    internal static bool EsDePalabra(char c) => !Separadores.Contains(c) && c != '\0';

    internal static (int Inicio, int Fin) LimitesDePalabra(TerminalCell[] linea, int columna)
    {
        if (linea.Length == 0)
        {
            return (0, 0);
        }

        var c = Math.Clamp(columna, 0, linea.Length - 1);

        if (!EsDePalabra(linea[c].Char))
        {
            return (c, c);
        }

        var inicio = c;
        while (inicio > 0 && EsDePalabra(linea[inicio - 1].Char))
        {
            inicio--;
        }

        var fin = c;
        while (fin < linea.Length - 1 && EsDePalabra(linea[fin + 1].Char))
        {
            fin++;
        }

        return (inicio, fin);
    }

    internal static int UltimaColumnaConTexto(TerminalCell[] linea)
    {
        for (var i = linea.Length - 1; i >= 0; i--)
        {
            if (linea[i].Char is not (' ' or '\0'))
            {
                return i;
            }
        }

        return 0;
    }

    private long _ultimoClic;
    private Point _puntoDelClic;
    private int _clicsSeguidos;

    private int ContarClics(Point celda)
    {
        var ahora = Environment.TickCount64;

        var seguido = ahora - _ultimoClic <= SystemInformation.DoubleClickTime
                      && celda == _puntoDelClic;

        _clicsSeguidos = seguido ? _clicsSeguidos + 1 : 1;
        _ultimoClic = ahora;
        _puntoDelClic = celda;

        return _clicsSeguidos;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button == MouseButtons.Left
            && HayBarraDeDesplazamiento
            && CanalDeDesplazamiento.Contains(e.Location))
        {
            var pulgar = PulgarDeDesplazamiento;

            if (pulgar.Contains(e.Location))
            {
                _arrastrandoBarra = true;
                _agarreEnPulgar = e.Y - pulgar.Y;
            }
            else
            {
                _arrastrandoBarra = true;
                _agarreEnPulgar = pulgar.Height / 2;
                DesplazarPorPulgar(e.Y - _agarreEnPulgar);
            }

            Invalidate();
            return;
        }

        var shift = ModifierKeys.HasFlag(Keys.Shift);
        var control = ModifierKeys.HasFlag(Keys.Control);

        var clics = e.Button == MouseButtons.Left ? ContarClics(ToCell(e.Location)) : 1;

        switch (DecidirMouse(e.Button, shift, control, clics))
        {
            case AccionDeMouse.EmpezarSeleccion:
                _selectionStart = ToCell(e.Location);
                _selectionEnd = _selectionStart;
                _seleccionRectangular = control;
                _seleccionPorClics = false;
                Invalidate();
                break;

            case AccionDeMouse.ExtenderSeleccion:
                _selectionStart ??= ToCell(e.Location);
                _selectionEnd = ToCell(e.Location);
                _seleccionPorClics = false;
                Invalidate();
                break;

            case AccionDeMouse.SeleccionarPalabra:
                SeleccionarPalabra(ToCell(e.Location));
                break;

            case AccionDeMouse.SeleccionarLinea:
                SeleccionarLinea(ToCell(e.Location));
                break;

            case AccionDeMouse.Pegar:
                Paste();
                break;

            case AccionDeMouse.Menu:
                MostrarMenu(e.Location);
                break;
        }
    }

    /// <summary>Qué hace cada botón del mouse dentro del área de texto.</summary>
    internal enum AccionDeMouse
    {
        Ninguna,
        EmpezarSeleccion,
        ExtenderSeleccion,
        SeleccionarPalabra,
        SeleccionarLinea,
        Pegar,
        Menu,
    }

    internal static AccionDeMouse DecidirMouse(MouseButtons boton, bool shift, bool control, int clics)
    {
        switch (boton)
        {
            case MouseButtons.Left:
                if (shift)
                {
                    return AccionDeMouse.ExtenderSeleccion;
                }

                return clics switch
                {
                    2 => AccionDeMouse.SeleccionarPalabra,
                    >= 3 => AccionDeMouse.SeleccionarLinea,
                    _ => AccionDeMouse.EmpezarSeleccion,
                };

            case MouseButtons.Middle:
                return AccionDeMouse.ExtenderSeleccion;

            case MouseButtons.Right:
                return control ? AccionDeMouse.Menu : AccionDeMouse.Pegar;

            default:
                return AccionDeMouse.Ninguna;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        Cursor = HayBarraDeDesplazamiento && CanalDeDesplazamiento.Contains(e.Location)
            ? Cursors.Default
            : Cursors.IBeam;

        if (_arrastrandoBarra)
        {
            DesplazarPorPulgar(e.Y - _agarreEnPulgar);
            return;
        }

        if (e.Button == MouseButtons.Left && _selectionStart is not null)
        {
            AcompanarSeleccion(e.Y);

            _selectionEnd = ToCell(e.Location);
            Invalidate();
        }
    }

    private void AcompanarSeleccion(int y)
    {
        if (y < 0)
        {
            ScrollBy(1 + (-y / Math.Max(1, _cellHeight)));
        }
        else if (y > Height)
        {
            ScrollBy(-(1 + ((y - Height) / Math.Max(1, _cellHeight))));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_arrastrandoBarra)
        {
            _arrastrandoBarra = false;
            Invalidate();
            return;
        }

        if (e.Button is MouseButtons.Left or MouseButtons.Middle)
        {
            TerminarSeleccion();
        }
    }

    internal void TerminarSeleccion()
    {
        if (_selectionStart is not { } a || _selectionEnd is not { } b)
        {
            return;
        }

        if (a == b && !_seleccionPorClics)
        {
            LimpiarSeleccion();
            return;
        }

        CopySelection();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (ModifierKeys.HasFlag(Keys.Control))
        {
            if (Zoom(e.Delta > 0 ? 1f : -1f))
            {
                CambioElZoom?.Invoke(this, TamanoDeLetra);
            }

            return;
        }

        ScrollBy(e.Delta > 0 ? 3 : -3);
    }

    public event EventHandler<float>? CambioElZoom;

    private void SeleccionarPalabra(Point celda)
    {
        var (linea, _, _) = FilaEnPantalla(celda.Y);

        if (linea is null)
        {
            return;
        }

        var (inicio, fin) = LimitesDePalabra(linea, celda.X);

        _selectionStart = new Point(inicio, celda.Y);
        _selectionEnd = new Point(fin, celda.Y);
        _seleccionRectangular = false;
        _seleccionPorClics = true;

        Invalidate();
    }

    private void SeleccionarLinea(Point celda)
    {
        var (linea, _, _) = FilaEnPantalla(celda.Y);

        if (linea is null)
        {
            return;
        }

        _selectionStart = new Point(0, celda.Y);
        _selectionEnd = new Point(UltimaColumnaConTexto(linea), celda.Y);
        _seleccionRectangular = false;
        _seleccionPorClics = true;

        Invalidate();
    }

    private bool HaySeleccion => _selectionStart is not null && _selectionEnd is not null;

    private bool HayTextoParaPegar()
    {
        try
        {
            return !string.IsNullOrEmpty(LeerDelPortapapeles());
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return false;
        }
    }

    private void LimpiarSeleccion()
    {
        _selectionStart = null;
        _selectionEnd = null;
        _seleccionRectangular = false;
        _seleccionPorClics = false;
        Invalidate();
    }

    public void SelectAll()
    {
        _selectionStart = new Point(0, 0);
        _selectionEnd = new Point(_buffer.Columns - 1, _buffer.Rows - 1);
        _seleccionRectangular = false;
        _seleccionPorClics = true;
        Invalidate();
    }

    private void MostrarMenu(Point donde)
    {
        _menu?.Dispose();

        var menu = new ContextMenuStrip
        {
            BackColor = _palette.Background,
            ForeColor = _palette.Foreground,
            ShowImageMargin = false,

            Renderer = new MenuOscuro(_palette),
            Font = new Font("Segoe UI", 9f),
        };

        void Agregar(string texto, string atajo, bool habilitado, Action accion)
        {
            var item = new ToolStripMenuItem(texto)
            {
                ShortcutKeyDisplayString = atajo,
                Enabled = habilitado,
            };

            item.Click += (_, _) => accion();
            menu.Items.Add(item);
        }

        Agregar("Copiar", "Ctrl+Ins", HaySeleccion, () =>
        {
            CopySelection();
            LimpiarSeleccion();
        });

        Agregar("Pegar", "Shift+Ins", HayTextoParaPegar(), Paste);
        menu.Items.Add(new ToolStripSeparator());
        Agregar("Seleccionar lo visible", "Ctrl+Shift+A", true, SelectAll);

        _menu = menu;
        menu.Show(this, donde);
    }

    /// <summary>Orden de lectura: primero por fila, después por columna.</summary>
    private static bool Precede(Point a, Point b) =>
        a.Y < b.Y || (a.Y == b.Y && a.X <= b.X);

    private Point ToCell(Point p) => new(
        Math.Clamp(p.X / Math.Max(1, _cellWidth), 0, _buffer.Columns - 1),
        Math.Clamp(p.Y / Math.Max(1, _cellHeight), 0, _buffer.Rows - 1));

    // Pintado y copia resolvían la selección por separado y se copiaba algo distinto de lo marcado (T324).
    internal static (int Desde, int Hasta)? TramoDeFila(
        int fila, Point inicio, Point fin, int ultimaColumna, bool rectangular)
    {
        if (fila < inicio.Y || fila > fin.Y)
        {
            return null;
        }

        if (rectangular)
        {
            return (Math.Min(inicio.X, fin.X), Math.Max(inicio.X, fin.X));
        }

        var desde = fila == inicio.Y ? inicio.X : 0;
        var hasta = fila == fin.Y ? fin.X : ultimaColumna;

        return (desde, hasta);
    }

    private bool EstaSeleccionado(int fila, int columna)
    {
        if (_selectionStart is not { } a || _selectionEnd is not { } b)
        {
            return false;
        }

        var (inicio, fin) = Precede(a, b) ? (a, b) : (b, a);
        var tramo = TramoDeFila(fila, inicio, fin, _buffer.Columns - 1, _seleccionRectangular);

        return tramo is { } t && columna >= t.Desde && columna <= t.Hasta;
    }

    public string SelectedText
    {
        get
        {
            if (_selectionStart is not { } a || _selectionEnd is not { } b)
            {
                return string.Empty;
            }

            return ArmarTexto(Precede(a, b) ? (a, b) : (b, a));
        }
    }

    public void CopySelection()
    {
        var texto = SelectedText;

        if (texto.Length == 0)
        {
            return;
        }

        try
        {
            EscribirEnPortapapeles(texto);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
        }
    }

    public void ScrollBy(int lineas)
    {
        _scrollOffset = Math.Clamp(_scrollOffset + lineas, 0, _buffer.Scrollback.Count);
        Invalidate();
    }

    public string TextoBuscado => _textoBuscado;

    public int TotalCoincidencias => _busqueda.Total;

    public int CoincidenciaActual => _busqueda.Posicion;

    public void Buscar(string texto)
    {
        _textoBuscado = texto;
        _busqueda.Establecer(BuscadorDeTerminal.Buscar(_buffer, texto));
        IndexarCoincidencias();

        if (_busqueda.Actual is { } actual)
        {
            DesplazarACoincidencia(actual);
        }

        Invalidate();
    }

    public void BusquedaSiguiente()
    {
        if (_busqueda.Siguiente() is { } coincidencia)
        {
            DesplazarACoincidencia(coincidencia);
        }

        Invalidate();
    }

    public void BusquedaAnterior()
    {
        if (_busqueda.Anterior() is { } coincidencia)
        {
            DesplazarACoincidencia(coincidencia);
        }

        Invalidate();
    }

    private void IndexarCoincidencias()
    {
        _indicesPorLineaHistorial.Clear();
        _indicesPorFilaPantalla.Clear();

        var coincidencias = _busqueda.Coincidencias;

        for (var i = 0; i < coincidencias.Count; i++)
        {
            var coincidencia = coincidencias[i];

            if (coincidencia.LineaHistorial is { } linea)
            {
                if (!_indicesPorLineaHistorial.TryGetValue(linea, out var lista))
                {
                    lista = [];
                    _indicesPorLineaHistorial[linea] = lista;
                }

                lista.Add(i);
            }
            else
            {
                if (!_indicesPorFilaPantalla.TryGetValue(coincidencia.Fila, out var lista))
                {
                    lista = [];
                    _indicesPorFilaPantalla[coincidencia.Fila] = lista;
                }

                lista.Add(i);
            }
        }
    }

    private void DesplazarACoincidencia(TerminalCoincidencia coincidencia)
    {
        if (coincidencia.LineaHistorial is { } linea)
        {
            var scrollback = _buffer.Scrollback;
            var indice = -1;

            for (var i = 0; i < scrollback.Count; i++)
            {
                if (ReferenceEquals(scrollback[i], linea))
                {
                    indice = i;
                    break;
                }
            }

            if (indice < 0)
            {
                return;
            }

            _scrollOffset = Math.Clamp(scrollback.Count - indice, 0, scrollback.Count);
        }
        else
        {
            _scrollOffset = 0;
        }
    }

    public string TextoCompleto
    {
        get
        {
            var sb = new StringBuilder();

            foreach (var linea in _buffer.Scrollback)
            {
                sb.AppendLine(SinCola(linea));
            }

            for (var fila = 0; fila < _buffer.Rows; fila++)
            {
                sb.AppendLine(_buffer.LineText(fila).TrimEnd());
            }

            return sb.ToString().TrimEnd() + Environment.NewLine;
        }
    }

    private static string SinCola(TerminalCell[] linea)
    {
        var sb = new StringBuilder(linea.Length);

        foreach (var celda in linea)
        {
            sb.Append(celda.Char);
        }

        while (sb.Length > 0 && sb[^1] == ' ')
        {
            sb.Length--;
        }

        return sb.ToString();
    }

    public int LineasDeHistorial => _buffer.Scrollback.Count;

    public void BorrarHistorial()
    {
        _buffer.ClearScrollback();
        _scrollOffset = 0;
        LimpiarSeleccion();
        Invalidate();
    }

    public void Restablecer()
    {
        _emulator.Reset();
        _scrollOffset = 0;
        LimpiarSeleccion();
        Invalidate();
    }

    private string ArmarTexto((Point Inicio, Point Fin) rango)
    {
        var (inicio, fin) = rango;
        var sb = new StringBuilder();

        for (var fila = inicio.Y; fila <= fin.Y; fila++)
        {
            var (linea, _, _) = FilaEnPantalla(fila);

            if (linea is null)
            {
                continue;
            }

            if (TramoDeFila(fila, inicio, fin, linea.Length - 1, _seleccionRectangular)
                is not { } tramo)
            {
                continue;
            }

            var (desde, hasta) = (Math.Max(0, tramo.Desde), tramo.Hasta);

            for (var x = desde; x <= hasta && x < linea.Length; x++)
            {
                sb.Append(linea[x].Char);
            }

            while (sb.Length > 0 && sb[^1] == ' ')
            {
                sb.Length--;
            }

            if (fila < fin.Y)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    public event EventHandler<ConfirmacionDePegado>? PidioConfirmarPegado;

    public sealed class ConfirmacionDePegado(int lineas) : EventArgs
    {
        public int Lineas { get; } = lineas;

        public bool Aceptado { get; set; } = true;
    }

    /// <summary>Normaliza CRLF a CR; sin esto cada línea pegada llega duplicada (FR-030g).</summary>
    internal static string NormalizarPegado(string texto) =>
        texto.Replace("\r\n", "\r").Replace('\n', '\r');

    internal static int ContarLineas(string normalizado) =>
        normalizado.TrimEnd('\r').Count(c => c == '\r') + 1;

    internal static byte[] ArmarPegado(string normalizado, bool bracketed) =>
        Encoding.UTF8.GetBytes(bracketed ? $"\x1b[200~{normalizado}\x1b[201~" : normalizado);

    public void Paste()
    {
        string? crudo;

        try
        {
            crudo = LeerDelPortapapeles();
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return;
        }

        if (string.IsNullOrEmpty(crudo))
        {
            return;
        }

        var texto = NormalizarPegado(crudo);
        var bracketed = _emulator.BracketedPaste;

        if (!bracketed && ContarLineas(texto) > 1)
        {
            var pregunta = new ConfirmacionDePegado(ContarLineas(texto));
            PidioConfirmarPegado?.Invoke(this, pregunta);

            if (!pregunta.Aceptado)
            {
                return;
            }
        }

        UserInput?.Invoke(this, ArmarPegado(texto, bracketed));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _blink.Stop();
            _blink.Dispose();
            _font.Dispose();
            _menu?.Dispose();
        }

        base.Dispose(disposing);
    }
}
