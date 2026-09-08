namespace CafManagerConection.Monitoring;

public static class ParserDeProcesos
{
    public const string Marca = "###CMC###";

    public const int BytesPorPaginaPorOmision = 4096;

    /// <summary>Cuántos tramos van antes del primer proceso.</summary>
    public const int Encabezados = 5;

    // La marca va entre comillas simples: un # al principio de una palabra abre un comentario de shell y todo el comando va en una línea.
    private const string MarcaCitada = "'" + Marca + "'";

    // stat sólo hace stat() de los directorios: el dueño de los 700 procesos sale en un proceso y sin abrir un archivo por PID.
    private static readonly string Comando = string.Join(
        "; ",
        [
            "getconf CLK_TCK 2>/dev/null",
            $"echo {MarcaCitada}",
            "getconf PAGESIZE 2>/dev/null",
            $"echo {MarcaCitada}",
            "cut -d: -f1,3 /etc/passwd 2>/dev/null",
            $"echo {MarcaCitada}",
            "cat /proc/uptime 2>/dev/null",
            $"echo {MarcaCitada}",
            "stat -c '%u %n' /proc/[0-9]* 2>/dev/null",
            "for d in /proc/[0-9]*",
            "do echo " + MarcaCitada,
            "cat \"$d/stat\" 2>/dev/null && cat \"$d/io\" 2>/dev/null",
            "done",
            "exit 0",
        ]);

    public static string ComandoDeLectura => Comando;

    public static MuestraDeProcesos Parse(string salida, DateTimeOffset instante)
    {
        var partes = salida.ReplaceLineEndings("\n").Split(Marca);

        var ticsPorSegundo = Entero(Tramo(partes, 0)) ?? MuestraDeProcesos.TicsPorSegundoPorOmision;
        var bytesPorPagina = Entero(Tramo(partes, 1)) ?? BytesPorPaginaPorOmision;
        var usuarios = DatosDeSistemaParser.UsuariosPorUid(Tramo(partes, 2));
        var encendido = Segundos(Tramo(partes, 3));
        var duenos = Duenos(Tramo(partes, 4));

        var procesos = new List<ProcesoCrudo>(Math.Max(0, partes.Length - Encabezados));

        for (var i = Encabezados; i < partes.Length; i++)
        {
            if (Bloque(partes[i], bytesPorPagina, duenos) is { } proceso)
            {
                procesos.Add(proceso);
            }
        }

        return new MuestraDeProcesos(instante, procesos, ticsPorSegundo, encendido, usuarios);
    }

    /// <summary>Dueño de cada proceso, del <c>stat -c '%u %n'</c> sobre los directorios de /proc.</summary>
    public static IReadOnlyDictionary<int, int> Duenos(string salida)
    {
        var duenos = new Dictionary<int, int>();

        foreach (var linea in salida.ReplaceLineEndings("\n").Split('\n'))
        {
            var campos = linea.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (campos.Length < 2 || !int.TryParse(campos[0], out var uid) || uid < 0)
            {
                continue;
            }

            var barra = campos[^1].LastIndexOf('/');

            if (barra >= 0 && int.TryParse(campos[^1].AsSpan(barra + 1), out var pid) && pid > 0)
            {
                duenos[pid] = uid;
            }
        }

        return duenos;
    }

    // campos[k] es el campo k+3 de /proc/<pid>/stat: se corta por el último ) porque el nombre puede traer espacios y paréntesis, como ((sd-pam)).
    public static ProcesoCrudo? ParsearStat(
        string linea, int bytesPorPagina = BytesPorPaginaPorOmision)
    {
        var apertura = linea.IndexOf('(');
        var cierre = linea.LastIndexOf(')');

        if (apertura < 0 || cierre < apertura)
        {
            return null;
        }

        if (!int.TryParse(linea.AsSpan(0, apertura).Trim(), out var pid) || pid <= 0)
        {
            return null;
        }

        var campos = linea[(cierre + 1)..]
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        if (campos.Length < 22)
        {
            return null;
        }

        return new ProcesoCrudo(
            pid,
            (int)Largo(campos[1]),
            linea[(apertura + 1)..cierre],
            campos[0],
            Largo(campos[11]),
            Largo(campos[12]),
            Largo(campos[19]),
            Largo(campos[21]) * bytesPorPagina,
            Math.Max(1, (int)Largo(campos[17])));
    }

    private static ProcesoCrudo? Bloque(
        string bloque, int bytesPorPagina, IReadOnlyDictionary<int, int> duenos)
    {
        var lineas = bloque.Split('\n');
        var primera = Array.FindIndex(lineas, l => l.Trim().Length > 0);

        if (primera < 0 || ParsearStat(lineas[primera], bytesPorPagina) is not { } proceso)
        {
            return null;
        }

        var io = ParserDeIo.Parse(string.Join('\n', lineas.Skip(primera + 1)));

        return proceso with
        {
            BytesLeidos = io.BytesLeidos,
            BytesEscritos = io.BytesEscritos,
            Uid = duenos.TryGetValue(proceso.Pid, out var uid) ? uid : -1,
        };
    }

    private static string Tramo(string[] partes, int i) =>
        i < partes.Length ? partes[i].Trim() : string.Empty;

    private static int? Entero(string texto) =>
        int.TryParse(texto, out var valor) && valor > 0 ? valor : null;

    private static long Largo(string texto) => long.TryParse(texto, out var valor) ? valor : 0;

    private static double Segundos(string uptime)
    {
        var primero = uptime.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries);

        return primero.Length > 0
               && double.TryParse(
                   primero[0],
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var valor)
               && valor > 0
            ? valor
            : 0;
    }
}
