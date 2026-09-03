using CafManagerConection.Ssh;

namespace CafManagerConection.Ssh.Tests;

// El servidor necesita el printf de POSIX, que interpreta las secuencias de escape.
[Trait("Categoria", "IntegracionSsh")]
public sealed class NormalizacionDeSalidaTests
{
    private static SshCommandRunner Runner() => new(
        ServidorDePrueba.Pedido(),
        new ServidorDePrueba.AceptaTodo(),
        ServidorDePrueba.Credencial());

    [PruebaDeIntegracionSsh]
    public async Task La_salida_con_CRLF_llega_normalizada_a_LF()
    {
        await using var runner = Runner();

        var r = await runner.RunAsync(@"printf 'primera\r\nsegunda\r\n'", 15);

        Assert.True(r.Success, $"El comando falló: {r.Error}");
        Assert.DoesNotContain('\r', r.Output);
        Assert.Equal("primera\nsegunda\n", r.Output);
    }
}
