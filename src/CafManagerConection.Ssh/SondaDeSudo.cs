using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Ssh;

public delegate Task<CommandResult> EjecutorRemoto(
    string comando, int timeoutSeconds, CancellationToken ct);

// LC_ALL=C obligatorio: un servidor en español traduce el aviso de sudo y la interpretación busca el texto en inglés.
public sealed class SondaDeSudo(EjecutorRemoto ejecutar)
{
    public const string Comando = "LC_ALL=C sudo -n true";

    private const int Espera = 10;

    private readonly SemaphoreSlim _puerta = new(1, 1);

    private ResultadoDeSondeo? _resultado;

    public ResultadoDeSondeo? Sondeado => _resultado;

    public int Sondeos { get; private set; }

    // Un sudo fallido de quien no está en sudoers deja una línea en el registro del servidor y le manda un correo a root: repetirlo por panel convierte un sondeo en una alarma (FR-184c).
    public async Task<ResultadoDeSondeo> SondearAsync(CancellationToken ct = default)
    {
        if (_resultado is { } ya)
        {
            return ya;
        }

        await _puerta.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_resultado is { } mientrasEsperaba)
            {
                return mientrasEsperaba;
            }

            var salida = await ejecutar(Comando, Espera, ct).ConfigureAwait(false);
            Sondeos++;

            _resultado = SondeoDeSudo.Interpretar(
                salida.ExitCode, salida.Output, salida.Error);

            return _resultado.Value;
        }
        finally
        {
            _puerta.Release();
        }
    }
}
