using CafManagerConection.Ssh;
using Xunit;

namespace CafManagerConection.Ssh.Tests;

// El servidor necesita sudo sin contraseña para CMC_SSH_PRUEBA_USUARIO, un /etc/shadow que
// empiece con "root:", y que "id -un" devuelva ese mismo usuario.
[Trait("Categoria", "IntegracionSsh")]
public sealed class EscaladaASudoIntegracionTests
{
    private static SshCommandRunner Runner() => new(
        ServidorDePrueba.Pedido(),
        new ServidorDePrueba.AceptaTodo(),
        ServidorDePrueba.Credencial());

    [PruebaDeIntegracionSsh]
    public async Task Un_comando_que_falla_por_permisos_se_reintenta_con_sudo_y_prospera()
    {
        await using var runner = Runner();

        var sinSudo = await runner.RunAsync("cat /etc/shadow", 15);

        Assert.False(sinSudo.Success);
        Assert.True(
            sinSudo.LooksLikePermissionDenied,
            $"No se reconoció como falta de permiso. Salida: {sinSudo.Output} "
            + $"Error: {sinSudo.Error}");

        var conSudo = await runner.RunWithSudoFallbackAsync("cat /etc/shadow", 15);

        Assert.True(conSudo.Success, $"La escalada no prosperó: {conSudo.Error}");
        Assert.Contains("root:", conSudo.Output, StringComparison.Ordinal);
    }

    [PruebaDeIntegracionSsh]
    public async Task Un_comando_que_no_necesita_sudo_no_lo_usa()
    {
        await using var runner = Runner();

        var r = await runner.RunWithSudoFallbackAsync("id -un", 15);

        Assert.True(r.Success);

        Assert.Equal(ServidorDePrueba.Usuario, r.Output.Trim());
    }

    [PruebaDeIntegracionSsh]
    public async Task Un_comando_que_no_existe_no_se_reintenta_con_sudo()
    {
        await using var runner = Runner();

        var r = await runner.RunWithSudoFallbackAsync("comando-que-no-existe-cmc", 15);

        Assert.False(r.Success);
        Assert.False(r.LooksLikePermissionDenied);
    }

    // Reproduce la forma de la respuesta de supervisord en servidor-uno: estado de salida
    // distinto de cero con la respuesta completa en la salida estándar.
    [PruebaDeIntegracionSsh]
    public async Task Un_estado_de_salida_no_cero_con_salida_valida_llega_tal_cual()
    {
        await using var runner = Runner();

        var r = await runner.RunWithSudoFallbackAsync("echo tabla-completa; exit 3", 15);

        Assert.False(r.Success);
        Assert.Equal(3, r.ExitCode);
        Assert.Contains("tabla-completa", r.Output, StringComparison.Ordinal);
    }
}
