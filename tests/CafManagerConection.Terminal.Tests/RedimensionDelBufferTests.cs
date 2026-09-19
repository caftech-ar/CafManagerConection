namespace CafManagerConection.Terminal.Tests;

public sealed class RedimensionDelBufferTests
{
    private static TerminalBuffer ConLineas(int filas, int columnas = 40)
    {
        var buffer = new TerminalBuffer(columnas, filas);

        for (var y = 0; y < filas; y++)
        {
            buffer.CursorX = 0;
            buffer.CursorY = y;

            foreach (var c in $"linea{y}")
            {
                buffer.Write(c, TerminalCell.DefaultColor, TerminalCell.DefaultColor, CellFlags.None);
            }
        }

        buffer.CursorY = filas - 1;

        return buffer;
    }

    [Fact]
    public void Al_achicar_se_conserva_el_final_y_no_el_principio()
    {
        var buffer = ConLineas(20);

        buffer.Resize(40, 5);

        Assert.Equal("linea15", buffer.LineText(0));
        Assert.Equal("linea19", buffer.LineText(4));
    }

    [Fact]
    public void Al_achicar_el_cursor_sigue_en_su_linea()
    {
        var buffer = ConLineas(20);

        buffer.Resize(40, 5);

        Assert.Equal(4, buffer.CursorY);
    }

    [Fact]
    public void Lo_que_sale_por_arriba_va_al_historial()
    {
        var buffer = ConLineas(20);
        var antes = buffer.Scrollback.Count;

        buffer.Resize(40, 5);

        Assert.Equal(antes + 15, buffer.Scrollback.Count);
    }

    [Fact]
    public void Al_agrandar_no_se_pierde_nada()
    {
        var buffer = ConLineas(10);

        buffer.Resize(40, 30);

        Assert.Equal("linea0", buffer.LineText(0));
        Assert.Equal("linea9", buffer.LineText(9));
        Assert.Equal(9, buffer.CursorY);
    }

    [Fact]
    public void Con_el_cursor_arriba_no_se_descarta_lo_que_esta_sobre_el()
    {
        var buffer = ConLineas(20);
        buffer.CursorY = 2;

        buffer.Resize(40, 10);

        Assert.Equal("linea0", buffer.LineText(0));
        Assert.Equal(2, buffer.CursorY);
    }

    [Fact]
    public void Achicar_solo_el_ancho_no_toca_las_filas()
    {
        var buffer = ConLineas(10);

        buffer.Resize(20, 10);

        Assert.Equal("linea0", buffer.LineText(0));
        Assert.Equal("linea9", buffer.LineText(9));
    }

    private static TerminalBuffer ConTexto(string texto, int columnas)
    {
        var buffer = new TerminalBuffer(columnas, 5);

        foreach (var c in texto)
        {
            buffer.Write(c, TerminalCell.DefaultColor, TerminalCell.DefaultColor, CellFlags.None);
        }

        return buffer;
    }

    [Fact]
    public void Al_angostar_la_linea_se_ve_cortada()
    {
        var buffer = ConTexto("/var/log/nginx/access.log", 40);

        buffer.Resize(12, 5);

        Assert.Equal("/var/log/ngi", buffer.LineText(0));
    }

    [Fact]
    public void Al_volver_a_ensanchar_la_linea_vuelve_entera()
    {
        var buffer = ConTexto("/var/log/nginx/access.log", 40);

        buffer.Resize(12, 5);
        buffer.Resize(40, 5);

        Assert.Equal("/var/log/nginx/access.log", buffer.LineText(0));
    }

    [Fact]
    public void Lo_que_quedo_fuera_de_la_vista_tambien_se_archiva()
    {
        var buffer = ConTexto("/var/log/nginx/access.log", 40);

        buffer.Resize(12, 5);
        buffer.CursorY = 4;
        buffer.ScrollUp();

        var archivada = new string([.. buffer.Scrollback[^1].Select(c => c.Char)]);

        Assert.Equal("/var/log/nginx/access.log", archivada);
    }

    [Fact]
    public void Borrar_la_linea_tambien_borra_lo_que_no_se_ve()
    {
        var buffer = ConTexto("/var/log/nginx/access.log", 40);

        buffer.Resize(12, 5);
        buffer.ClearLine(0);
        buffer.Resize(40, 5);

        Assert.Equal(string.Empty, buffer.LineText(0));
    }
}
