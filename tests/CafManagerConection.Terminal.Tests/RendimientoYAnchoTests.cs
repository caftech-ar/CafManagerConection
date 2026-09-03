using System.Diagnostics;
using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

public class RendimientoYAnchoTests
{
    private static (VtEmulator Emu, TerminalBuffer Buf) Nuevo(int cols = 80, int rows = 24)
    {
        var buffer = new TerminalBuffer(cols, rows);
        return (new VtEmulator(buffer), buffer);
    }

    private static byte[] CuadroCompleto(int cols, int rows)
    {
        var sb = new StringBuilder();

        sb.Append("\x1b[H\x1b[2J");

        for (var fila = 1; fila <= rows; fila++)
        {
            sb.Append($"\x1b[{fila};1H");

            for (var tramo = 0; tramo < 8; tramo++)
            {
                sb.Append($"\x1b[3{tramo % 8}m");
                sb.Append(new string((char)('a' + tramo), cols / 8));
            }
        }

        sb.Append("\x1b[0m");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    [Fact]
    public void Sostiene_al_menos_60_repintados_por_segundo_a_80x24()
    {
        const int cuadros = 60;
        var (emu, _) = Nuevo();
        var datos = CuadroCompleto(80, 24);

        emu.Write(datos);

        var reloj = Stopwatch.StartNew();

        for (var i = 0; i < cuadros; i++)
        {
            emu.Write(datos);
        }

        reloj.Stop();

        Assert.True(
            reloj.ElapsedMilliseconds < 1000,
            $"60 cuadros completos tardaron {reloj.ElapsedMilliseconds} ms; el piso es 1000 ms.");
    }

    [Fact]
    public void Una_rafaga_grande_de_texto_corrido_no_se_atraganta()
    {
        var (emu, _) = Nuevo();
        var bloque = Encoding.UTF8.GetBytes(new string('x', 8_000) + "\r\n");

        var reloj = Stopwatch.StartNew();

        for (var i = 0; i < 200; i++)
        {
            emu.Write(bloque);
        }

        reloj.Stop();

        Assert.True(
            reloj.ElapsedMilliseconds < 1000,
            $"1,6 MB de texto corrido tardaron {reloj.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void El_historial_no_crece_sin_limite_por_mas_que_llueva_texto()
    {
        // Se admite un excedente del 10 %: el recorte se hace por tandas, no línea por línea,
        // porque quitar el primer elemento de la lista desplaza todos los demás.
        const int tope = 100;
        var buffer = new TerminalBuffer(80, 24, scrollbackLimit: tope);
        var emu = new VtEmulator(buffer);

        for (var i = 0; i < 500; i++)
        {
            emu.Write(Encoding.UTF8.GetBytes($"linea {i}\r\n"));
        }

        Assert.True(
            buffer.Scrollback.Count <= tope + (tope / 10),
            $"el historial creció a {buffer.Scrollback.Count} líneas con un tope de {tope}.");
    }

    /// <remarks>
    /// Con 220 columnas y TerminalCell de 8 bytes, archivar la fila entera pesaba ~1,8 KB
    /// aunque sólo tuviera "hola"; recortada a la última columna con contenido pesa ~32 bytes.
    /// </remarks>
    [Fact]
    public void Una_linea_corta_archivada_en_una_grilla_ancha_no_ocupa_el_ancho_entero()
    {
        const int columnas = 220;
        var buffer = new TerminalBuffer(columnas, 5);
        var emu = new VtEmulator(buffer);

        emu.Write(Encoding.UTF8.GetBytes("hola\r\n\r\n\r\n\r\n\r\n"));

        Assert.NotEmpty(buffer.Scrollback);

        var archivada = buffer.Scrollback[0];

        Assert.Equal(4, archivada.Length);
        Assert.Equal("hola", new string(archivada.Select(c => c.Char).ToArray()));

        Assert.True(
            archivada.Length < columnas / 2,
            $"la línea archivada mide {archivada.Length} celdas en una grilla de {columnas} "
            + "columnas: no se está recortando el relleno.");
    }

    [Fact]
    public void Una_linea_en_blanco_archivada_ocupa_una_sola_celda()
    {
        var buffer = new TerminalBuffer(220, 5);
        var emu = new VtEmulator(buffer);

        emu.Write(Encoding.UTF8.GetBytes("\r\n\r\n\r\n\r\n\r\n"));

        Assert.NotEmpty(buffer.Scrollback);
        Assert.Single(buffer.Scrollback[0]);
    }

    [Fact]
    public void Un_espacio_con_color_de_fondo_propio_no_se_recorta_como_relleno()
    {
        var buffer = new TerminalBuffer(220, 5);
        var emu = new VtEmulator(buffer);

        emu.Write(Encoding.UTF8.GetBytes("hola\x1b[7m \x1b[0m"));
        emu.Write(Encoding.UTF8.GetBytes("\r\n\r\n\r\n\r\n\r\n"));

        var archivada = buffer.Scrollback[0];

        Assert.Equal(5, archivada.Length);
        Assert.Equal(' ', archivada[4].Char);
        Assert.NotEqual(CellFlags.None, archivada[4].Flags);
    }

    [Fact]
    public void Un_espacio_de_relleno_al_final_si_se_recorta()
    {
        var buffer = new TerminalBuffer(220, 5);
        var emu = new VtEmulator(buffer);

        emu.Write(Encoding.UTF8.GetBytes("hola "));
        emu.Write(Encoding.UTF8.GetBytes("\r\n\r\n\r\n\r\n\r\n"));

        Assert.Equal(4, buffer.Scrollback[0].Length);
    }

    [Fact]
    public void Un_caracter_cjk_ocupa_una_celda_y_esta_es_la_limitacion_conocida()
    {
        // Limitación conocida: el emulador trata los caracteres CJK de ancho doble como de
        // ancho simple.
        var (emu, buf) = Nuevo();

        emu.Write(Encoding.UTF8.GetBytes("日本語"));

        Assert.Equal(3, buf.CursorX);
        Assert.Equal('日', buf.At(0, 0).Char);
        Assert.Equal('本', buf.At(0, 1).Char);
        Assert.Equal('語', buf.At(0, 2).Char);
    }

    [Fact]
    public void Un_acento_no_rompe_el_conteo_de_columnas()
    {
        var (emu, buf) = Nuevo();

        emu.Write(Encoding.UTF8.GetBytes("configuración"));

        Assert.Equal(13, buf.CursorX);
        Assert.Equal("configuración", buf.LineText(0).TrimEnd());
    }

    [Fact]
    public void Un_emoji_fuera_del_plano_basico_no_descoloca_el_resto_de_la_linea()
    {
        var (emu, buf) = Nuevo();

        emu.Write(Encoding.UTF8.GetBytes("ok \U0001F600 listo"));

        Assert.Contains("listo", buf.LineText(0), StringComparison.Ordinal);
    }
}
