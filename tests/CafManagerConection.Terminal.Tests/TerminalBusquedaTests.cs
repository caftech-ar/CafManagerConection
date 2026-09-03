using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

/// <summary>Integra el motor de búsqueda con <see cref="TerminalControl"/> (FR-144).</summary>
public sealed class TerminalBusquedaTests
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

    private static void Escribir(TerminalControl terminal, string texto) =>
        terminal.Write(Encoding.UTF8.GetBytes(texto));

    [Fact]
    public void Buscar_deja_el_contador_en_la_primera_coincidencia()
    {
        var (total, actual) = EnSta(t =>
        {
            for (var i = 1; i <= 5; i++)
            {
                Escribir(t, $"linea{i}\r\n");
            }

            t.Buscar("linea");
            return (t.TotalCoincidencias, t.CoincidenciaActual);
        });

        Assert.Equal(5, total);
        Assert.Equal(1, actual);
    }

    [Fact]
    public void Buscar_vacio_limpia_el_contador()
    {
        var (total, actual) = EnSta(t =>
        {
            Escribir(t, "linea1\r\nlinea2\r\n");
            t.Buscar("linea");
            t.Buscar(string.Empty);
            return (t.TotalCoincidencias, t.CoincidenciaActual);
        });

        Assert.Equal(0, total);
        Assert.Equal(0, actual);
    }

    [Fact]
    public void Un_texto_que_no_esta_deja_el_contador_en_cero()
    {
        var actual = EnSta(t =>
        {
            Escribir(t, "todo en orden\r\n");
            t.Buscar("no está esto");
            return t.CoincidenciaActual;
        });

        Assert.Equal(0, actual);
    }

    [Fact]
    public void BusquedaSiguiente_avanza_y_da_la_vuelta()
    {
        var posiciones = EnSta(t =>
        {
            for (var i = 1; i <= 3; i++)
            {
                Escribir(t, $"marca{i}\r\n");
            }

            t.Buscar("marca");

            var lista = new List<int> { t.CoincidenciaActual };
            t.BusquedaSiguiente();
            lista.Add(t.CoincidenciaActual);
            t.BusquedaSiguiente();
            lista.Add(t.CoincidenciaActual);

            t.BusquedaSiguiente();
            lista.Add(t.CoincidenciaActual);

            return lista;
        });

        Assert.Equal([1, 2, 3, 1], posiciones);
    }

    [Fact]
    public void BusquedaAnterior_desde_la_primera_da_la_vuelta_a_la_ultima()
    {
        var (total, actual) = EnSta(t =>
        {
            for (var i = 1; i <= 3; i++)
            {
                Escribir(t, $"marca{i}\r\n");
            }

            t.Buscar("marca");
            t.BusquedaAnterior();

            return (t.TotalCoincidencias, t.CoincidenciaActual);
        });

        Assert.Equal(3, total);
        Assert.Equal(3, actual);
    }

    [Fact]
    public void El_historial_creciendo_con_la_busqueda_abierta_no_rompe_ni_excepciona()
    {
        var sobrevivio = EnSta(t =>
        {
            t.ApplyTheme(dark: true, "Consolas", 10, scrollback: 20);
            Escribir(t, "objetivoUnico aparece acá\r\n");

            t.Buscar("objetivoUnico");

            Assert.Equal(1, t.TotalCoincidencias);
            Assert.Equal(1, t.CoincidenciaActual);

            for (var i = 1; i <= 500; i++)
            {
                Escribir(t, $"relleno {i}\r\n");
            }

            t.BusquedaSiguiente();
            t.BusquedaAnterior();
            t.BusquedaSiguiente();

            return t.TotalCoincidencias == 1 && t.CoincidenciaActual == 1;
        });

        Assert.True(sobrevivio);
    }

    [Fact]
    public void Rehacer_la_busqueda_despues_de_que_crecio_el_historial_encuentra_lo_nuevo()
    {
        var total = EnSta(t =>
        {
            t.ApplyTheme(dark: true, "Consolas", 10, scrollback: 20);
            Escribir(t, "primera aparición\r\n");
            t.Buscar("aparición");

            for (var i = 1; i <= 5; i++)
            {
                Escribir(t, $"relleno {i}\r\n");
            }

            Escribir(t, "segunda aparición\r\n");
            t.Buscar("aparición");

            return t.TotalCoincidencias;
        });

        Assert.Equal(2, total);
    }
}
