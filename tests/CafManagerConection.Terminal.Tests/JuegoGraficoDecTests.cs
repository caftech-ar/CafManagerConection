using System.Text;

namespace CafManagerConection.Terminal.Tests;

/// <summary>
/// Juego gráfico de DEC: es lo que dibuja los cuadros de dialog, whiptail, nmtui y mc.
/// </summary>
public class JuegoGraficoDecTests
{
    private const string Esc = "\u001b";
    private const string So = "\u000e";
    private const string Si = "\u000f";

    private static (VtEmulator Emu, TerminalBuffer Buf) Nuevo(int cols = 80, int rows = 24)
    {
        var buffer = new TerminalBuffer(cols, rows);
        return (new VtEmulator(buffer), buffer);
    }

    private static void Write(VtEmulator emu, string texto) =>
        emu.Write(Encoding.UTF8.GetBytes(texto));

    [Fact]
    public void El_borde_superior_de_dialog_se_dibuja_con_lineas()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0lqqqk{Esc}(B");

        Assert.Equal("┌───┐", buf.LineText(0));
    }

    [Fact]
    public void Al_volver_a_ascii_la_q_vuelve_a_ser_una_q()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0lqqqk{Esc}(Bque tal");

        Assert.Equal("┌───┐que tal", buf.LineText(0));
    }

    [Fact]
    public void Un_cuadro_de_tres_lineas_como_el_de_dialog()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0lqqqqqk{Esc}(B\r\n" +
                   $"{Esc}(0x{Esc}(B hola {Esc}(0x{Esc}(B\r\n" +
                   $"{Esc}(0mqqqqqj{Esc}(B");

        Assert.Equal("┌─────┐", buf.LineText(0));
        Assert.Equal("│ hola │", buf.LineText(1));
        Assert.Equal("└─────┘", buf.LineText(2));
    }

    [Fact]
    public void El_juego_grafico_se_invoca_en_g1_con_so_y_se_sale_con_si()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc})0{So}lqk{Si}abc");

        Assert.Equal("┌─┐abc", buf.LineText(0));
    }

    [Fact]
    public void Designar_g1_no_cambia_nada_hasta_que_llega_so()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc})0lqk");

        Assert.Equal("lqk", buf.LineText(0));
    }

    [Fact]
    public void Fuera_del_rango_no_se_traduce_nada()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(012345 ABC[]^ áéñ");

        Assert.Equal("12345 ABC[]^ áéñ", buf.LineText(0));
    }

    [Fact]
    public void Los_parametros_de_las_secuencias_no_pasan_por_la_traduccion()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0{Esc}[1;31mq{Esc}[0mq");

        Assert.Equal("──", buf.LineText(0));
    }

    [Fact]
    public void Un_final_desconocido_deja_el_juego_en_ascii_y_el_emulador_en_texto()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(%hola");

        Assert.Equal("hola", buf.LineText(0));
    }

    [Fact]
    public void El_ascii_del_reino_unido_no_es_grafico()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0q{Esc}(Aq");

        Assert.Equal("─q", buf.LineText(0));
    }

    [Fact]
    public void La_tabla_completa_de_0x5f_a_0x7e()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0_`abcdefghijklmnopqrstuvwxyz{{|}}~");

        Assert.Equal(" ◆▒␉␌␍␊°±␤␋┘┐┌└┼⎺⎻─⎼⎽├┤┴┬│≤≥π≠£·", buf.LineText(0));
    }

    [Fact]
    public void El_guion_bajo_se_dibuja_como_un_espacio()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0q_q");

        Assert.Equal("─ ─", buf.LineText(0));
    }

    [Fact]
    public void Guardar_y_restaurar_el_cursor_lleva_el_juego_de_caracteres_consigo()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0{Esc}7{Esc}(Bq{Esc}8q");

        Assert.Equal("─", buf.LineText(0));
    }

    [Fact]
    public void El_reset_vuelve_los_dos_registros_a_ascii()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}(0{Esc})0{So}{Esc}clqk");

        Assert.Equal("lqk", buf.LineText(0));
    }

    [Fact]
    public void La_designacion_de_g2_no_afecta_a_lo_que_se_escribe()
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"{Esc}*0lqk");

        Assert.Equal("lqk", buf.LineText(0));
    }
}
