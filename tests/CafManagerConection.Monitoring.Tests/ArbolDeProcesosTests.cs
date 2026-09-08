using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class ArbolDeProcesosTests
{
    private static ProcesoMedido Fila(
        int pid, int padre, double? cpu = 0, long residentes = 0) =>
        new(pid, padre, $"p{pid}", "S", cpu, residentes, 1, null, null);

    private static readonly ProcesoMedido[] Familia =
    [
        Fila(1, 0, cpu: 1, residentes: 100),
        Fila(10, 1, cpu: 2, residentes: 200),
        Fila(11, 10, cpu: 4, residentes: 400),
        Fila(12, 10, cpu: 8, residentes: 800),
        Fila(13, 11, cpu: 16, residentes: 1600),
    ];

    [Fact]
    public void El_indice_da_los_hijos_directos_de_cualquier_pid()
    {
        var indice = new IndiceDeProcesos(Familia);

        Assert.Equal(new[] { 11, 12 }, indice.HijosDirectos(10).Select(h => h.Pid).ToArray());
        Assert.Empty(indice.HijosDirectos(12));
    }

    [Fact]
    public void El_subarbol_de_un_proceso_baja_hasta_los_nietos()
    {
        var nodo = new IndiceDeProcesos(Familia).Subarbol(Familia[1]);

        Assert.Equal(10, nodo.Proceso.Pid);
        Assert.Equal(new[] { 11, 12 }, nodo.Hijos.Select(h => h.Proceso.Pid).ToArray());
        Assert.Equal(13, Assert.Single(nodo.Hijos[0].Hijos).Proceso.Pid);
    }

    // El consumo del padre es el suyo; el del subárbol se pide aparte y no se cuenta dos veces.
    [Fact]
    public void El_subarbol_suma_el_consumo_de_todos_una_sola_vez()
    {
        var nodo = new IndiceDeProcesos(Familia).Subarbol(Familia[1]);

        Assert.Equal(2, nodo.Proceso.PorcentajeDeCpu);
        Assert.Equal(2 + 4 + 8 + 16, nodo.CpuDelSubarbol);
        Assert.Equal(200 + 400 + 800 + 1600, nodo.BytesResidentesDelSubarbol);
    }

    [Fact]
    public void El_subarbol_de_una_rama_no_suma_a_sus_hermanos()
    {
        var diez = new IndiceDeProcesos(Familia).Subarbol(Familia[1]);
        var once = diez.Hijos.Single(h => h.Proceso.Pid == 11);

        Assert.Equal(4 + 16, once.CpuDelSubarbol, precision: 6);
    }

    [Fact]
    public void El_proceso_sin_porcentaje_medido_no_ensucia_la_suma_del_subarbol()
    {
        ProcesoMedido[] muestra = [Fila(10, 1, cpu: 3), Fila(11, 10, cpu: null)];

        var nodo = new IndiceDeProcesos(muestra).Subarbol(muestra[0]);

        Assert.Equal(3, nodo.CpuDelSubarbol, precision: 6);
    }

    [Fact]
    public void Un_proceso_que_es_su_propio_padre_no_es_su_propio_hijo()
    {
        ProcesoMedido[] muestra = [Fila(10, 10, cpu: 5)];

        var nodo = new IndiceDeProcesos(muestra).Subarbol(muestra[0]);

        Assert.Empty(nodo.Hijos);
        Assert.Equal(5, nodo.CpuDelSubarbol, precision: 6);
    }

    [Fact]
    public void Un_ciclo_de_ppid_no_cuelga_al_armar_el_subarbol()
    {
        ProcesoMedido[] ciclo = [Fila(7, 8), Fila(8, 7)];

        var nodo = new IndiceDeProcesos(ciclo).Subarbol(ciclo[0]);

        Assert.Equal(8, Assert.Single(nodo.Hijos).Proceso.Pid);
        Assert.Empty(Assert.Single(nodo.Hijos).Hijos);
    }

    [Fact]
    public void Un_ciclo_de_tres_no_cuenta_a_nadie_dos_veces()
    {
        ProcesoMedido[] ciclo = [Fila(10, 11, cpu: 1), Fila(11, 12, cpu: 1), Fila(12, 10, cpu: 1)];

        var nodo = new IndiceDeProcesos(ciclo).Subarbol(ciclo[0]);

        Assert.Equal(3, nodo.CpuDelSubarbol, precision: 6);
    }

    [Fact]
    public void Una_muestra_vacia_no_le_da_hijos_a_nadie()
    {
        Assert.Empty(new IndiceDeProcesos([]).HijosDirectos(1));
    }
}
