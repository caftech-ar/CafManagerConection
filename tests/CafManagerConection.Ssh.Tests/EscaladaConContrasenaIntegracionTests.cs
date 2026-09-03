using CafManagerConection.UseCases.Abstractions;
using Xunit;

namespace CafManagerConection.Ssh.Tests;

// El servidor necesita el usuario de CMC_SSH_PRUEBA_USUARIO_SUDO con «PASSWD: ALL» en sudoers,
// la misma contraseña para entrar y para sudo, y un /etc/shadow que empiece con "root:".
// Las de EscaladaASudoIntegracionTests corren contra el usuario sin contraseña y por eso no
// tocaban la entrada estándar: ahí vivió el defecto de CreateInputStream.
[Trait("Categoria", "IntegracionSsh")]
public sealed class EscaladaConContrasenaIntegracionTests
{
    private const string SoloRoot = "cat /etc/shadow";

    [PruebaDeSudoConContrasena]
    public async Task El_sudo_que_pide_contrasena_no_prospera_sin_ella()
    {
        await using var runner = Runner();

        var sinPreguntar = await runner.RunAsync($"sudo -n {Envuelto(SoloRoot)}", 15);

        Assert.False(sinPreguntar.Success);
        Assert.True(
            sinPreguntar.NeedsSudoPassword,
            $"No se reconoció que hacía falta la contraseña. Error: {sinPreguntar.Error}");
    }

    [PruebaDeSudoConContrasena]
    public async Task La_contrasena_de_la_conexion_llega_a_sudo_por_la_entrada_estandar()
    {
        await using var runner = Runner();

        var conSudo = await runner.RunWithSudoFallbackAsync(SoloRoot, 15);

        Assert.True(conSudo.Success, $"La escalada no prosperó: {conSudo.Error}");
        Assert.Contains("root:", conSudo.Output, StringComparison.Ordinal);
    }

    [PruebaDeSudoConContrasena]
    public async Task La_contrasena_no_aparece_en_la_traza_ni_en_el_registro()
    {
        var trazas = new TrazasQueGuardan();
        var registro = new RegistroQueGuarda();
        var contrasena = ServidorDePrueba.CredencialDelSudo().RevealSecret();

        await using var runner = new SshCommandRunner(
            ServidorDePrueba.PedidoDelSudo(),
            new ServidorDePrueba.AceptaTodo(),
            ServidorDePrueba.CredencialDelSudo(),
            registro,
            trazas,
            "servidor-de-prueba");

        var conSudo = await runner.RunWithSudoFallbackAsync(SoloRoot, 15);

        Assert.True(conSudo.Success, $"La escalada no prosperó: {conSudo.Error}");
        Assert.NotEmpty(trazas.Textos);

        Assert.DoesNotContain(contrasena, trazas.Todo(), StringComparison.Ordinal);
        Assert.DoesNotContain(contrasena, registro.Todo(), StringComparison.Ordinal);
    }

    private static SshCommandRunner Runner() => new(
        ServidorDePrueba.PedidoDelSudo(),
        new ServidorDePrueba.AceptaTodo(),
        ServidorDePrueba.CredencialDelSudo());

    private static string Envuelto(string guion) => ShellPosix.ComoUnSoloComando(guion);
}
