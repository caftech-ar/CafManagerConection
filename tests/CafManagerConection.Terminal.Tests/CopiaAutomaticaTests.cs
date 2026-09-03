using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

/// <summary>Al soltar la selección, el texto ya está en el portapapeles (FR-030a).</summary>
public sealed class CopiaAutomaticaTests
{
    private static string? Copiado(Action<TerminalControl> accion)
    {
        string? copiado = null;
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                using var terminal = new TerminalControl();
                terminal.ApplyTheme(dark: true, "Consolas", 10, scrollback: 100);
                terminal.Size = new System.Drawing.Size(400, 100);
                terminal.EscribirEnPortapapeles = texto => copiado = texto;

                var sb = new StringBuilder();
                for (var i = 1; i <= 10; i++)
                {
                    sb.Append($"linea{i}\r\n");
                }

                terminal.Write(Encoding.UTF8.GetBytes(sb.ToString()));

                accion(terminal);
            }
            catch (Exception ex)
            {
                fallo = ex;
            }
        });

        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();

        Assert.True(hilo.Join(TimeSpan.FromSeconds(30)), "El hilo STA no terminó.");

        if (fallo is not null)
        {
            throw new Xunit.Sdk.XunitException($"{fallo.GetType().Name}: {fallo.Message}");
        }

        return copiado;
    }

    [Fact]
    public void Al_terminar_una_seleccion_el_texto_queda_copiado()
    {
        var copiado = Copiado(t =>
        {
            t.SelectAll();
            t.TerminarSeleccion();
        });

        Assert.False(string.IsNullOrEmpty(copiado), "no se copió nada al soltar.");
        Assert.Contains("linea", copiado, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_seleccion_no_se_copia_nada() =>
        Assert.Null(Copiado(t => t.TerminarSeleccion()));

    [Fact]
    public void Lo_copiado_es_lo_que_estaba_seleccionado()
    {
        string? seleccionado = null;

        var copiado = Copiado(t =>
        {
            t.SelectAll();
            seleccionado = t.SelectedText;
            t.TerminarSeleccion();
        });

        Assert.Equal(seleccionado, copiado);
    }

    [Fact]
    public void Si_el_portapapeles_falla_el_terminal_sigue_vivo()
    {
        var excepcion = Record.Exception(() => Copiado(t =>
        {
            t.EscribirEnPortapapeles = _ =>
                throw new System.Runtime.InteropServices.ExternalException("tomado");

            t.SelectAll();
            t.TerminarSeleccion();
        }));

        Assert.Null(excepcion);
    }
}
