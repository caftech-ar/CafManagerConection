using System.Text;

namespace CafManagerConection.Terminal.Tests;

public sealed class SeleccionAncladaTests
{
    private static T EnSta<T>(int lineas, int scrollback, Func<TerminalControl, T> accion)
    {
        T? resultado = default;
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                using var terminal = new TerminalControl();
                terminal.Size = new System.Drawing.Size(400, 200);
                terminal.ApplyTheme(dark: true, "Consolas", 10, scrollback);

                var sb = new StringBuilder();

                for (var i = 1; i <= lineas; i++)
                {
                    sb.Append($"linea{i}\r\n");
                }

                terminal.Write(Encoding.UTF8.GetBytes(sb.ToString()));

                resultado = accion(terminal);
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
    public void La_seleccion_sigue_al_texto_cuando_el_historial_se_desplaza()
    {
        var (antes, despues) = EnSta(200, 500, t =>
        {
            t.SelectAll();
            var antes = t.SelectedText;

            // ScrollBy no suelta la selección: es el camino que usa el arrastre más allá del borde.
            t.ScrollBy(5);

            return (antes, t.SelectedText);
        });

        Assert.NotEqual(string.Empty, antes.Trim());
        Assert.Equal(antes, despues);
    }

    [Fact]
    public void Desplazarse_no_corre_la_seleccion_a_otro_texto()
    {
        var texto = EnSta(200, 500, t =>
        {
            t.SelectAll();
            t.ScrollBy(20);

            return t.SelectedText;
        });

        // Lo seleccionado era el final; tras desplazar veinte líneas sigue siendo el final.
        Assert.Contains("linea200", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Lo_que_el_historial_descarta_recorta_la_seleccion()
    {
        var quedo = EnSta(60, 40, t =>
        {
            t.SelectAll();
            Assert.NotEqual(string.Empty, t.SelectedText.Trim());

            var sb = new StringBuilder();

            for (var i = 1; i <= 400; i++)
            {
                sb.Append($"nueva{i}\r\n");
            }

            t.Write(Encoding.UTF8.GetBytes(sb.ToString()));

            return t.SelectedText;
        });

        Assert.DoesNotContain("linea", quedo, StringComparison.Ordinal);
    }

    [Fact]
    public void La_rueda_y_el_teclado_siguen_soltando_la_seleccion()
    {
        var texto = EnSta(200, 500, t =>
        {
            t.SelectAll();
            t.DesplazarElHistorial(3);

            return t.SelectedText;
        });

        Assert.Equal(string.Empty, texto.Trim());
    }

    [Fact]
    public void Seleccionar_lo_visible_sigue_copiando_lo_visible()
    {
        var texto = EnSta(200, 500, t =>
        {
            t.SelectAll();
            return t.SelectedText;
        });

        Assert.Contains("linea200", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("linea1\r", texto, StringComparison.Ordinal);
    }
}
