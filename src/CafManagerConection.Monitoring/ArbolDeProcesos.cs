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
