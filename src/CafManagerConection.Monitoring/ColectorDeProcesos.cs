namespace CafManagerConection.Monitoring;

public sealed class ColectorDeProcesos(
    IRemoteCommandRunner runner,
    TimeProvider? time = null,
    IRemoteCommandRunner? conPrivilegios = null)
{
    private readonly TimeProvider _reloj = time ?? TimeProvider.System;

    private MuestraDeProcesos? _anterior;

    public string? UltimoError { get; private set; }

    public bool TienePorcentajes => _anterior is not null;

    /// <summary>Si esta sesión tiene por dónde escalar; sin eso el panel no ofrece el botón (FR-184a).</summary>
    public bool PuedeEscalar => conPrivilegios is not null;

    public bool ConPrivilegios { get; set; }

    public async Task<IReadOnlyList<ProcesoMedido>?> MedirAsync(
        int timeoutSeconds, CancellationToken ct = default)
    {
        var lector = ConPrivilegios && conPrivilegios is { } elevado ? elevado : runner;

        var (ok, salida, error) = await lector
            .RunAsync(ParserDeProcesos.ComandoDeLectura, timeoutSeconds, ct)
            .ConfigureAwait(false);

        if (!ok || string.IsNullOrWhiteSpace(salida))
        {
            UltimoError = ok
                ? "El servidor no devolvió ningún proceso. Puede no ser Linux, o no exponer /proc."
                : error?.Trim() is { Length: > 0 } motivo
                    ? motivo
                    : "La lectura de procesos falló y el canal no dijo por qué.";

            return null;
        }

        UltimoError = null;

        var actual = ParserDeProcesos.Parse(salida, _reloj.GetUtcNow());

        var filas = _anterior is { } previa
            ? MuestraDeProcesos.Entre(previa, actual)
            : actual.SinMedir();

        _anterior = actual;

        return filas;
    }

    public void Olvidar()
    {
        _anterior = null;
        UltimoError = null;
    }
}
