namespace CafManagerConection.Monitoring;

/// <summary>La muestra de procesos de una sesión, compartida por los paneles que la miran (SC-050a).</summary>
public sealed class MonitorDeProcesos(ColectorDeProcesos colector, TimeProvider? time = null)
{
    private readonly TimeProvider _reloj = time ?? TimeProvider.System;

    private Task<IReadOnlyList<ProcesoMedido>?>? _enCurso;

    /// <summary>Cuántas veces se le pidió la tabla al servidor, para poder medir el costo.</summary>
    public int Lecturas { get; private set; }

    public IReadOnlyList<ProcesoMedido>? Ultima { get; private set; }

    public DateTimeOffset? Instante { get; private set; }

    public string? UltimoError => colector.UltimoError;

    public bool TienePorcentajes => colector.TienePorcentajes;

    public bool PuedeEscalar => colector.PuedeEscalar;

    public bool ConPrivilegios
    {
        get => colector.ConPrivilegios;
        set => colector.ConPrivilegios = value;
    }

    /// <summary>La muestra vigente si no pasó de <paramref name="frescura"/>, y si no una nueva. Dos paneles con el mismo intervalo cuestan una sola lectura.</summary>
    public async Task<IReadOnlyList<ProcesoMedido>?> MuestraAsync(
        TimeSpan frescura, int timeoutSeconds, CancellationToken ct = default)
    {
        if (Ultima is { } vigente
            && Instante is { } cuando
            && _reloj.GetUtcNow() - cuando <= frescura)
        {
            return vigente;
        }

        if (_enCurso is { } yaPedida)
        {
            return await yaPedida.ConfigureAwait(false);
        }

        var lectura = LeerAsync(timeoutSeconds, ct);
        _enCurso = lectura;

        try
        {
            return await lectura.ConfigureAwait(false);
        }
        finally
        {
            _enCurso = null;
        }
    }

    public void Olvidar()
    {
        colector.Olvidar();
        Ultima = null;
        Instante = null;
    }

    private async Task<IReadOnlyList<ProcesoMedido>?> LeerAsync(
        int timeoutSeconds, CancellationToken ct)
    {
        Lecturas++;

        var filas = await colector.MedirAsync(timeoutSeconds, ct).ConfigureAwait(false);

        if (filas is not null)
        {
            Ultima = filas;
            Instante = _reloj.GetUtcNow();
        }

        return filas;
    }
}
