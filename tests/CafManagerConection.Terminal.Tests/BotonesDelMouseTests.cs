using System.Windows.Forms;
using CafManagerConection.Terminal;

using Accion = CafManagerConection.Terminal.TerminalControl.AccionDeMouse;

namespace CafManagerConection.Terminal.Tests;

/// <summary>El modelo de botones de PuTTY, modo «Compromise» (FR-030b, FR-030d).</summary>
public sealed class BotonesDelMouseTests
{
    private static Accion Decidir(
        MouseButtons boton, bool shift = false, bool control = false, int clics = 1) =>
        TerminalControl.DecidirMouse(boton, shift, control, clics);

    [Fact]
    public void El_derecho_pega() =>
        Assert.Equal(Accion.Pegar, Decidir(MouseButtons.Right));

    [Fact]
    public void Ctrl_y_el_derecho_abren_el_menu() =>
        Assert.Equal(Accion.Menu, Decidir(MouseButtons.Right, control: true));

    [Fact]
    public void El_medio_extiende_la_seleccion() =>
        Assert.Equal(Accion.ExtenderSeleccion, Decidir(MouseButtons.Middle));

    [Fact]
    public void El_izquierdo_empieza_una_seleccion() =>
        Assert.Equal(Accion.EmpezarSeleccion, Decidir(MouseButtons.Left));

    [Fact]
    public void Shift_y_el_izquierdo_extienden() =>
        Assert.Equal(Accion.ExtenderSeleccion, Decidir(MouseButtons.Left, shift: true));

    [Fact]
    public void Doble_clic_toma_la_palabra() =>
        Assert.Equal(Accion.SeleccionarPalabra, Decidir(MouseButtons.Left, clics: 2));

    [Fact]
    public void Triple_clic_toma_la_linea() =>
        Assert.Equal(Accion.SeleccionarLinea, Decidir(MouseButtons.Left, clics: 3));

    [Fact]
    public void Cuatro_clics_siguen_siendo_la_linea() =>
        Assert.Equal(Accion.SeleccionarLinea, Decidir(MouseButtons.Left, clics: 4));

    [Fact]
    public void Con_Shift_los_clics_repetidos_siguen_extendiendo() =>
        Assert.Equal(Accion.ExtenderSeleccion, Decidir(MouseButtons.Left, shift: true, clics: 3));

    /// <summary>Ctrl sobre el izquierdo es la selección rectangular (FR-154d).</summary>
    [Fact]
    public void Ctrl_y_el_izquierdo_siguen_empezando_una_seleccion() =>
        Assert.Equal(Accion.EmpezarSeleccion, Decidir(MouseButtons.Left, control: true));

    [Fact]
    public void Los_botones_de_los_costados_no_hacen_nada() =>
        Assert.Equal(Accion.Ninguna, Decidir(MouseButtons.XButton1));
}
