using System.Text;
using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

/// <summary>El motor de búsqueda de FR-144.</summary>
public sealed class BuscadorDeTerminalTests
{
    private static (VtEmulator Emu, TerminalBuffer Buf) Nuevo(int cols = 80, int rows = 3)
    {
        var buffer = new TerminalBuffer(cols, rows, scrollbackLimit: 100);
        return (new VtEmulator(buffer), buffer);
    }

    private static void Write(VtEmulator emu, string texto) =>
        emu.Write(Encoding.UTF8.GetBytes(texto));

    [Fact]
    public void Sin_busqueda_activa_no_hay_coincidencias()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "arrancó el servicio\r\n");

        Assert.Empty(BuscadorDeTerminal.Buscar(buf, string.Empty));
    }

    [Fact]
    public void Un_texto_que_no_esta_da_una_lista_vacia()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "arrancó el servicio\r\nescuchando en el puerto 8080\r\n");

        Assert.Empty(BuscadorDeTerminal.Buscar(buf, "error de conexión"));
    }

    [Fact]
    public void Encuentra_todas_las_apariciones_de_una_linea()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "error error error\r\n");

        var coincidencias = BuscadorDeTerminal.Buscar(buf, "error");

        Assert.Equal(3, coincidencias.Count);
        Assert.Equal([0, 6, 12], coincidencias.Select(c => c.Columna));
    }

    [Fact]
    public void Coincidencias_superpuestas_no_se_cuentan_dos_veces()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "aaaa\r\n");

        var coincidencias = BuscadorDeTerminal.Buscar(buf, "aa");

        Assert.Equal(2, coincidencias.Count);
        Assert.Equal([0, 2], coincidencias.Select(c => c.Columna));
    }

    [Fact]
    public void No_distingue_mayusculas_por_omision()
    {
        var (emu, buf) = Nuevo();
        Write(emu, "Conexión ESTABLECIDA con el servidor\r\n");

        Assert.Single(BuscadorDeTerminal.Buscar(buf, "establecida"));
        Assert.Single(BuscadorDeTerminal.Buscar(buf, "CONEXIÓN"));
    }

    [Fact]
    public void Encuentra_coincidencias_en_el_historial_y_en_la_pantalla_juntas()
    {
        var (emu, buf) = Nuevo(rows: 3);

        for (var i = 1; i <= 6; i++)
        {
            Write(emu, $"linea{i} con dato\r\n");
        }

        Assert.True(buf.Scrollback.Count > 0, "la prueba necesita que algo haya ido al historial.");

        var coincidencias = BuscadorDeTerminal.Buscar(buf, "dato");

        Assert.Equal(6, coincidencias.Count);
        Assert.Equal(buf.Scrollback.Count, coincidencias.Count(c => !c.EsPantalla));
        Assert.Equal(6 - buf.Scrollback.Count, coincidencias.Count(c => c.EsPantalla));
    }

    [Fact]
    public void El_orden_es_el_de_lectura_historial_primero_y_despues_pantalla()
    {
        var (emu, buf) = Nuevo(rows: 3);

        for (var i = 1; i <= 6; i++)
        {
            Write(emu, $"marca{i}\r\n");
        }

        var coincidencias = BuscadorDeTerminal.Buscar(buf, "marca");

        var deHistorial = coincidencias.TakeWhile(c => !c.EsPantalla).ToList();
        var dePantalla = coincidencias.SkipWhile(c => !c.EsPantalla).ToList();

        Assert.Equal(coincidencias.Count, deHistorial.Count + dePantalla.Count);
        Assert.All(dePantalla, c => Assert.True(c.EsPantalla));

        for (var i = 0; i < dePantalla.Count - 1; i++)
        {
            Assert.True(dePantalla[i].Fila < dePantalla[i + 1].Fila);
        }
    }

    [Fact]
    public void Una_coincidencia_no_cruza_el_salto_entre_dos_lineas()
    {
        var (emu, buf) = Nuevo(rows: 3);
        Write(emu, "esto termina en err\r\nor arranca así\r\n");

        Assert.Empty(BuscadorDeTerminal.Buscar(buf, "error"));

        Assert.Single(BuscadorDeTerminal.Buscar(buf, "err"));
        Assert.Single(BuscadorDeTerminal.Buscar(buf, "or arranca"));
    }

    [Fact]
    public void Busca_bien_sobre_historial_recortado_a_su_contenido()
    {
        var (emu, buf) = Nuevo(cols: 300, rows: 3);

        for (var i = 1; i <= 5; i++)
        {
            Write(emu, $"ok{i}\r\n");
        }

        Assert.True(buf.Scrollback.Count > 0);
        Assert.True(buf.Scrollback[0].Length < 300, "la línea archivada debería estar recortada.");

        var coincidencias = BuscadorDeTerminal.Buscar(buf, "ok");

        Assert.Equal(5, coincidencias.Count);
        Assert.All(coincidencias, c => Assert.Equal(0, c.Columna));
    }

    [Fact]
    public void Una_coincidencia_de_historial_guarda_la_linea_archivada_misma()
    {
        var (emu, buf) = Nuevo(rows: 2);
        Write(emu, "primera\r\nsegunda\r\ntercera\r\n");

        var coincidencia = BuscadorDeTerminal.Buscar(buf, "primera").Single();

        Assert.False(coincidencia.EsPantalla);
        Assert.Same(buf.Scrollback[0], coincidencia.LineaHistorial);
    }
}
