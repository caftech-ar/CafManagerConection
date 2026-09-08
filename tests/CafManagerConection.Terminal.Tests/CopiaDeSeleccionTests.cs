using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

public sealed class CopiaDeSeleccionTests
{
    private static string EnSta(Func<TerminalControl, string> accion)
    {
        string? resultado = null;
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

        return resultado ?? string.Empty;
    }

    [Fact]
    public void Sin_seleccion_no_hay_texto() =>
        Assert.Empty(EnSta(t => t.SelectedText));

    [Fact]
    public void Seleccionar_lo_visible_trae_las_lineas_de_pantalla()
    {
        var texto = EnSta(t =>
        {
            t.SelectAll();
            return t.SelectedText;
        });

        Assert.NotEmpty(texto);

        foreach (var linea in texto.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.StartsWith("linea", linea.Trim(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Desplazado_hacia_arriba_se_copia_lo_que_se_ve()
    {
        var (sinDesplazar, desplazado) = EnStaDoble();

        Assert.NotEmpty(sinDesplazar);
        Assert.NotEmpty(desplazado);

        Assert.NotEqual(sinDesplazar, desplazado);
    }

    [Fact]
    public void Al_desplazarse_aparecen_lineas_anteriores()
    {
        var (sinDesplazar, desplazado) = EnStaDoble();

        var primeraSin = sinDesplazar.Split('\n')[0].Trim();
        var primeraCon = desplazado.Split('\n')[0].Trim();

        var numeroSin = int.Parse(primeraSin.Replace("linea", string.Empty));
        var numeroCon = int.Parse(primeraCon.Replace("linea", string.Empty));

        Assert.True(
            numeroCon < numeroSin,
            $"Desplazado hacia atrás empieza en {primeraCon} y sin desplazar en {primeraSin}: "
            + "no se está mostrando historial.");
    }

    [Fact]
    public void Las_lineas_copiadas_no_arrastran_espacios_de_relleno()
    {
        var texto = EnSta(t =>
        {
            t.SelectAll();
            return t.SelectedText;
        });

        foreach (var linea in texto.Split('\n'))
        {
            Assert.DoesNotContain("  ", linea);
        }
    }

    [Fact]
    public void La_seleccion_sobre_historial_recortado_sigue_dando_el_texto_exacto()
    {
        string? resultado = null;
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                using var terminal = new TerminalControl();
                terminal.ApplyTheme(dark: true, "Consolas", 10, scrollback: 100);

                terminal.Size = new System.Drawing.Size(2000, 100);

                var sb = new StringBuilder();
                for (var i = 1; i <= 10; i++)
                {
                    sb.Append($"linea{i}\r\n");
                }

                terminal.Write(Encoding.UTF8.GetBytes(sb.ToString()));

                Assert.True(terminal.Columns > 50, $"la grilla dio {terminal.Columns} columnas.");
                Assert.True(terminal.LineasDeHistorial > 0);

                terminal.ScrollBy(int.MaxValue / 2);
                terminal.SelectAll();
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

        var lineas = resultado!.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.NotEmpty(lineas);

        foreach (var linea in lineas)
        {
            var recortada = linea.Trim();
            Assert.Matches("^linea[0-9]+$", recortada);
        }
    }

    private static (string SinDesplazar, string Desplazado) EnStaDoble()
    {
        var sin = EnSta(t =>
        {
            t.SelectAll();
            return t.SelectedText;
        });

        var con = EnSta(t =>
        {
            t.ScrollBy(3);
            t.SelectAll();
            return t.SelectedText;
        });

        return (sin, con);
    }
}
