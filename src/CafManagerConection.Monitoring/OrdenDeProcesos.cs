namespace CafManagerConection.Monitoring;

public enum CriterioDeProcesos
{
    Cpu,
    Memoria,
    Disco,
}

/// <summary>Ordena las filas de procesos por lo que se está mirando (FR-183).</summary>
public static class OrdenDeProcesos
{
    // Un proceso recién arrancado no tiene porcentaje hasta la segunda muestra: sin el -1 quedaría arriba de todos con un cero.
    public static IReadOnlyList<ProcesoMedido> Ordenar(
        IReadOnlyList<ProcesoMedido> procesos, CriterioDeProcesos criterio) => criterio switch
    {
        CriterioDeProcesos.Memoria => [.. procesos
            .OrderByDescending(p => p.BytesResidentes)
            .ThenByDescending(p => p.PorcentajeDeCpu ?? -1)
            .ThenBy(p => p.Pid)],

        CriterioDeProcesos.Disco => [.. procesos
            .OrderByDescending(p => p.BytesDeDisco)
            .ThenByDescending(p => p.BytesResidentes)
            .ThenBy(p => p.Pid)],

        _ => [.. procesos
            .OrderByDescending(p => p.PorcentajeDeCpu ?? -1)
            .ThenByDescending(p => p.BytesResidentes)
            .ThenBy(p => p.Pid)],
    };

    public static IReadOnlyList<ProcesoMedido> Primeros(
        IReadOnlyList<ProcesoMedido> procesos, CriterioDeProcesos criterio, int cuantos) =>
        [.. Ordenar(procesos, criterio).Take(cuantos)];
}
