using System.Windows.Forms;
using CafManagerConection.Terminal;

using Accion = CafManagerConection.Terminal.TerminalControl.AccionDeTeclado;

namespace CafManagerConection.Terminal.Tests;

/// <summary>Qué teclas se queda la aplicación y cuáles van al servidor (FR-030c, FR-032, FR-155).</summary>
public sealed class AtajosDePortapapelesTests
{
    private static Accion Decidir(Keys tecla, bool control = false, bool shift = false, bool alt = false) =>
        TerminalControl.DecidirTeclado(tecla, control, shift, alt);

    [Fact]
    public void Ctrl_C_siempre_va_al_servidor() =>
        Assert.Equal(Accion.AlServidor, Decidir(Keys.C, control: true));

    [Fact]
    public void Ctrl_C_manda_la_interrupcion()
    {
        var bytes = KeyboardMapper.Map(Keys.C, control: true, alt: false, shift: false, applicationCursor: false);

        Assert.Equal([0x03], bytes);
    }

    [Theory]
    [InlineData(Keys.Insert, true, false)]
    [InlineData(Keys.C, true, true)]
    public void Los_dos_atajos_de_copiar(Keys tecla, bool control, bool shift) =>
        Assert.Equal(Accion.Copiar, Decidir(tecla, control, shift));

    [Theory]
    [InlineData(Keys.Insert, false, true)]
    [InlineData(Keys.V, true, true)]
    public void Los_dos_atajos_de_pegar(Keys tecla, bool control, bool shift) =>
        Assert.Equal(Accion.Pegar, Decidir(tecla, control, shift));

    [Fact]
    public void Insert_sola_va_al_servidor() =>
        Assert.Equal(Accion.AlServidor, Decidir(Keys.Insert));

    [Theory]
    [InlineData(Keys.PageUp, false, true, (int)Accion.HistorialPaginaArriba)]
    [InlineData(Keys.PageDown, false, true, (int)Accion.HistorialPaginaAbajo)]
    [InlineData(Keys.PageUp, true, false, (int)Accion.HistorialLineaArriba)]
    [InlineData(Keys.PageDown, true, false, (int)Accion.HistorialLineaAbajo)]
    [InlineData(Keys.PageUp, true, true, (int)Accion.HistorialAlPrincipio)]
    [InlineData(Keys.PageDown, true, true, (int)Accion.HistorialAlFinal)]
    [InlineData(Keys.Home, true, true, (int)Accion.HistorialAlPrincipio)]
    [InlineData(Keys.End, true, true, (int)Accion.HistorialAlFinal)]
    public void El_historial_por_teclado(Keys tecla, bool control, bool shift, int esperada) =>
        Assert.Equal((Accion)esperada, Decidir(tecla, control, shift));

    [Theory]
    [InlineData(Keys.Home)]
    [InlineData(Keys.End)]
    public void Ctrl_sin_Shift_en_Inicio_y_Fin_va_al_servidor(Keys tecla) =>
        Assert.Equal(Accion.AlServidor, Decidir(tecla, control: true));

    /// <summary>La lista de FR-032 es cerrada: todo lo que no está reservado llega al servidor.</summary>
    [Theory]
    [InlineData(Keys.A, true, false)]
    [InlineData(Keys.E, true, false)]
    [InlineData(Keys.R, true, false)]
    [InlineData(Keys.D, true, false)]
    [InlineData(Keys.Z, true, false)]
    [InlineData(Keys.L, true, false)]
    [InlineData(Keys.P, true, false)]
    [InlineData(Keys.F, false, false)]
    [InlineData(Keys.F5, false, false)]
    [InlineData(Keys.Up, false, false)]
    [InlineData(Keys.Tab, false, true)]
    [InlineData(Keys.Escape, false, false)]
    public void Lo_que_no_esta_reservado_va_al_servidor(Keys tecla, bool control, bool shift) =>
        Assert.Equal(Accion.AlServidor, Decidir(tecla, control, shift));

    [Theory]
    [InlineData(Keys.C)]
    [InlineData(Keys.V)]
    [InlineData(Keys.Insert)]
    [InlineData(Keys.F12)]
    public void Con_Alt_nada_se_reserva(Keys tecla) =>
        Assert.Equal(Accion.AlServidor, Decidir(tecla, control: true, shift: true, alt: true));

    [Fact]
    public void F12_sola_es_el_diagnostico() =>
        Assert.Equal(Accion.Diagnostico, Decidir(Keys.F12));

    [Fact]
    public void F12_con_modificadoras_va_al_servidor() =>
        Assert.Equal(Accion.AlServidor, Decidir(Keys.F12, control: true));

    [Fact]
    public void Ctrl_F_abre_la_busqueda() =>
        Assert.Equal(Accion.Buscar, Decidir(Keys.F, control: true));

    [Fact]
    public void Ctrl_Shift_A_selecciona_lo_visible() =>
        Assert.Equal(Accion.SeleccionarLoVisible, Decidir(Keys.A, control: true, shift: true));

    [Fact]
    public void Ctrl_Shift_P_abre_la_paleta() =>
        Assert.Equal(Accion.Paleta, Decidir(Keys.P, control: true, shift: true));

    [Theory]
    [InlineData(Keys.Add, (int)Accion.ZoomMas)]
    [InlineData(Keys.Oemplus, (int)Accion.ZoomMas)]
    [InlineData(Keys.Subtract, (int)Accion.ZoomMenos)]
    [InlineData(Keys.OemMinus, (int)Accion.ZoomMenos)]
    [InlineData(Keys.D0, (int)Accion.ZoomDeOrigen)]
    [InlineData(Keys.NumPad0, (int)Accion.ZoomDeOrigen)]
    public void El_zoom_con_Ctrl(Keys tecla, int esperada) =>
        Assert.Equal((Accion)esperada, Decidir(tecla, control: true));
}
