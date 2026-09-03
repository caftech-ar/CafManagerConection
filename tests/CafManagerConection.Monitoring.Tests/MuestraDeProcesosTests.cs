using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class MuestraDeProcesosTests
{
    private static readonly DateTimeOffset Cero = DateTimeOffset.UnixEpoch;

    private static ProcesoCrudo Proceso(
        int pid,
        long tics,
        int padre = 1,
        long arranque = 500,
        long residentes = 4096,
        int hilos = 1,
        long? leidos = null,
        long? escritos = null) =>
        new(pid, padre, $"p{pid}", "S", tics, 0, arranque, residentes, hilos, leidos, escritos);

    private static MuestraDeProcesos Muestra(double segundos, int tics, params ProcesoCrudo[] p) =>
        new(Cero.AddSeconds(segundos), p, tics);

    [Fact]
    public void El_porcentaje_es_la_diferencia_de_tics_dividida_por_el_tiempo_y_por_los_tics_del_reloj()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 1000));
        var despues = Muestra(2, 100, Proceso(10, tics: 1100));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Equal(50, fila.PorcentajeDeCpu!.Value, precision: 6);
    }

    [Fact]
    public void No_es_el_promedio_de_vida_que_informa_ps()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 360000, arranque: 0));
        var despues = Muestra(1, 100, Proceso(10, tics: 360001, arranque: 0));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Equal(1, fila.PorcentajeDeCpu!.Value, precision: 6);
    }

    [Fact]
    public void El_porcentaje_no_se_acota_a_cien_porque_un_proceso_con_muchos_hilos_lo_pasa()
    {
        var antes = Muestra(0, 100, Proceso(907, tics: 0, hilos: 82));
        var despues = Muestra(1, 100, Proceso(907, tics: 341, hilos: 82));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Equal(341, fila.PorcentajeDeCpu!.Value, precision: 6);
    }

    [Fact]
    public void Un_proceso_que_aparecio_entre_las_dos_muestras_no_informa_porcentaje()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 100));
        var despues = Muestra(1, 100, Proceso(10, tics: 150), Proceso(11, tics: 40));

        var filas = MuestraDeProcesos.Entre(antes, despues);

        Assert.Null(filas.Single(f => f.Pid == 11).PorcentajeDeCpu);
        Assert.NotNull(filas.Single(f => f.Pid == 10).PorcentajeDeCpu);
    }

    [Fact]
    public void Un_proceso_que_desaparecio_no_queda_en_la_lista_con_el_valor_anterior()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 100), Proceso(11, tics: 500));
        var despues = Muestra(1, 100, Proceso(10, tics: 150));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Equal(10, fila.Pid);
    }

    [Fact]
    public void Un_pid_reusado_no_hereda_el_contador_del_proceso_anterior()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 5000, arranque: 500));
        var despues = Muestra(1, 100, Proceso(10, tics: 3, arranque: 900000));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Null(fila.PorcentajeDeCpu);
    }

    [Fact]
    public void Un_contador_que_va_para_atras_no_da_un_porcentaje_negativo()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 5000));
        var despues = Muestra(1, 100, Proceso(10, tics: 4000));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Null(fila.PorcentajeDeCpu);
    }

    [Fact]
    public void Sin_tiempo_transcurrido_no_hay_porcentaje_ni_division_por_cero()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 100));
        var despues = Muestra(0, 100, Proceso(10, tics: 200));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Null(fila.PorcentajeDeCpu);
    }

    [Fact]
    public void Dos_muestras_en_desorden_no_dan_porcentaje()
    {
        var antes = Muestra(5, 100, Proceso(10, tics: 100));
        var despues = Muestra(1, 100, Proceso(10, tics: 200));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Null(fila.PorcentajeDeCpu);
    }

    [Fact]
    public void Los_tics_por_segundo_del_servidor_mandan_sobre_el_valor_por_omision()
    {
        var antes = Muestra(0, 250, Proceso(10, tics: 0));
        var despues = Muestra(1, 250, Proceso(10, tics: 250));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Equal(100, fila.PorcentajeDeCpu!.Value, precision: 6);
    }

    [Fact]
    public void Los_tics_por_segundo_valen_cien_cuando_nadie_los_dice()
    {
        var muestra = new MuestraDeProcesos(Cero, []);

        Assert.Equal(100, muestra.TicsPorSegundo);
        Assert.Equal(100, MuestraDeProcesos.TicsPorSegundoPorOmision);
    }

    [Fact]
    public void La_primera_muestra_sale_sin_porcentaje_y_no_con_cero_inventado()
    {
        var muestra = Muestra(0, 100, Proceso(10, tics: 900));

        var fila = Assert.Single(muestra.SinMedir());

        Assert.Null(fila.PorcentajeDeCpu);
        Assert.Equal(10, fila.Pid);
    }

    [Fact]
    public void La_memoria_la_entrada_y_la_salida_vienen_de_la_muestra_actual()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 0, residentes: 1024));
        var despues = Muestra(
            1, 100, Proceso(10, tics: 0, residentes: 8192, leidos: 4096, escritos: 512));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Equal(8192, fila.BytesResidentes);
        Assert.Equal(4096, fila.BytesLeidos);
        Assert.Equal(512, fila.BytesEscritos);
    }

    [Fact]
    public void El_proceso_sin_io_legible_llega_a_la_fila_sin_entrada_ni_salida()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 0));
        var despues = Muestra(1, 100, Proceso(10, tics: 0));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Null(fila.BytesLeidos);
        Assert.Null(fila.BytesEscritos);
    }

    [Fact]
    public void El_nombre_el_padre_el_estado_y_los_hilos_pasan_a_la_fila()
    {
        var antes = Muestra(0, 100, Proceso(10, tics: 0, padre: 7, hilos: 12));
        var despues = Muestra(1, 100, Proceso(10, tics: 0, padre: 7, hilos: 12));

        var fila = Assert.Single(MuestraDeProcesos.Entre(antes, despues));

        Assert.Equal("p10", fila.Nombre);
        Assert.Equal(7, fila.PidPadre);
        Assert.Equal("S", fila.Estado);
        Assert.Equal(12, fila.Hilos);
    }
}
