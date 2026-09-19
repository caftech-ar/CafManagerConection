using System.Text;

namespace CafManagerConection.Terminal.Tests;

public sealed class LineaArchivadaTests
{
    private static List<string> Capturar(string entrada)
    {
        var archivadas = new List<string>();
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                using var terminal = new TerminalControl();
                terminal.Size = new System.Drawing.Size(400, 120);
                terminal.ApplyTheme(dark: true, "Consolas", 10, scrollback: 500);

                terminal.LineaArchivada += (_, linea) => archivadas.Add(linea.TrimEnd());
                terminal.Write(Encoding.UTF8.GetBytes(entrada));
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

        return archivadas;
    }

    private static string Lineas(int cuantas, string prefijo = "linea")
    {
        var sb = new StringBuilder();

        for (var i = 1; i <= cuantas; i++)
        {
            sb.Append($"{prefijo}{i}\r\n");
        }

        return sb.ToString();
    }

    [Fact]
    public void Cada_linea_que_sale_de_pantalla_se_avisa()
    {
        var archivadas = Capturar(Lineas(80));

        Assert.NotEmpty(archivadas);
        Assert.Contains("linea1", archivadas);
    }

    [Fact]
    public void Las_lineas_llegan_en_orden()
    {
        var archivadas = Capturar(Lineas(80));

        var primera = archivadas.IndexOf("linea1");
        var segunda = archivadas.IndexOf("linea2");

        Assert.True(primera >= 0 && segunda > primera, "No llegaron en orden.");
    }

    [Fact]
    public void Lo_que_sigue_en_pantalla_todavia_no_se_avisa()
    {
        var archivadas = Capturar(Lineas(3));

        Assert.Empty(archivadas);
    }

    // 1049 es la pantalla alternativa, la que usan vim, htop y less.
    [Fact]
    public void La_pantalla_alternativa_no_genera_historial()
    {
        var entrada = "\u001b[?1049h" + Lineas(80, "ruido") + "\u001b[?1049l";

        Assert.Empty(Capturar(entrada));
    }

    [Fact]
    public void Al_volver_de_la_pantalla_alternativa_se_sigue_avisando()
    {
        var entrada = "\u001b[?1049h" + Lineas(40, "ruido") + "\u001b[?1049l" + Lineas(80);

        var archivadas = Capturar(entrada);

        Assert.Contains("linea1", archivadas);
        Assert.DoesNotContain(archivadas, l => l.StartsWith("ruido", StringComparison.Ordinal));
    }
}
