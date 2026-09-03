using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

/// <summary>Zoom del terminal (FR-145).</summary>
public sealed class ZoomTests
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
                terminal.Size = new System.Drawing.Size(600, 300);
                terminal.ApplyTheme(dark: true, "Consolas", 12, scrollback: 100);

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
    public void Agrandar_sube_el_tamano()
    {
        var (antes, despues) = EnSta(t =>
        {
            var a = t.TamanoDeLetra;
            t.Zoom(2f);
            return (a, t.TamanoDeLetra);
        });

        Assert.Equal(antes + 2f, despues, 0.01);
    }

    [Fact]
    public void Achicar_baja_el_tamano()
    {
        var (antes, despues) = EnSta(t =>
        {
            var a = t.TamanoDeLetra;
            t.Zoom(-2f);
            return (a, t.TamanoDeLetra);
        });

        Assert.Equal(antes - 2f, despues, 0.01);
    }

    [Fact]
    public void Con_mas_letra_entran_menos_columnas()
    {
        var (antes, despues) = EnSta(t =>
        {
            var a = t.Columns;
            t.Zoom(6f);
            return (a, t.Columns);
        });

        Assert.True(despues < antes, $"Las columnas no bajaron: {antes} → {despues}");
    }

    [Fact]
    public void Con_menos_letra_entran_mas_columnas()
    {
        var (antes, despues) = EnSta(t =>
        {
            var a = t.Columns;
            t.Zoom(-4f);
            return (a, t.Columns);
        });

        Assert.True(despues > antes, $"Las columnas no subieron: {antes} → {despues}");
    }

    [Fact]
    public void No_baja_del_minimo()
    {
        var tamano = EnSta(t =>
        {
            for (var i = 0; i < 50; i++)
            {
                t.Zoom(-1f);
            }

            return t.TamanoDeLetra;
        });

        Assert.Equal(TerminalControl.ZoomMinimo, tamano, 0.01);
    }

    [Fact]
    public void No_pasa_del_maximo()
    {
        var tamano = EnSta(t =>
        {
            for (var i = 0; i < 80; i++)
            {
                t.Zoom(1f);
            }

            return t.TamanoDeLetra;
        });

        Assert.Equal(TerminalControl.ZoomMaximo, tamano, 0.01);
    }

    [Fact]
    public void En_el_tope_avisa_que_no_cambio()
    {
        var cambio = EnSta(t =>
        {
            for (var i = 0; i < 50; i++)
            {
                t.Zoom(-1f);
            }

            return t.Zoom(-1f);
        });

        Assert.False(cambio);
    }

    [Fact]
    public void Un_cambio_real_avisa_que_si()
    {
        Assert.True(EnSta(t => t.Zoom(1f)));
    }

    [Fact]
    public void El_aviso_lleva_el_tamano_nuevo()
    {
        var recibido = EnSta(t =>
        {
            float? puntos = null;
            t.CambioElZoom += (_, p) => puntos = p;

            t.AvisarZoom(t.Zoom(3f));

            return puntos;
        });

        Assert.Equal(15f, recibido!.Value, 0.01);
    }

    [Fact]
    public void En_el_tope_no_se_avisa()
    {
        var avisos = EnSta(t =>
        {
            var cuenta = 0;
            t.CambioElZoom += (_, _) => cuenta++;

            for (var i = 0; i < 50; i++)
            {
                t.AvisarZoom(t.Zoom(-1f));
            }

            var alLlegar = cuenta;
            t.AvisarZoom(t.Zoom(-1f));

            return (alLlegar, cuenta);
        });

        Assert.Equal(avisos.alLlegar, avisos.cuenta);
    }

    [Fact]
    public void El_texto_sobrevive_al_zoom()
    {
        var texto = EnSta(t =>
        {
            t.Write(System.Text.Encoding.UTF8.GetBytes("hola mundo\r\n"));
            t.Zoom(4f);

            return t.TextoCompleto;
        });

        Assert.Contains("hola mundo", texto, StringComparison.Ordinal);
    }
}
