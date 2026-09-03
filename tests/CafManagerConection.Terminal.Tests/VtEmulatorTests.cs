using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

public class VtEmulatorTests
{
    private static (VtEmulator Emu, TerminalBuffer Buf) Nuevo(int cols = 80, int rows = 24)
    {
        var buffer = new TerminalBuffer(cols, rows);
        return (new VtEmulator(buffer), buffer);
    }

    private static void Write(VtEmulator emu, string texto) =>
        emu.Write(Encoding.UTF8.GetBytes(texto));

    private static string Linea(TerminalBuffer b, int fila) => b.LineText(fila);

    [Fact]
    public void Escribe_texto_plano()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "hola mundo");

        Assert.Equal("hola mundo", Linea(buf, 0));
        Assert.Equal(10, buf.CursorX);
    }

    [Fact]
    public void El_retorno_de_carro_vuelve_al_inicio_de_la_linea()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "abcdef\rXY");

        Assert.Equal("XYcdef", Linea(buf, 0));
    }

    [Fact]
    public void El_salto_de_linea_baja_una_fila()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "uno\r\ndos");

        Assert.Equal("uno", Linea(buf, 0));
        Assert.Equal("dos", Linea(buf, 1));
    }

    [Fact]
    public void El_retroceso_borra_hacia_atras_como_en_bash()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "abc\b \b");

        Assert.Equal("ab", Linea(buf, 0));
    }

    [Fact]
    public void Los_acentos_y_la_enie_sobreviven()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "Conexión añadida · éxito");

        Assert.Equal("Conexión añadida · éxito", Linea(buf, 0));
    }

    [Fact]
    public void Un_caracter_utf8_partido_entre_dos_bloques_se_arma_bien()
    {
        var (emu, buf) = Nuevo();
        var bytes = Encoding.UTF8.GetBytes("ó");

        emu.Write(bytes.AsSpan(0, 1));
        emu.Write(bytes.AsSpan(1));

        Assert.Equal("ó", Linea(buf, 0));
    }

    [Theory]
    [InlineData("\x1b[31m", 1)]  // rojo
    [InlineData("\x1b[32m", 2)]  // verde
    [InlineData("\x1b[34m", 4)]  // azul
    public void Los_colores_ansi_basicos_se_aplican(string secuencia, short esperado)
    {
        var (emu, buf) = Nuevo();

        Write(emu, secuencia + "X");

        Assert.Equal(esperado, buf.At(0, 0).Foreground);
    }

    [Fact]
    public void El_color_de_256_se_aplica()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b[38;5;208mX");

        Assert.Equal(208, buf.At(0, 0).Foreground);
    }

    [Fact]
    public void El_color_de_24_bits_se_aplica()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b[38;2;255;128;0mX");

        Assert.True(buf.At(0, 0).Foreground >= 256);
    }

    [Fact]
    public void El_reinicio_de_atributos_vuelve_a_los_valores_por_omision()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b[31;1mrojo\x1b[0mnormal");

        Assert.Equal(1, buf.At(0, 0).Foreground);
        Assert.Equal(TerminalCell.DefaultColor, buf.At(0, 4).Foreground);
        Assert.Equal(CellFlags.None, buf.At(0, 4).Flags);
    }

    [Fact]
    public void La_negrita_y_el_subrayado_se_registran()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b[1;4mX");

        Assert.True(buf.At(0, 0).Flags.HasFlag(CellFlags.Bold));
        Assert.True(buf.At(0, 0).Flags.HasFlag(CellFlags.Underline));
    }

    [Fact]
    public void El_cursor_se_posiciona_con_CUP()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b[5;10H");

        Assert.Equal(4, buf.CursorY);
        Assert.Equal(9, buf.CursorX);
    }

    [Fact]
    public void CUP_sin_argumentos_va_al_origen()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "\x1b[10;10H");

        Write(emu, "\x1b[H");

        Assert.Equal(0, buf.CursorX);
        Assert.Equal(0, buf.CursorY);
    }

    [Fact]
    public void Las_flechas_mueven_el_cursor()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "\x1b[10;10H");

        Write(emu, "\x1b[2A\x1b[3C");

        Assert.Equal(7, buf.CursorY);
        Assert.Equal(12, buf.CursorX);
    }

    [Fact]
    public void Limpiar_la_pantalla_la_deja_vacia()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "basura\r\nmás basura");

        Write(emu, "\x1b[2J\x1b[H");

        Assert.Equal(string.Empty, Linea(buf, 0));
        Assert.Equal(string.Empty, Linea(buf, 1));
        Assert.Equal(0, buf.CursorX);
    }

    [Fact]
    public void Borrar_hasta_el_final_de_la_linea_respeta_lo_anterior()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "abcdefghij");

        Write(emu, "\x1b[1;4H\x1b[K");

        Assert.Equal("abc", Linea(buf, 0));
    }

    [Fact]
    public void El_desplazamiento_manda_la_linea_saliente_al_historial()
    {
        var (emu, buf) = Nuevo(80, 3);

        Write(emu, "uno\r\ndos\r\ntres\r\ncuatro");

        Assert.Single(buf.Scrollback);
        Assert.Equal("cuatro", Linea(buf, 2));
    }

    [Fact]
    public void El_historial_respeta_su_limite()
    {
        var buffer = new TerminalBuffer(80, 3, scrollbackLimit: 5);
        var emu = new VtEmulator(buffer);

        for (var i = 0; i < 40; i++)
        {
            Write(emu, $"linea {i}\r\n");
        }

        Assert.Equal(5, buffer.Scrollback.Count);
    }

    [Fact]
    public void La_pantalla_alternativa_preserva_la_principal()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "prompt del shell");

        Write(emu, "\x1b[?1049h");
        Write(emu, "\x1b[2J\x1b[Hcontenido de vim");

        Assert.Equal("contenido de vim", emu.Buffer.LineText(0));

        Write(emu, "\x1b[?1049l");

        Assert.Equal("prompt del shell", emu.Buffer.LineText(0));
        Assert.Same(buf, emu.Buffer);
    }

    /// <summary>Modo 2004: el servidor avisa que quiere el texto pegado marcado (FR-030e).</summary>
    [Fact]
    public void El_modo_2004_se_enciende_y_se_apaga()
    {
        var (emu, _) = Nuevo();

        Assert.False(emu.BracketedPaste);

        Write(emu, "\x1b[?2004h");
        Assert.True(emu.BracketedPaste);

        Write(emu, "\x1b[?2004l");
        Assert.False(emu.BracketedPaste);
    }

    [Fact]
    public void La_pantalla_alternativa_no_altera_el_modo_2004()
    {
        var (emu, _) = Nuevo();

        Write(emu, "\x1b[?2004h");
        Write(emu, "\x1b[?1049h");

        Assert.True(emu.BracketedPaste);

        Write(emu, "\x1b[?1049l");

        Assert.True(emu.BracketedPaste);
    }

    [Fact]
    public void Restablecer_apaga_el_modo_2004()
    {
        var (emu, _) = Nuevo();

        Write(emu, "\x1b[?2004h");
        emu.Reset();

        Assert.False(emu.BracketedPaste);
    }

    [Fact]
    public void La_region_de_desplazamiento_acota_el_scroll()
    {
        var (emu, buf) = Nuevo(80, 10);

        Write(emu, "\x1b[1;1Hfija");
        Write(emu, "\x1b[2;5r");
        Write(emu, "\x1b[5;1Ha\r\nb\r\nc");

        Assert.Equal("fija", Linea(buf, 0));
    }

    [Fact]
    public void Insertar_y_borrar_lineas_funciona()
    {
        var (emu, buf) = Nuevo(80, 5);
        Write(emu, "uno\r\ndos\r\ntres");

        Write(emu, "\x1b[2;1H\x1b[L");

        Assert.Equal("uno", Linea(buf, 0));
        Assert.Equal(string.Empty, Linea(buf, 1));
        Assert.Equal("dos", Linea(buf, 2));
    }

    [Fact]
    public void Borrar_caracteres_corre_el_resto_de_la_linea()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "abcdef");

        Write(emu, "\x1b[1;2H\x1b[2P");

        Assert.Equal("adef", Linea(buf, 0));
    }

    [Fact]
    public void Ocultar_y_mostrar_el_cursor()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b[?25l");
        Assert.False(buf.CursorVisible);

        Write(emu, "\x1b[?25h");
        Assert.True(buf.CursorVisible);
    }

    [Fact]
    public void El_modo_de_cursor_de_aplicacion_se_registra()
    {
        var (emu, _) = Nuevo();

        Write(emu, "\x1b[?1h");
        Assert.True(emu.ApplicationCursorKeys);

        Write(emu, "\x1b[?1l");
        Assert.False(emu.ApplicationCursorKeys);
    }

    [Fact]
    public void El_titulo_de_la_ventana_se_captura()
    {
        var (emu, _) = Nuevo();

        Write(emu, "\x1b]0;usuario@servidor: ~\a");

        Assert.Equal("usuario@servidor: ~", emu.Title);
    }

    /// <remarks>Una secuencia OSC puede terminar en BEL o en ST; ST son dos bytes, ESC y barra invertida.</remarks>
    [Fact]
    public void El_titulo_terminado_en_ST_no_deja_la_barra_impresa()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b]0;usuario@servidor: ~\x1b\\");
        Write(emu, "hola");

        Assert.Equal("usuario@servidor: ~", emu.Title);
        Assert.Equal("hola", Linea(buf, 0).TrimEnd());
    }

    [Fact]
    public void El_informe_de_posicion_del_cursor_se_responde()
    {
        var (emu, _) = Nuevo();
        byte[]? respuesta = null;
        emu.ResponseRequested += (_, r) => respuesta = r;

        Write(emu, "\x1b[3;7H\x1b[6n");

        Assert.NotNull(respuesta);
        Assert.Equal("\x1b[3;7R", Encoding.ASCII.GetString(respuesta));
    }

    [Fact]
    public void La_tabulacion_avanza_de_a_ocho_columnas()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "ab\tc");

        Assert.Equal(9, buf.CursorX);
    }

    [Fact]
    public void El_texto_que_pasa_el_ancho_sigue_en_la_linea_siguiente()
    {
        var (emu, buf) = Nuevo(10, 5);

        Write(emu, "0123456789ABC");

        Assert.Equal("0123456789", Linea(buf, 0));
        Assert.Equal("ABC", Linea(buf, 1));
    }

    [Fact]
    public void Una_secuencia_desconocida_no_rompe_ni_se_imprime()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "antes\x1b[999;999Zdespués");

        Assert.Contains("antes", Linea(buf, 0), StringComparison.Ordinal);
        Assert.DoesNotContain("999", Linea(buf, 0), StringComparison.Ordinal);
    }

    [Fact]
    public void Redimensionar_conserva_el_contenido_que_entra()
    {
        var (emu, buf) = Nuevo(80, 24);
        Write(emu, "primera linea\r\nsegunda linea");

        buf.Resize(40, 10);

        Assert.Equal("primera linea", Linea(buf, 0));
        Assert.Equal("segunda linea", Linea(buf, 1));
        Assert.Equal(40, buf.Columns);
        Assert.Equal(10, buf.Rows);
    }

    [Fact]
    public void Una_salida_con_colores_tipo_ls_se_interpreta_completa()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b[0m\x1b[01;34mdocs\x1b[0m  \x1b[01;32mscript.sh\x1b[0m\r\n");

        Assert.Contains("docs", Linea(buf, 0), StringComparison.Ordinal);
        Assert.Contains("script.sh", Linea(buf, 0), StringComparison.Ordinal);
        Assert.Equal(4, buf.At(0, 0).Foreground);   // azul
        Assert.Equal(2, buf.At(0, 6).Foreground);   // verde
    }

    [Fact]
    public void DECALN_llena_la_pantalla_de_E()
    {
        var (emu, buf) = Nuevo(5, 3);

        Write(emu, "\x1b#8");

        Assert.Equal("EEEEE", Linea(buf, 0));
        Assert.Equal("EEEEE", Linea(buf, 1));
        Assert.Equal("EEEEE", Linea(buf, 2));
    }

    [Fact]
    public void DECALN_no_deja_el_8_ni_el_numeral_impresos()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b#8");

        Assert.DoesNotContain('8', Linea(buf, 0));
        Assert.DoesNotContain('#', Linea(buf, 0));
    }

    [Fact]
    public void Un_final_de_numeral_desconocido_no_se_imprime()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b#3hola");

        Assert.Equal("hola", Linea(buf, 0));
    }

    [Fact]
    public void DECKPAM_activa_el_teclado_numerico_de_aplicacion_sin_imprimir_nada()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b=");

        Assert.True(emu.TecladoNumericoEnModoAplicacion);
        Assert.Equal(string.Empty, Linea(buf, 0));
    }

    [Fact]
    public void DECKPNM_vuelve_el_teclado_numerico_a_modo_normal()
    {
        var (emu, buf) = Nuevo();

        Write(emu, "\x1b=\x1b>");

        Assert.False(emu.TecladoNumericoEnModoAplicacion);
        Assert.Equal(string.Empty, Linea(buf, 0));
    }

    [Fact]
    public void Restablecer_apaga_el_teclado_numerico_de_aplicacion()
    {
        var (emu, _) = Nuevo();

        Write(emu, "\x1b=");
        emu.Reset();

        Assert.False(emu.TecladoNumericoEnModoAplicacion);
    }

    [Theory]
    [InlineData("1000")]
    [InlineData("1002")]
    [InlineData("1006")]
    public void Activar_el_reporte_de_mouse_no_ensucia_la_pantalla(string modo)
    {
        var (emu, buf) = Nuevo();

        Write(emu, $"x\x1b[?{modo}hy");

        Assert.Equal("xy", Linea(buf, 0));
    }

    [Fact]
    public void Los_modos_de_reporte_de_mouse_se_registran_por_separado()
    {
        var (emu, _) = Nuevo();

        Write(emu, "\x1b[?1000h\x1b[?1002h\x1b[?1006h");
        Assert.True(emu.MouseTrackingNormal);
        Assert.True(emu.MouseTrackingButtonEvent);
        Assert.True(emu.MouseTrackingSgr);

        Write(emu, "\x1b[?1000l\x1b[?1002l\x1b[?1006l");
        Assert.False(emu.MouseTrackingNormal);
        Assert.False(emu.MouseTrackingButtonEvent);
        Assert.False(emu.MouseTrackingSgr);
    }
}

public class KeyboardMapperTests
{
    [Theory]
    [InlineData(Keys.Up, "\x1b[A")]
    [InlineData(Keys.Down, "\x1b[B")]
    [InlineData(Keys.Right, "\x1b[C")]
    [InlineData(Keys.Left, "\x1b[D")]
    public void Las_flechas_en_modo_normal(Keys tecla, string esperado)
    {
        var bytes = KeyboardMapper.Map(tecla, false, false, false, applicationCursor: false);

        Assert.Equal(esperado, Encoding.ASCII.GetString(bytes!));
    }

    [Fact]
    public void Las_flechas_cambian_de_prefijo_en_modo_aplicacion()
    {
        var bytes = KeyboardMapper.Map(Keys.Up, false, false, false, applicationCursor: true);

        Assert.Equal("\x1bOA", Encoding.ASCII.GetString(bytes!));
    }

    [Fact]
    public void Ctrl_C_manda_el_caracter_de_interrupcion()
    {
        var bytes = KeyboardMapper.Map(Keys.C, control: true, false, false, false);

        Assert.Equal([3], bytes);
    }

    [Fact]
    public void Ctrl_D_manda_fin_de_transmision()
    {
        var bytes = KeyboardMapper.Map(Keys.D, control: true, false, false, false);

        Assert.Equal([4], bytes);
    }

    [Fact]
    public void La_tecla_de_borrar_manda_DEL()
    {
        var bytes = KeyboardMapper.Map(Keys.Back, false, false, false, false);

        Assert.Equal([0x7f], bytes);
    }

    [Theory]
    [InlineData(Keys.F1, "\x1bOP")]
    [InlineData(Keys.F5, "\x1b[15~")]
    [InlineData(Keys.F10, "\x1b[21~")]
    public void Las_teclas_de_funcion(Keys tecla, string esperado)
    {
        var bytes = KeyboardMapper.Map(tecla, false, false, false, false);

        Assert.Equal(esperado, Encoding.ASCII.GetString(bytes!));
    }

    [Fact]
    public void Alt_antepone_escape_como_meta()
    {
        var bytes = KeyboardMapper.MapText('b', alt: true);

        Assert.Equal(0x1b, bytes[0]);
        Assert.Equal((byte)'b', bytes[1]);
    }

    [Fact]
    public void El_texto_normal_viaja_en_utf8()
    {
        var bytes = KeyboardMapper.MapText('ñ', alt: false);

        Assert.Equal("ñ", Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [InlineData(Keys.NumPad0, "\x1bOp")]
    [InlineData(Keys.NumPad1, "\x1bOq")]
    [InlineData(Keys.NumPad2, "\x1bOr")]
    [InlineData(Keys.NumPad3, "\x1bOs")]
    [InlineData(Keys.NumPad4, "\x1bOt")]
    [InlineData(Keys.NumPad5, "\x1bOu")]
    [InlineData(Keys.NumPad6, "\x1bOv")]
    [InlineData(Keys.NumPad7, "\x1bOw")]
    [InlineData(Keys.NumPad8, "\x1bOx")]
    [InlineData(Keys.NumPad9, "\x1bOy")]
    [InlineData(Keys.Decimal, "\x1bOn")]
    [InlineData(Keys.Add, "\x1bOk")]
    [InlineData(Keys.Subtract, "\x1bOm")]
    [InlineData(Keys.Multiply, "\x1bOj")]
    [InlineData(Keys.Divide, "\x1bOo")]
    public void El_teclado_numerico_en_modo_aplicacion(Keys tecla, string esperado)
    {
        var bytes = KeyboardMapper.Map(tecla, false, false, false, false, applicationKeypad: true);

        Assert.Equal(esperado, Encoding.ASCII.GetString(bytes!));
    }

    [Theory]
    [InlineData(Keys.NumPad0)]
    [InlineData(Keys.NumPad5)]
    [InlineData(Keys.NumPad9)]
    [InlineData(Keys.Decimal)]
    [InlineData(Keys.Add)]
    [InlineData(Keys.Subtract)]
    [InlineData(Keys.Multiply)]
    [InlineData(Keys.Divide)]
    public void El_teclado_numerico_en_modo_normal_deja_pasar_el_caracter(Keys tecla)
    {
        Assert.Null(KeyboardMapper.Map(tecla, false, false, false, false));
    }

    [Fact]
    public void Alt_con_el_numerico_no_manda_secuencia_para_no_romper_los_codigos_alt()
    {
        Assert.Null(KeyboardMapper.Map(Keys.NumPad1, false, alt: true, false, false, applicationKeypad: true));
    }

    [Fact]
    public void Control_con_el_numerico_no_manda_secuencia_y_deja_el_atajo_de_zoom()
    {
        Assert.Null(KeyboardMapper.Map(Keys.NumPad0, control: true, false, false, false, applicationKeypad: true));
    }

    [Theory]
    [InlineData(Keys.Up, "\x1b[A")]
    [InlineData(Keys.Home, "\x1b[H")]
    [InlineData(Keys.End, "\x1b[F")]
    [InlineData(Keys.Insert, "\x1b[2~")]
    [InlineData(Keys.Delete, "\x1b[3~")]
    [InlineData(Keys.PageDown, "\x1b[6~")]
    public void Con_Bloq_Num_apagado_la_navegacion_no_cambia(Keys tecla, string esperado)
    {
        var bytes = KeyboardMapper.Map(tecla, false, false, false, false, applicationKeypad: true);

        Assert.Equal(esperado, Encoding.ASCII.GetString(bytes!));
    }

    [Fact]
    public void El_modo_del_numerico_es_independiente_del_modo_del_cursor()
    {
        var flecha = KeyboardMapper.Map(Keys.Up, false, false, false, applicationCursor: true, applicationKeypad: false);
        var digito = KeyboardMapper.Map(Keys.NumPad7, false, false, false, applicationCursor: false, applicationKeypad: true);

        Assert.Equal("\x1bOA", Encoding.ASCII.GetString(flecha!));
        Assert.Equal("\x1bOw", Encoding.ASCII.GetString(digito!));
    }

    [Fact]
    public void El_Enter_del_numerico_manda_retorno_como_el_Enter_principal()
    {
        var bytes = KeyboardMapper.Map(Keys.Enter, false, false, false, false, applicationKeypad: true);

        Assert.Equal("\r", Encoding.ASCII.GetString(bytes!));
    }
}

public class TecladoNumericoDelEmuladorTests
{
    private static VtEmulator Nuevo() => new(new TerminalBuffer(80, 24));

    private static void Write(VtEmulator emu, string texto) => emu.Write(Encoding.UTF8.GetBytes(texto));

    private static string? Digito(VtEmulator emu)
    {
        var bytes = KeyboardMapper.Map(
            Keys.NumPad3, false, false, false, emu.ApplicationCursorKeys, emu.TecladoNumericoEnModoAplicacion);

        return bytes is null ? null : Encoding.ASCII.GetString(bytes);
    }

    [Fact]
    public void DECKPAM_hace_que_el_numerico_mande_la_secuencia_SS3()
    {
        var emu = Nuevo();

        Write(emu, "\x1b=");

        Assert.Equal("\x1bOs", Digito(emu));
    }

    [Fact]
    public void DECKPNM_devuelve_el_numerico_al_caracter_suelto()
    {
        var emu = Nuevo();

        Write(emu, "\x1b=\x1b>");

        Assert.Null(Digito(emu));
    }

    [Fact]
    public void Restablecer_devuelve_el_numerico_al_caracter_suelto()
    {
        var emu = Nuevo();

        Write(emu, "\x1b=");
        emu.Reset();

        Assert.Null(Digito(emu));
    }
}
