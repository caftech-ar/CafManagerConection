using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Sessions;
using Renci.SshNet.Common;

namespace CafManagerConection.Ssh.Tests;

public sealed class ReintentoConContrasenaTests
{
    private static readonly SshAuthenticationException FalloDeAutenticacion =
        new("No suitable authentication method found to complete authentication (password).");

    [Fact]
    public void Se_reintenta_cuando_el_servidor_nunca_pidio_nada() =>
        Assert.True(SshSession.HayQueReintentarConContraseña(
            FalloDeAutenticacion,
            preguntoPorTeclado: false,
            haySecretoGuardado: false,
            hayQuienPregunte: true));

    [Fact]
    public void No_se_reintenta_si_el_servidor_ya_habia_preguntado() =>
        Assert.False(SshSession.HayQueReintentarConContraseña(
            FalloDeAutenticacion,
            preguntoPorTeclado: true,
            haySecretoGuardado: false,
            hayQuienPregunte: true));

    [Fact]
    public void No_se_reintenta_si_habia_una_contrasena_guardada() =>
        Assert.False(SshSession.HayQueReintentarConContraseña(
            FalloDeAutenticacion,
            preguntoPorTeclado: false,
            haySecretoGuardado: true,
            hayQuienPregunte: true));

    [Fact]
    public void No_se_reintenta_si_no_hay_quien_pregunte() =>
        Assert.False(SshSession.HayQueReintentarConContraseña(
            FalloDeAutenticacion,
            preguntoPorTeclado: false,
            haySecretoGuardado: false,
            hayQuienPregunte: false));

    [Theory]
    [MemberData(nameof(FallosQueNoSonDeAutenticacion))]
    public void No_se_reintenta_ante_un_fallo_que_no_es_de_autenticacion(Exception fallo) =>
        Assert.False(SshSession.HayQueReintentarConContraseña(
            fallo,
            preguntoPorTeclado: false,
            haySecretoGuardado: false,
            hayQuienPregunte: true));

    public static TheoryData<Exception> FallosQueNoSonDeAutenticacion() =>
    [
        new SshConnectionException("El servidor cortó la conexión."),
        new SshOperationTimeoutException("Se agotó el tiempo de espera."),
        new System.Net.Sockets.SocketException(10061),
    ];

    [Fact]
    public async Task Un_fallo_de_red_no_pide_ninguna_contrasena()
    {
        var prompt = new PromptQueCuenta();

        var pedido = new SshSessionRequest(
            ConnectionId: Guid.NewGuid(),
            Host: "127.0.0.1",
            Port: 4,
            UserName: "testuser",
            AuthMethod: SshAuthMethod.Password,
            PrivateKeyPath: null,
            KnownHostFingerprint: null,
            KeepAliveSeconds: 0,
            InitialColumns: 80,
            InitialRows: 24,
            TimeoutSeconds: 2);

        await using var sesion = new SshSession(pedido, new SiempreAcepta(), null, prompt);

        await sesion.ConnectAsync(new StoredCredential("testuser", null, null));

        Assert.Equal(SessionState.Error, sesion.State);
        Assert.Equal(0, prompt.Veces);
    }

    private sealed class PromptQueCuenta : IInteractivePasswordPrompt
    {
        public int Veces { get; private set; }

        public Task<string?> PedirAsync(
            string userName, string host, int intento, string? errorPrevio, CancellationToken ct)
        {
            Veces++;
            return Task.FromResult<string?>("lo-que-sea");
        }
    }

    private sealed class SiempreAcepta : IHostKeyVerifier
    {
        public HostKeyDecision Verify(
            Guid connectionId, string host, string fingerprint, string? known) =>
            HostKeyDecision.Accept;
    }
}
