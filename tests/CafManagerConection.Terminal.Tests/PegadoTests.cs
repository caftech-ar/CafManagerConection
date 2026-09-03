using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

/// <summary>Qué se manda al servidor al pegar (FR-030e, FR-030f, FR-030g).</summary>
public sealed class PegadoTests
{
    [Theory]
    [InlineData("uno\r\ndos", "uno\rdos")]
    [InlineData("uno\ndos", "uno\rdos")]
    [InlineData("uno\rdos", "uno\rdos")]
    public void Los_saltos_de_linea_se_normalizan_a_CR(string crudo, string esperado) =>
        Assert.Equal(esperado, TerminalControl.NormalizarPegado(crudo));

    [Theory]
    [InlineData("una sola linea", 1)]
    [InlineData("una sola linea\r", 1)]
    [InlineData("uno\rdos", 2)]
    [InlineData("uno\rdos\rtres\r", 3)]
    public void Se_cuentan_las_lineas_de_lo_que_se_va_a_pegar(string texto, int esperadas) =>
        Assert.Equal(esperadas, TerminalControl.ContarLineas(TerminalControl.NormalizarPegado(texto)));

    [Fact]
    public void Sin_modo_2004_el_texto_va_crudo() =>
        Assert.Equal("hola", Encoding.UTF8.GetString(TerminalControl.ArmarPegado("hola", bracketed: false)));

    [Fact]
    public void Con_modo_2004_el_texto_va_entre_marcas() =>
        Assert.Equal(
            "\x1b[200~hola\x1b[201~",
            Encoding.UTF8.GetString(TerminalControl.ArmarPegado("hola", bracketed: true)));

    private static string Pegar(
        string portapapeles,
        bool modo2004 = false,
        Func<TerminalControl.ConfirmacionDePegado, bool>? responder = null)
    {
        string? enviado = null;
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                using var terminal = new TerminalControl();
                terminal.ApplyTheme(dark: true, "Consolas", 10, scrollback: 100);
                terminal.Size = new System.Drawing.Size(400, 100);
                terminal.LeerDelPortapapeles = () => portapapeles;

                if (modo2004)
                {
                    terminal.Write(Encoding.ASCII.GetBytes("\x1b[?2004h"));
                }

                if (responder is not null)
                {
                    terminal.PidioConfirmarPegado += (_, p) => p.Aceptado = responder(p);
                }

                terminal.UserInput += (_, bytes) => enviado = Encoding.UTF8.GetString(bytes);
                terminal.Paste();
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

        return enviado ?? string.Empty;
    }

    [Fact]
    public void Una_linea_se_pega_sin_preguntar_nada() =>
        Assert.Equal("systemctl status nginx", Pegar("systemctl status nginx"));

    /// <summary>El caso que motivó FR-030e.</summary>
    [Fact]
    public void Con_el_servidor_en_modo_2004_el_pegado_va_marcado() =>
        Assert.Equal("\x1b[200~uno\rdos\x1b[201~", Pegar("uno\r\ndos", modo2004: true));

    [Fact]
    public void Con_modo_2004_no_se_pregunta_por_las_lineas()
    {
        var preguntas = 0;

        var enviado = Pegar("uno\r\ndos\r\ntres", modo2004: true, responder: _ =>
        {
            preguntas++;
            return true;
        });

        Assert.Equal(0, preguntas);
        Assert.Contains("\x1b[200~", enviado, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_modo_2004_varias_lineas_piden_confirmacion()
    {
        var lineasInformadas = 0;

        var enviado = Pegar("uno\r\ndos\r\ntres", responder: p =>
        {
            lineasInformadas = p.Lineas;
            return true;
        });

        Assert.Equal(3, lineasInformadas);
        Assert.Equal("uno\rdos\rtres", enviado);
    }

    [Fact]
    public void Si_se_cancela_la_confirmacion_no_se_manda_nada() =>
        Assert.Empty(Pegar("uno\r\ndos", responder: _ => false));

    [Fact]
    public void Sin_nadie_que_conteste_se_pega_igual() =>
        Assert.Equal("uno\rdos", Pegar("uno\r\ndos"));

    [Fact]
    public void Con_el_portapapeles_vacio_no_se_manda_nada() =>
        Assert.Empty(Pegar(string.Empty));
}
