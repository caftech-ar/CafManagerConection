using System.Text;

namespace CafManagerConection.Terminal.Tests;

public sealed class SeleccionAlDesplazarTests
{
    private static string ConSeleccion(Action<TerminalControl> desplazar)
    {
        string? resultado = null;
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                using var terminal = new TerminalControl();
                terminal.Size = new System.Drawing.Size(400, 200);
                terminal.ApplyTheme(dark: true, "Consolas", 10, scrollback: 500);

                var sb = new StringBuilder();

                for (var i = 1; i <= 200; i++)
                {
                    sb.Append($"linea{i}\r\n");
                }

                terminal.Write(Encoding.UTF8.GetBytes(sb.ToString()));

                terminal.SelectAll();
                Assert.NotEqual(string.Empty, terminal.SelectedText.Trim());

                desplazar(terminal);

                resultado = terminal.SelectedText;
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

        return resultado!;
    }

    [Fact]
    public void La_rueda_y_el_teclado_sueltan_la_seleccion() =>
        Assert.Equal(string.Empty, ConSeleccion(t => t.DesplazarElHistorial(3)).Trim());

    [Fact]
    public void Bajar_el_historial_tambien_la_suelta() =>
        Assert.Equal(string.Empty, ConSeleccion(t => t.DesplazarElHistorial(-3)).Trim());

    [Fact]
    public void El_pulgar_de_la_barra_suelta_la_seleccion() =>
        Assert.Equal(string.Empty, ConSeleccion(t => t.DesplazarPorPulgar(20)).Trim());

    // AcompanarSeleccion desplaza por ScrollBy mientras se arrastra: si ScrollBy soltara, no se podría
    // extender la selección más allá del borde.
    [Fact]
    public void Desplazar_para_extender_la_seleccion_la_mantiene() =>
        Assert.NotEqual(string.Empty, ConSeleccion(t => t.ScrollBy(3)).Trim());
}
