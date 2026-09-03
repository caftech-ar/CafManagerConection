using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Ssh.Tests;

// Necesita un servidor donde el usuario esté en sudoers para la prueba de integración; las demás
// corren siempre.
public sealed class SC052_ContrasenaDeSudoNoQuedaEnRegistroTests
{
    private const string DeLaConexion = "clave-de-conexion-de-prueba-4c81";

    private const string DelUsuario = "clave-de-sudo-tecleada-7be2";

    [Fact]
    public void La_contrasena_desaparece_del_texto_de_una_salida()
    {
        var limpio = SinElSecreto.En($"bash: {DeLaConexion}: command not found", DeLaConexion);

        Assert.DoesNotContain(DeLaConexion, limpio, StringComparison.Ordinal);
        Assert.Contains(SinElSecreto.Omitido, limpio, StringComparison.Ordinal);
    }

    [Fact]
    public void Desaparecen_todas_las_apariciones_y_no_solo_la_primera()
    {
        var limpio = SinElSecreto.En(
            $"{DeLaConexion} al principio, {DeLaConexion} al final", DeLaConexion);

        Assert.DoesNotContain(DeLaConexion, limpio, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_texto_que_no_la_menciona_queda_igual()
    {
        const string Tal = "sudo: 1 incorrect password attempt";

        Assert.Equal(Tal, SinElSecreto.En(Tal, DeLaConexion));
    }

    [Fact]
    public void Un_secreto_vacio_no_toca_el_texto()
    {
        const string Tal = "cat: /etc/shadow: Permission denied";

        Assert.Equal(Tal, SinElSecreto.En(Tal, ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void El_resultado_de_un_comando_queda_sin_la_contrasena_en_las_dos_salidas()
    {
        var sucio = new CommandResult(
            127, $"eco de {DeLaConexion}", $"bash: {DeLaConexion}: command not found");

        var limpio = SinElSecreto.En(sucio, DeLaConexion);

        Assert.DoesNotContain(DeLaConexion, limpio.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(DeLaConexion, limpio.Error, StringComparison.Ordinal);
        Assert.Equal(127, limpio.ExitCode);
    }

    [Fact]
    public async Task Un_error_forzado_que_repite_lo_que_tecleo_el_usuario_no_lo_devuelve()
    {
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var orden = OrdenQueDevuelve(
            (_, _, _) => Devuelto(Rechazo()),
            (_, _, contrasena, _) => Devuelto(EcoDe(new string(contrasena.Span))),
            deSesion,
            new PedidoQueEscribe(DelUsuario));

        var resultado = await orden.IntentarAsync("cat /etc/shadow", 15, CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.DoesNotContain(DelUsuario, resultado!.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(DelUsuario, resultado.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_motivo_de_una_escalada_imposible_no_menciona_lo_que_se_tecleo()
    {
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var orden = OrdenQueDevuelve(
            (_, _, _) => Devuelto(Rechazo()),
            (_, _, _, _) => Devuelto(Rechazo()),
            deSesion,
            new PedidoQueEscribe(DelUsuario));

        var resultado = await orden.IntentarAsync("cat /etc/shadow", 15, CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.DoesNotContain(DelUsuario, resultado!.Error, StringComparison.Ordinal);
        Assert.Equal(OrdenDelReintentoDeSudo.SudoLaRechazo, resultado.Error);
    }

    [PruebaDeIntegracionSsh]
    public async Task La_contrasena_de_la_conexion_no_aparece_en_la_traza_ni_en_el_registro()
    {
        var trazas = new TrazasQueGuardan();
        var registro = new RegistroQueGuarda();
        var contrasena = ServidorDePrueba.Credencial().RevealSecret();

        await using var runner = new SshCommandRunner(
            ServidorDePrueba.Pedido(),
            new ServidorDePrueba.AceptaTodo(),
            ServidorDePrueba.Credencial(),
            registro,
            trazas,
            "servidor-de-prueba");

        Assert.True(await runner.ConnectAsync());

        await runner.RunWithSudoFallbackAsync("cat /etc/shadow", 15);

        Assert.NotEmpty(trazas.Textos);
        Assert.DoesNotContain(contrasena, trazas.Todo(), StringComparison.Ordinal);
        Assert.DoesNotContain(contrasena, registro.Todo(), StringComparison.Ordinal);
    }

    private static CommandResult EcoDe(string contrasena) =>
        new(127, $"eco de {contrasena}", $"bash: {contrasena}: command not found");

    private static CommandResult Rechazo() =>
        new(1, string.Empty, "sudo: 1 incorrect password attempt");

    private static Task<IntentoDeEscalada> Devuelto(CommandResult resultado) =>
        Task.FromResult(new IntentoDeEscalada(
            resultado, SshCommandRunner.SudoRechazoLaContrasena(resultado.Error)));

    private static OrdenDelReintentoDeSudo OrdenQueDevuelve(
        Func<string, int, CancellationToken, Task<IntentoDeEscalada>> conLaDeLaConexion,
        Func<string, int, ReadOnlyMemory<char>, CancellationToken, Task<IntentoDeEscalada>>
            conUnaContrasena,
        ContrasenaDeSudoDeSesion? deSesion,
        IPedidoDeContrasenaDeSudo? pedido) =>
        new(
            conLaDeLaConexion,
            conUnaContrasena,
            () => true,
            deSesion,
            pedido,
            "servidor-de-prueba",
            "testuser");

    private sealed class PedidoQueEscribe(string contrasena) : IPedidoDeContrasenaDeSudo
    {
        public Task<bool> PedirAsync(
            string servidor, string usuario, ContrasenaDeSudoDeSesion destino, CancellationToken ct)
        {
            destino.Guardar(contrasena);
            return Task.FromResult(true);
        }
    }
}
