using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

public sealed class BarraDeDesplazamientoTests
{
    private static T EnSta<T>(int lineas, Func<TerminalControl, T> accion)
    {
        T? resultado = default;
        Exception? fallo = null;

        var hilo = new Thread(() =>
        {
            try
            {
                using var terminal = new TerminalControl();
                terminal.Size = new System.Drawing.Size(400, 200);
                terminal.ApplyTheme(dark: true, "Consolas", 10, scrollback: 500);

                if (lineas > 0)
                {
                    var sb = new StringBuilder();

                    for (var i = 1; i <= lineas; i++)
                    {
                        sb.Append($"linea{i}\r\n");
                    }

                    terminal.Write(Encoding.UTF8.GetBytes(sb.ToString()));
                }

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
    public void Sin_historial_no_hay_barra() =>
        Assert.False(EnSta(0, t => t.HayBarraDeDesplazamiento));

    [Fact]
    public void Con_historial_hay_barra() =>
        Assert.True(EnSta(200, t => t.HayBarraDeDesplazamiento));

    [Fact]
    public void En_el_fondo_el_pulgar_va_abajo()
    {
        var (pulgar, alto) = EnSta(200, t => (t.PulgarDeDesplazamiento, t.Height));

        Assert.Equal(alto, pulgar.Bottom);
    }

    [Fact]
    public void Arriba_de_todo_el_pulgar_va_arriba()
    {
        var pulgar = EnSta(200, t =>
        {
            t.ScrollBy(10_000);
            return t.PulgarDeDesplazamiento;
        });

        Assert.Equal(0, pulgar.Y);
    }

    [Fact]
    public void Subir_mueve_el_pulgar_hacia_arriba()
    {
        var (fondo, medio) = EnSta(200, t =>
        {
            var a = t.PulgarDeDesplazamiento.Y;
            t.ScrollBy(40);
            return (a, t.PulgarDeDesplazamiento.Y);
        });

        Assert.True(medio < fondo, $"El pulgar no subió: {fondo} → {medio}");
    }

    [Fact]
    public void El_pulgar_es_mas_chico_cuanto_mas_historial_hay()
    {
        var poco = EnSta(30, t => t.PulgarDeDesplazamiento.Height);
        var mucho = EnSta(400, t => t.PulgarDeDesplazamiento.Height);

        Assert.True(mucho < poco, $"No se encogió: {poco} → {mucho}");
    }

    [Fact]
    public void El_pulgar_nunca_queda_impracticable()
    {
        var alto = EnSta(3000, t => t.PulgarDeDesplazamiento.Height);

        Assert.True(alto >= 26, $"Quedó de {alto} px");
    }

    [Fact]
    public void El_pulgar_no_se_sale_del_canal()
    {
        var (pulgar, alto) = EnSta(3000, t =>
        {
            t.ScrollBy(10_000);
            return (t.PulgarDeDesplazamiento, t.Height);
        });

        Assert.True(pulgar.Y >= 0, $"Se fue arriba: {pulgar.Y}");
        Assert.True(pulgar.Bottom <= alto, $"Se fue abajo: {pulgar.Bottom} > {alto}");
    }

    [Fact]
    public void La_barra_va_pegada_al_borde_derecho()
    {
        var (canal, ancho) = EnSta(200, t => (t.CanalDeDesplazamiento, t.Width));

        Assert.Equal(ancho, canal.Right);
    }

    [Fact]
    public void Arrastrar_al_tope_de_arriba_lleva_al_principio()
    {
        var pulgar = EnSta(200, t =>
        {
            t.DesplazarPorPulgar(0);
            return t.PulgarDeDesplazamiento;
        });

        Assert.Equal(0, pulgar.Y);
    }

    [Fact]
    public void Arrastrar_al_tope_de_abajo_lleva_al_fondo()
    {
        var (pulgar, alto) = EnSta(200, t =>
        {
            t.ScrollBy(10_000);
            t.DesplazarPorPulgar(t.Height);

            return (t.PulgarDeDesplazamiento, t.Height);
        });

        Assert.Equal(alto, pulgar.Bottom);
    }

    [Fact]
    public void Arrastrar_a_donde_ya_estaba_no_mueve_nada()
    {
        var (antes, despues) = EnSta(200, t =>
        {
            t.ScrollBy(40);

            var a = t.PulgarDeDesplazamiento;
            t.DesplazarPorPulgar(a.Y);

            return (a.Y, t.PulgarDeDesplazamiento.Y);
        });

        Assert.True(Math.Abs(antes - despues) <= 1, $"{antes} → {despues}");
    }

    [Fact]
    public void Sin_historial_arrastrar_no_rompe() =>
        Assert.False(EnSta(0, t =>
        {
            t.DesplazarPorPulgar(50);
            return t.HayBarraDeDesplazamiento;
        }));
}
