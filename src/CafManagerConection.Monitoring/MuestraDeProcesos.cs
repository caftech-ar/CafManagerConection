namespace CafManagerConection.Monitoring;

public sealed record ProcesoCrudo(
    int Pid,
    int PidPadre,
    string Nombre,
    string Estado,
    long TicsDeUsuario,
    long TicsDeSistema,
    long TicDeArranque,
    long BytesResidentes,
    int Hilos,
    long? BytesLeidos = null,
    long? BytesEscritos = null,
    int Uid = -1)
{
    public long TicsDeCpu => TicsDeUsuario + TicsDeSistema;
}

public sealed record ProcesoMedido(
    int Pid,
    int PidPadre,
    string Nombre,
    string Estado,
    double? PorcentajeDeCpu,
    long BytesResidentes,
    int Hilos,
    long? BytesLeidos,
    long? BytesEscritos,
    string Usuario = "",
    TimeSpan? TiempoCorriendo = null)
{
    public long BytesDeDisco => (BytesLeidos ?? 0) + (BytesEscritos ?? 0);

    public bool TieneDisco => BytesLeidos is not null || BytesEscritos is not null;
}

public sealed record MuestraDeProcesos(
    DateTimeOffset Instante,
    IReadOnlyList<ProcesoCrudo> Procesos,
    int TicsPorSegundo = MuestraDeProcesos.TicsPorSegundoPorOmision,
    double SegundosEncendido = 0,
    IReadOnlyDictionary<string, string>? Usuarios = null)
{
    // getconf CLK_TCK devuelve 100 en todo Linux de escritorio y servidor; el valor real lo trae la muestra y esto es sólo el respaldo.
    public const int TicsPorSegundoPorOmision = 100;

    public IReadOnlyList<ProcesoMedido> SinMedir() =>
        Procesos.Select(p => Fila(p, null)).ToList();

    public static IReadOnlyList<ProcesoMedido> Entre(
        MuestraDeProcesos anterior, MuestraDeProcesos actual)
    {
        var segundos = (actual.Instante - anterior.Instante).TotalSeconds;
        var previos = PorPid(anterior.Procesos);

        return actual.Procesos
            .Select(p => actual.Fila(p, Porcentaje(p, previos, segundos, actual.TicsPorSegundo)))
            .ToList();
    }

    private static Dictionary<int, ProcesoCrudo> PorPid(IReadOnlyList<ProcesoCrudo> procesos)
    {
        var porPid = new Dictionary<int, ProcesoCrudo>(procesos.Count);

        foreach (var p in procesos)
        {
            porPid[p.Pid] = p;
        }

        return porPid;
    }

    private static double? Porcentaje(
        ProcesoCrudo proceso,
        Dictionary<int, ProcesoCrudo> previos,
        double segundos,
        int ticsPorSegundo)
    {
        if (segundos <= 0 || ticsPorSegundo <= 0)
        {
            return null;
        }

        if (!previos.TryGetValue(proceso.Pid, out var antes))
        {
            return null;
        }

        if (antes.TicDeArranque != proceso.TicDeArranque)
        {
            return null;
        }

        var consumidos = proceso.TicsDeCpu - antes.TicsDeCpu;

        return consumidos < 0 ? null : consumidos * 100.0 / (segundos * ticsPorSegundo);
    }

    private ProcesoMedido Fila(ProcesoCrudo p, double? porcentaje) =>
        new(
            p.Pid,
            p.PidPadre,
            p.Nombre,
            p.Estado,
            porcentaje,
            p.BytesResidentes,
            p.Hilos,
            p.BytesLeidos,
            p.BytesEscritos,
            Usuario(p.Uid),
            Corriendo(p.TicDeArranque));

    private string Usuario(int uid)
    {
        if (uid < 0)
        {
            return string.Empty;
        }

        var texto = uid.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return Usuarios?.TryGetValue(texto, out var nombre) == true ? nombre : texto;
    }

    private TimeSpan? Corriendo(long ticDeArranque)
    {
        if (SegundosEncendido <= 0 || TicsPorSegundo <= 0)
        {
            return null;
        }

        var desde = (double)ticDeArranque / TicsPorSegundo;

        return TimeSpan.FromSeconds(Math.Max(0, SegundosEncendido - desde));
    }
}
