using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.Ssh.Tests;

public sealed class AutenticacionPorCertificadoTests : IDisposable
{
    // Clave ed25519 sin passphrase, generada con "ssh-keygen -t ed25519 -N ''" sólo para estas
    // pruebas.
    private const string ClavePrivada = """
        -----BEGIN OPENSSH PRIVATE KEY-----
        b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
        QyNTUxOQAAACCrkGAv89tfBhJIiO+wjHXP3gp6OA8LAcbUsXQnGyCXLwAAAJDvxZlz78WZ
        cwAAAAtzc2gtZWQyNTUxOQAAACCrkGAv89tfBhJIiO+wjHXP3gp6OA8LAcbUsXQnGyCXLw
        AAAEA8BBkQxWrtCnpLGZZ13StNS/OeZXSHC3UTtn8BHsN6lauQYC/z218GEkiI77CMdc/e
        Cno4DwsBxtSxdCcbIJcvAAAAC2NtYy1wcnVlYmFzAQI=
        -----END OPENSSH PRIVATE KEY-----
        """;

    // Certificado firmado sobre la clave de arriba: "ssh-keygen -s ca -I test-user -n testuser
    // -V always:forever id_test.pub".
    private const string CertificadoQueCorresponde =
        "ssh-ed25519-cert-v01@openssh.com AAAAIHNzaC1lZDI1NTE5LWNlcnQtdjAxQG9wZW5zc2gu"
        + "Y29tAAAAIBJoLvjStj7o6HxMLTnmGMURRs5II9YTTGnvbNBVk/+zAAAAIKuQYC/z218GEkiI77CMdc/eCno4DwsBxtSx"
        + "dCcbIJcvAAAAAAAAAAAAAAABAAAACXRlc3QtdXNlcgAAAAwAAAAIdGVzdHVzZXIAAAAAAAAAAP//////////AAAAAAAA"
        + "AIIAAAAVcGVybWl0LVgxMS1mb3J3YXJkaW5nAAAAAAAAABdwZXJtaXQtYWdlbnQtZm9yd2FyZGluZwAAAAAAAAAWcGVy"
        + "bWl0LXBvcnQtZm9yd2FyZGluZwAAAAAAAAAKcGVybWl0LXB0eQAAAAAAAAAOcGVybWl0LXVzZXItcmMAAAAAAAAAAAAA"
        + "ADMAAAALc3NoLWVkMjU1MTkAAAAguc5s5G5yXL7d4cYpdjxyiXdm6Rozkwve5Z89aac8gIUAAABTAAAAC3NzaC1lZDI1"
        + "NTE5AAAAQJHaonWJV69D0SEVv5JyIv0qKbyDkI3uMJxe+Vs3jyPNh2s+eouAJ5XByUwqmrfamx7SBAxwAh5FtMd9gPjS"
        + "EQA= cmc-pruebas";

    // Certificado firmado por la misma CA, pero sobre OTRA clave: sirve para el caso de
    // desajuste, donde el certificado es válido pero no corresponde a esta clave privada.
    private const string CertificadoQueNoCorresponde =
        "ssh-ed25519-cert-v01@openssh.com AAAAIHNzaC1lZDI1NTE5LWNlcnQtdjAxQG9wZW5zc2gu"
        + "Y29tAAAAIJJuzhaIXqKpPLyvNYs2xmnnDOaKYfZMObR/Xuv2hq4mAAAAICpCFdZHkObgXZP6oArntcmD1mjIbJPP+YEj"
        + "TaOFjeZ7AAAAAAAAAAAAAAABAAAACm90aGVyLXVzZXIAAAAMAAAACHRlc3R1c2VyAAAAAAAAAAD//////////wAAAAAA"
        + "AACCAAAAFXBlcm1pdC1YMTEtZm9yd2FyZGluZwAAAAAAAAAXcGVybWl0LWFnZW50LWZvcndhcmRpbmcAAAAAAAAAFnBl"
        + "cm1pdC1wb3J0LWZvcndhcmRpbmcAAAAAAAAACnBlcm1pdC1wdHkAAAAAAAAADnBlcm1pdC11c2VyLXJjAAAAAAAAAAAA"
        + "AAAzAAAAC3NzaC1lZDI1NTE5AAAAILnObORucly+3eHGKXY8col3ZukaM5ML3uWfPWmnPICFAAAAUwAAAAtzc2gtZWQy"
        + "NTUxOQAAAEBkNAjYrMgq0IlBftn+rpQJ8YMrPAaJ5C2G2BubnfrdrnmmOgeMh+QvDCecVgeSc1UNqUf1AGldc2mwGTUQ"
        + "4LcN cmc-pruebas";

    private readonly string _dir;
    private readonly string _clave;

    public AutenticacionPorCertificadoTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cmc-cert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        _clave = Path.Combine(_dir, "id_test");
        File.WriteAllText(_clave, ClavePrivada.Replace("\r\n", "\n") + "\n");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string EscribirCertificado(string nombre, string contenido)
    {
        var ruta = Path.Combine(_dir, nombre);
        File.WriteAllText(ruta, contenido + "\n");
        return ruta;
    }

    private SshSessionRequest Pedido(
        string? certificatePath, string host = "127.0.0.1", int port = 4, int timeout = 3) =>
        new(
            ConnectionId: Guid.NewGuid(),
            Host: host,
            Port: port,
            UserName: "testuser",
            AuthMethod: SshAuthMethod.PrivateKey,
            PrivateKeyPath: _clave,
            KnownHostFingerprint: null,
            KeepAliveSeconds: 0,
            InitialColumns: 80,
            InitialRows: 24,
            TimeoutSeconds: timeout,
            CertificatePath: certificatePath);

    [Fact]
    public async Task Un_certificado_que_no_existe_da_un_mensaje_especifico_y_no_generico()
    {
        var pedido = Pedido(Path.Combine(_dir, "no-existe-cert.pub"));
        await using var sesion = new SshSession(pedido, new SiempreAcepta());

        await sesion.ConnectAsync(new StoredCredential("testuser", null, string.Empty));

        Assert.Equal(SessionState.Error, sesion.State);
        Assert.NotNull(sesion.Failure);
        Assert.Equal(SessionFailureReason.CertificateNotFound, sesion.Failure.Reason);
        Assert.Contains("certificado", sesion.Failure.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_certificado_de_otra_clave_dice_que_no_corresponde_y_no_solo_que_fallo()
    {
        var certificado = EscribirCertificado("otra-cert.pub", CertificadoQueNoCorresponde);
        var pedido = Pedido(certificado);
        await using var sesion = new SshSession(pedido, new SiempreAcepta());

        await sesion.ConnectAsync(new StoredCredential("testuser", null, string.Empty));

        Assert.Equal(SessionState.Error, sesion.State);
        Assert.NotNull(sesion.Failure);
        Assert.Equal(SessionFailureReason.CertificateMismatch, sesion.Failure.Reason);
        Assert.Contains(
            "no corresponde", sesion.Failure.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sin_certificado_configurado_el_camino_de_la_clave_privada_no_cambia()
    {
        var pedido = Pedido(certificatePath: null);
        await using var sesion = new SshSession(pedido, new SiempreAcepta());

        await sesion.ConnectAsync(new StoredCredential("testuser", null, string.Empty));

        Assert.Equal(SessionState.Error, sesion.State);
        Assert.NotNull(sesion.Failure);
        Assert.DoesNotContain(
            sesion.Failure.Reason,
            (IEnumerable<SessionFailureReason>)
            [SessionFailureReason.PrivateKeyNotFound,
             SessionFailureReason.CertificateNotFound,
             SessionFailureReason.CertificateMismatch]);
    }

    [Fact]
    public async Task Un_certificado_que_corresponde_a_la_clave_llega_hasta_el_intento_de_red()
    {
        var certificado = EscribirCertificado("id_test-cert.pub", CertificadoQueCorresponde);
        var pedido = Pedido(certificado);
        await using var sesion = new SshSession(pedido, new SiempreAcepta());

        await sesion.ConnectAsync(new StoredCredential("testuser", null, string.Empty));

        Assert.Equal(SessionState.Error, sesion.State);
        Assert.NotNull(sesion.Failure);
        Assert.DoesNotContain(
            sesion.Failure.Reason,
            (IEnumerable<SessionFailureReason>)
            [SessionFailureReason.PrivateKeyNotFound,
             SessionFailureReason.CertificateNotFound,
             SessionFailureReason.CertificateMismatch]);
    }

    private sealed class SiempreAcepta : IHostKeyVerifier
    {
        public HostKeyDecision Verify(Guid connectionId, string host, string fingerprint, string? known) =>
            HostKeyDecision.Accept;
    }
}
