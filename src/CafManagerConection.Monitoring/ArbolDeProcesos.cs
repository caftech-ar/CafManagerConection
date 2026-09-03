namespace CafManagerConection.Monitoring;

public sealed record NodoDeProceso(ProcesoMedido Proceso, IReadOnlyList<NodoDeProceso> Hijos)
{
    public double CpuDelSubarbol =>
        (Proceso.PorcentajeDeCpu ?? 0) + Hijos.Sum(h => h.CpuDelSubarbol);

    public long BytesResidentesDelSubarbol =>
        Proceso.BytesResidentes + Hijos.Sum(h => h.BytesResidentesDelSubarbol);
}

/// <summary>Los hijos de cada proceso, indexados una vez para no recorrer la tabla por fila.</summary>
public sealed class IndiceDeProcesos
{
    private readonly Dictionary<int, List<ProcesoMedido>> _hijosDe;

    public IndiceDeProcesos(IReadOnlyList<ProcesoMedido> procesos) =>
        _hijosDe = procesos
            .Where(p => p.PidPadre != p.Pid)
            .GroupBy(p => p.PidPadre)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Pid).ToList());

    public IReadOnlyList<ProcesoMedido> HijosDirectos(int pid) =>
        _hijosDe.TryGetValue(pid, out var hijos) ? hijos : [];

    public NodoDeProceso Subarbol(ProcesoMedido raiz) => Nodo(raiz, []);

    private NodoDeProceso Nodo(ProcesoMedido proceso, HashSet<int> vistos)
    {
        if (!vistos.Add(proceso.Pid) || !_hijosDe.TryGetValue(proceso.Pid, out var hijos))
        {
            return new NodoDeProceso(proceso, []);
        }

        // Sin descartar a los ya vistos, un ciclo de PPID pone al mismo proceso dos veces en el árbol.
        return new NodoDeProceso(
            proceso,
            [.. hijos.Where(h => !vistos.Contains(h.Pid)).Select(h => Nodo(h, vistos))]);
    }
}

public static class ArbolDeProcesos
{
    public static IReadOnlyList<ProcesoMedido> HijosDirectos(
        IReadOnlyList<ProcesoMedido> procesos, int pid) =>
        procesos.Where(p => p.PidPadre == pid && p.Pid != pid).ToList();

    public static IReadOnlyList<NodoDeProceso> Armar(IReadOnlyList<ProcesoMedido> procesos)
    {
        var pids = procesos.Select(p => p.Pid).ToHashSet();

        var hijosDe = procesos
            .Where(p => p.PidPadre != p.Pid)
            .GroupBy(p => p.PidPadre)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Pid).ToList());

        var armados = new HashSet<int>();
        var bosque = new List<NodoDeProceso>();

        foreach (var raiz in procesos.Where(p => !pids.Contains(p.PidPadre) || p.PidPadre == p.Pid)
                     .OrderBy(p => p.Pid))
        {
            bosque.Add(Nodo(raiz, hijosDe, armados));
        }

        // Un ciclo en los PPID no tiene raíz y sin esto no entra al bosque: /proc leído a pedazos puede darlo.
        foreach (var suelto in procesos.OrderBy(p => p.Pid).Where(p => !armados.Contains(p.Pid)))
        {
            bosque.Add(Nodo(suelto, hijosDe, armados));
        }

        return bosque;
    }

    private static NodoDeProceso Nodo(
        ProcesoMedido proceso,
        Dictionary<int, List<ProcesoMedido>> hijosDe,
        HashSet<int> armados)
    {
        armados.Add(proceso.Pid);

        if (!hijosDe.TryGetValue(proceso.Pid, out var hijos))
        {
            return new NodoDeProceso(proceso, []);
        }

        var nodos = new List<NodoDeProceso>(hijos.Count);

        foreach (var hijo in hijos.Where(h => !armados.Contains(h.Pid)))
        {
            nodos.Add(Nodo(hijo, hijosDe, armados));
        }

        return new NodoDeProceso(proceso, nodos);
    }
}
