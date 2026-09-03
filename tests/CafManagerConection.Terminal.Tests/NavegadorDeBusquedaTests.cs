using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

/// <summary>El recorrido de "ir a la siguiente" y "a la anterior" de FR-144.</summary>
public sealed class NavegadorDeBusquedaTests
{
    private static TerminalCoincidencia[] Tres() =>
    [
        TerminalCoincidencia.EnPantalla(0, 0, 3),
        TerminalCoincidencia.EnPantalla(1, 4, 3),
        TerminalCoincidencia.EnPantalla(2, 8, 3),
    ];

    [Fact]
    public void Sin_coincidencias_no_hay_actual()
    {
        var navegador = new NavegadorDeBusqueda();
        navegador.Establecer([]);

        Assert.Null(navegador.Actual);
        Assert.Equal(0, navegador.Posicion);
        Assert.Equal(0, navegador.Total);
    }

    [Fact]
    public void Al_establecer_coincidencias_la_actual_es_la_primera()
    {
        var navegador = new NavegadorDeBusqueda();
        navegador.Establecer(Tres());

        Assert.Equal(Tres()[0], navegador.Actual);
        Assert.Equal(1, navegador.Posicion);
        Assert.Equal(3, navegador.Total);
    }

    [Fact]
    public void Siguiente_avanza_en_orden()
    {
        var navegador = new NavegadorDeBusqueda();
        var coincidencias = Tres();
        navegador.Establecer(coincidencias);

        Assert.Equal(coincidencias[1], navegador.Siguiente());
        Assert.Equal(coincidencias[2], navegador.Siguiente());
    }

    [Fact]
    public void Siguiente_da_la_vuelta_al_llegar_al_final()
    {
        var navegador = new NavegadorDeBusqueda();
        var coincidencias = Tres();
        navegador.Establecer(coincidencias);

        navegador.Siguiente();
        navegador.Siguiente();
        var vuelta = navegador.Siguiente();

        Assert.Equal(coincidencias[0], vuelta);
        Assert.Equal(1, navegador.Posicion);
    }

    [Fact]
    public void Anterior_retrocede_en_orden()
    {
        var navegador = new NavegadorDeBusqueda();
        var coincidencias = Tres();
        navegador.Establecer(coincidencias);

        navegador.Siguiente();
        navegador.Siguiente();

        Assert.Equal(coincidencias[1], navegador.Anterior());
        Assert.Equal(coincidencias[0], navegador.Anterior());
    }

    [Fact]
    public void Anterior_da_la_vuelta_al_llegar_al_principio()
    {
        var navegador = new NavegadorDeBusqueda();
        var coincidencias = Tres();
        navegador.Establecer(coincidencias);

        var vuelta = navegador.Anterior();

        Assert.Equal(coincidencias[2], vuelta);
        Assert.Equal(3, navegador.Posicion);
    }

    [Fact]
    public void Siguiente_y_anterior_sin_coincidencias_no_rompen()
    {
        var navegador = new NavegadorDeBusqueda();
        navegador.Establecer([]);

        Assert.Null(navegador.Siguiente());
        Assert.Null(navegador.Anterior());
        Assert.Equal(0, navegador.Posicion);
    }

    [Fact]
    public void Si_la_coincidencia_actual_desaparece_al_rehacer_la_busqueda_no_rompe()
    {
        var navegador = new NavegadorDeBusqueda();
        navegador.Establecer(Tres());
        navegador.Siguiente();

        var nuevas = new[] { TerminalCoincidencia.EnPantalla(5, 0, 2) };
        navegador.Establecer(nuevas);

        Assert.Equal(nuevas[0], navegador.Actual);
        Assert.Equal(1, navegador.Posicion);
        Assert.NotNull(navegador.Siguiente());
    }

    [Fact]
    public void Si_la_coincidencia_actual_sigue_estando_se_mantiene_la_posicion()
    {
        var navegador = new NavegadorDeBusqueda();
        var coincidencias = Tres();
        navegador.Establecer(coincidencias);
        navegador.Siguiente();

        var conUnaMas = new[]
        {
            coincidencias[0],
            coincidencias[1],
            coincidencias[2],
            TerminalCoincidencia.EnPantalla(3, 0, 2),
        };

        navegador.Establecer(conUnaMas);

        Assert.Equal(coincidencias[1], navegador.Actual);
        Assert.Equal(2, navegador.Posicion);
    }

    [Fact]
    public void Establecer_vacio_deja_sin_actual()
    {
        var navegador = new NavegadorDeBusqueda();
        navegador.Establecer(Tres());
        navegador.Establecer([]);

        Assert.Null(navegador.Actual);
        Assert.Equal(-1, navegador.IndiceActual);
    }
}
