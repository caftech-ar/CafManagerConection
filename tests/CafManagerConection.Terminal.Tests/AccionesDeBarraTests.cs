using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

public sealed class AccionesDeBarraTests
{
    private static T EnSta<T>(Func<TerminalControl, T> accion)
    {
        T? resultado = default;
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                using var terminal = new TerminalControl();
                terminal.ApplyTheme(dark: true, "Consolas", 10, scrollback: 100);
                terminal.Size = new System.Drawing.Size(400, 100);

                var sb = new StringBuilder();

                for (var i = 1; i <= 10; i++)
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
    public void Copiar_todo_trae_el_historial_y_la_pantalla()
    {
        var texto = EnSta(t => t.TextoCompleto);

        for (var i = 1; i <= 10; i++)
        {
            Assert.Contains($"linea{i}", texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Las_lineas_salen_en_orden()
    {
        var texto = EnSta(t => t.TextoCompleto);

        var posiciones = Enumerable.Range(1, 10)
            .Select(i => texto.IndexOf($"linea{i}", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(-1, posiciones);
        Assert.Equal(posiciones.Order(), posiciones);
    }

    [Fact]
    public void No_se_copian_espacios_ni_renglones_de_relleno()
    {
        var texto = EnSta(t => t.TextoCompleto);
        var lineas = texto.Split('\n');

        Assert.All(lineas, linea => Assert.DoesNotContain("  ", linea.TrimEnd('\r')));
        Assert.EndsWith("linea10" + Environment.NewLine, texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Antes_de_borrar_hay_historial() =>
        Assert.True(EnSta(t => t.LineasDeHistorial) > 0);

    [Fact]
    public void Borrar_el_historial_lo_deja_en_cero()
    {
        var quedan = EnSta(t =>
        {
            t.BorrarHistorial();
            return t.LineasDeHistorial;
        });

        Assert.Equal(0, quedan);
    }

    [Fact]
    public void Borrar_el_historial_deja_la_pantalla()
    {
        var texto = EnSta(t =>
        {
            t.BorrarHistorial();
            return t.TextoCompleto;
        });

        Assert.Contains("linea10", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("linea1\n", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Borrar_el_historial_con_todo_seleccionado_y_desplazado_no_revienta()
    {
        var texto = EnSta(t =>
        {
            t.ScrollBy(5);
            t.SelectAll();
            t.BorrarHistorial();

            return t.SelectedText;
        });

        Assert.Empty(texto);
    }

    [Fact]
    public void Restablecer_limpia_la_pantalla()
    {
        var texto = EnSta(t =>
        {
            t.Restablecer();
            t.SelectAll();

            return t.SelectedText;
        });

        Assert.DoesNotContain("linea", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Restablecer_no_toca_el_historial()
    {
        var (antes, despues) = EnSta(t =>
        {
            var a = t.LineasDeHistorial;
            t.Restablecer();
            return (a, t.LineasDeHistorial);
        });

        Assert.True(antes > 0);
        Assert.Equal(antes, despues);
    }

    [Fact]
    public void Restablecer_deja_el_terminal_usable()
    {
        var texto = EnSta(t =>
        {
            t.Restablecer();
            t.Write(Encoding.UTF8.GetBytes("despues\r\n"));

            return t.TextoCompleto;
        });

        Assert.Contains("despues", texto, StringComparison.Ordinal);
    }
}
