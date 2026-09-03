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
    public void Los_hijos_directos_son_los_del_ppid_y_no_los_nietos()
    {
        var hijos = ArbolDeProcesos.HijosDirectos(Familia, 10);

        Assert.Equal(new[] { 11, 12 }, hijos.Select(h => h.Pid).ToArray());
    }

    [Fact]
    public void Un_proceso_sin_hijos_no_tiene_descendencia_directa()
    {
        Assert.Empty(ArbolDeProcesos.HijosDirectos(Familia, 13));
    }

    [Fact]
    public void El_bosque_arranca_por_el_proceso_cuyo_padre_no_esta_en_la_muestra()
    {
        var bosque = ArbolDeProcesos.Armar(Familia);

        var raiz = Assert.Single(bosque);
        Assert.Equal(1, raiz.Proceso.Pid);
        Assert.Equal(10, Assert.Single(raiz.Hijos).Proceso.Pid);
    }

    [Fact]
    public void Un_proceso_huerfano_es_raiz_de_su_propio_arbol()
    {
        var bosque = ArbolDeProcesos.Armar([Fila(10, 1), Fila(11, 10), Fila(77, 4242)]);

        Assert.Equal(new[] { 10, 77 }, bosque.Select(n => n.Proceso.Pid).ToArray());
    }

    [Fact]
    public void El_porcentaje_propio_del_padre_no_incluye_el_de_los_hijos()
    {
        var raiz = Assert.Single(ArbolDeProcesos.Armar(Familia));

        Assert.Equal(1, raiz.Proceso.PorcentajeDeCpu!.Value, precision: 6);
    }

    [Fact]
    public void El_consumo_del_subarbol_cuenta_a_cada_proceso_una_sola_vez()
    {
        var raiz = Assert.Single(ArbolDeProcesos.Armar(Familia));

        Assert.Equal(1 + 2 + 4 + 8 + 16, raiz.CpuDelSubarbol, precision: 6);
        Assert.Equal(100L + 200 + 400 + 800 + 1600, raiz.BytesResidentesDelSubarbol);
    }

    [Fact]
    public void El_subarbol_de_una_rama_no_suma_a_sus_hermanos()
    {
        var raiz = Assert.Single(ArbolDeProcesos.Armar(Familia));
        var diez = Assert.Single(raiz.Hijos);
        var once = diez.Hijos.Single(h => h.Proceso.Pid == 11);

        Assert.Equal(4 + 16, once.CpuDelSubarbol, precision: 6);
    }

    [Fact]
    public void El_proceso_sin_porcentaje_medido_no_ensucia_la_suma_del_subarbol()
    {
        var bosque = ArbolDeProcesos.Armar([Fila(10, 1, cpu: 3), Fila(11, 10, cpu: null)]);

        Assert.Equal(3, Assert.Single(bosque).CpuDelSubarbol, precision: 6);
    }

    [Fact]
    public void Un_ciclo_en_los_ppid_no_cuelga_el_armado()
    {
        var bosque = ArbolDeProcesos.Armar([Fila(10, 11), Fila(11, 10)]);

        Assert.NotEmpty(bosque);
        Assert.Equal(2, Aplanar(bosque).Count);
    }

    [Fact]
    public void Un_ciclo_de_tres_no_cuenta_a_nadie_dos_veces()
    {
        var bosque = ArbolDeProcesos.Armar([Fila(10, 11, cpu: 1), Fila(11, 12, cpu: 1), Fila(12, 10, cpu: 1)]);

        Assert.Equal(new[] { 10, 11, 12 }, Aplanar(bosque).Select(n => n.Proceso.Pid).Order().ToArray());
        Assert.Equal(3, bosque.Sum(n => n.CpuDelSubarbol), precision: 6);
    }

    [Fact]
    public void Un_proceso_que_es_su_propio_padre_es_raiz_y_no_su_propio_hijo()
    {
        var raiz = Assert.Single(ArbolDeProcesos.Armar([Fila(10, 10, cpu: 5)]));

        Assert.Empty(raiz.Hijos);
        Assert.Equal(5, raiz.CpuDelSubarbol, precision: 6);
    }

    [Fact]
    public void Cada_proceso_de_la_muestra_aparece_una_sola_vez_en_el_bosque()
    {
        var plano = Aplanar(ArbolDeProcesos.Armar(Familia));

        Assert.Equal(Familia.Length, plano.Count);
        Assert.Equal(Familia.Length, plano.Select(n => n.Proceso.Pid).Distinct().Count());
    }

    [Fact]
    public void Una_muestra_vacia_da_un_bosque_vacio()
    {
        Assert.Empty(ArbolDeProcesos.Armar([]));
    }

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

    // El consumo del padre es el suyo; el del subárbol se pide aparte y no se cuenta dos veces (FR-183).
    [Fact]
    public void El_subarbol_suma_el_consumo_de_todos_una_sola_vez()
    {
        var nodo = new IndiceDeProcesos(Familia).Subarbol(Familia[1]);

        Assert.Equal(2, nodo.Proceso.PorcentajeDeCpu);
        Assert.Equal(2 + 4 + 8 + 16, nodo.CpuDelSubarbol);
        Assert.Equal(200 + 400 + 800 + 1600, nodo.BytesResidentesDelSubarbol);
    }

    [Fact]
    public void Un_ciclo_de_ppid_no_cuelga_al_armar_el_subarbol()
    {
        ProcesoMedido[] ciclo = [Fila(7, 8), Fila(8, 7)];

        var nodo = new IndiceDeProcesos(ciclo).Subarbol(ciclo[0]);

        Assert.Equal(8, Assert.Single(nodo.Hijos).Proceso.Pid);
        Assert.Empty(Assert.Single(nodo.Hijos).Hijos);
    }

    private static List<NodoDeProceso> Aplanar(IReadOnlyList<NodoDeProceso> bosque)
    {
        var todos = new List<NodoDeProceso>();

        void Bajar(IReadOnlyList<NodoDeProceso> nodos)
        {
            foreach (var n in nodos)
            {
                todos.Add(n);
                Bajar(n.Hijos);
            }
        }

        Bajar(bosque);
        return todos;
    }
}
