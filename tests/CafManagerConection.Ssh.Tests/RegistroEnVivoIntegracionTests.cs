using CafManagerConection.Platform;
using CafManagerConection.Ssh;
using Xunit;

namespace CafManagerConection.Ssh.Tests;

// El servidor necesita sh, "pgrep -f" y un sleep que acepte 0.2: POSIX solo exige enteros.
[Trait("Categoria", "IntegracionSsh")]
public sealed class RegistroEnVivoIntegracionTests
{
    private static SshCommandRunner Runner() => new(
        ServidorDePrueba.Pedido(),
        new ServidorDePrueba.AceptaTodo(),
        ServidorDePrueba.Credencial());

    [PruebaDeIntegracionSsh]
    public async Task Las_lineas_llegan_a_medida_que_el_proceso_las_escribe()
    {
        await using var runner = Runner();
        IPlatformLogStreamer streamer = runner;

        var lineas = new List<string>();
        var recibidas = new SemaphoreSlim(0);

        var canal = await streamer.SeguirAsync(
            "sh -c 'i=0; while true; do echo linea-$i; i=$((i+1)); sleep 0.2; done'",
            linea => { lock (lineas) { lineas.Add(linea); } recibidas.Release(); },
            _ => { },
            CancellationToken.None);

        try
        {
            for (var i = 0; i < 3; i++)
            {
                Assert.True(
                    await recibidas.WaitAsync(TimeSpan.FromSeconds(10)),
                    "No llegó ninguna línea nueva a tiempo.");
            }
        }
        finally
        {
            await canal.DisposeAsync();
        }

        lock (lineas)
        {
            Assert.Contains(lineas, l => l.StartsWith("linea-", StringComparison.Ordinal));
        }
    }

    [PruebaDeIntegracionSsh]
    public async Task Al_desechar_el_canal_el_proceso_remoto_termina()
    {
        var marca = $"cmc-canal-{Guid.NewGuid():N}";

        await using var runner = Runner();
        IPlatformLogStreamer streamer = runner;

        var recibioAlgo = new SemaphoreSlim(0);

        var canal = await streamer.SeguirAsync(
            $"sh -c 'echo {marca}; i=0; while true; do echo tick-$i; i=$((i+1)); sleep 0.2; done'",
            _ => recibioAlgo.Release(),
            _ => { },
            CancellationToken.None);

        Assert.True(
            await recibioAlgo.WaitAsync(TimeSpan.FromSeconds(10)),
            "El proceso remoto nunca llegó a escribir nada.");

        Assert.True(await SigueVivoAsync(runner, marca), "El proceso no llegó a arrancar.");

        await canal.DisposeAsync();

        // El proceso remoto recién se entera de que perdió su salida al escribir la próxima
        // línea, hasta 0.2 s después: se sondea antes de dar el defecto por confirmado.
        var siguioVivo = true;

        for (var intento = 0; intento < 20 && siguioVivo; intento++)
        {
            await Task.Delay(250);
            siguioVivo = await SigueVivoAsync(runner, marca);
        }

        Assert.False(
            siguioVivo,
            "El proceso remoto siguió corriendo después de cerrar el canal: es exactamente la "
            + "fuga que este mecanismo tiene que evitar.");
    }

    private static async Task<bool> SigueVivoAsync(SshCommandRunner runner, string marca)
    {
        var r = await runner.RunAsync($"pgrep -f {marca}", 10);
        return r.Success && !string.IsNullOrWhiteSpace(r.Output);
    }
}
