using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class OrdenDeProcesosTests
{
    private static ProcesoMedido Fila(
        int pid,
        double? cpu = null,
        long residentes = 0,
        long? leidos = null,
        long? escritos = null) =>
        new(pid, 1, $"p{pid}", "S", cpu, residentes, 1, leidos, escritos);

    [Fact]
    public void Por_cpu_el_primero_es_el_que_mas_consume_ahora()
    {
        var orden = OrdenDeProcesos.Ordenar(
            [Fila(1, cpu: 3), Fila(2, cpu: 91), Fila(3, cpu: 12)],
            CriterioDeProcesos.Cpu);

        Assert.Equal(new[] { 2, 3, 1 }, orden.Select(p => p.Pid).ToArray());
    }

    [Fact]
    public void Por_memoria_el_primero_es_el_de_mayor_residente()
    {
        var orden = OrdenDeProcesos.Ordenar(
            [Fila(1, residentes: 100), Fila(2, residentes: 9000), Fila(3, residentes: 300)],
            CriterioDeProcesos.Memoria);

        Assert.Equal(new[] { 2, 3, 1 }, orden.Select(p => p.Pid).ToArray());
    }

    [Fact]
    public void Por_disco_suma_lo_leido_y_lo_escrito()
    {
        var orden = OrdenDeProcesos.Ordenar(
            [
                Fila(1, leidos: 10, escritos: 10),
                Fila(2, leidos: 500, escritos: null),
                Fila(3, leidos: null, escritos: 100),
            ],
            CriterioDeProcesos.Disco);

        Assert.Equal(new[] { 2, 3, 1 }, orden.Select(p => p.Pid).ToArray());
    }

    [Fact]
    public void Un_proceso_sin_porcentaje_medido_queda_ultimo_y_no_primero()
    {
        var orden = OrdenDeProcesos.Ordenar(
            [Fila(1, cpu: null, residentes: 5), Fila(2, cpu: 0, residentes: 1)],
            CriterioDeProcesos.Cpu);

        Assert.Equal(new[] { 2, 1 }, orden.Select(p => p.Pid).ToArray());
    }

    [Fact]
    public void Con_todos_sin_medir_el_orden_por_cpu_cae_en_la_memoria()
    {
        var orden = OrdenDeProcesos.Ordenar(
            [Fila(1, residentes: 10), Fila(2, residentes: 700), Fila(3, residentes: 90)],
            CriterioDeProcesos.Cpu);

        Assert.Equal(new[] { 2, 3, 1 }, orden.Select(p => p.Pid).ToArray());
    }

    [Fact]
    public void Primeros_recorta_sin_alterar_el_orden()
    {
        var orden = OrdenDeProcesos.Primeros(
            [Fila(1, cpu: 3), Fila(2, cpu: 91), Fila(3, cpu: 12)],
            CriterioDeProcesos.Cpu,
            cuantos: 2);

        Assert.Equal(new[] { 2, 3 }, orden.Select(p => p.Pid).ToArray());
    }

    [Fact]
    public void Primeros_de_una_lista_mas_corta_no_falla()
    {
        Assert.Single(OrdenDeProcesos.Primeros([Fila(1)], CriterioDeProcesos.Memoria, 10));
    }
}
