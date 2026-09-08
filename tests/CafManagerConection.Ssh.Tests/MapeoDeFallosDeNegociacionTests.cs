using CafManagerConection.Domain.Sessions;
using CafManagerConection.Ssh;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace CafManagerConection.Ssh.Tests;

// Los mensajes usados como entrada son literales fijos de SSH.NET 2026.0.0, verificados en
// ServiceFactory.CreateKeyExchange y Security.KeyExchange, no supuestos.
public sealed class MapeoDeFallosDeNegociacionTests
{
    [Theory]
    [InlineData(
        "No matching key exchange algorithm (server offers diffie-hellman-group14-sha256)",
        "intercambio de claves")]
    [InlineData(
        "No matching host key algorithm (server offers ssh-rsa,ssh-ed25519)",
        "clave de host")]
    [InlineData(
        "No matching client encryption algorithm (server offers 3des-cbc)",
        "cifrado")]
    [InlineData(
        "No matching server encryption algorithm (server offers 3des-cbc)",
        "cifrado")]
    [InlineData(
        "No matching client MAC algorithm (server offers hmac-md5)",
        "MAC")]
    [InlineData(
        "No matching server MAC algorithm (server offers hmac-md5)",
        "MAC")]
    public void Un_fallo_de_negociacion_dice_que_categoria_no_se_pudo_acordar(
        string mensajeDeSshNet, string categoriaEsperada)
    {
        var fallo = SshSession.Map(new SshConnectionException(mensajeDeSshNet));

        Assert.Equal(SessionFailureReason.AlgorithmNegotiationFailed, fallo.Reason);
        Assert.Contains(categoriaEsperada, fallo.UserMessage);
    }

    [Fact]
    public void El_mensaje_incluye_lo_que_ofrecia_el_servidor()
    {
        var fallo = SshSession.Map(new SshConnectionException(
            "No matching key exchange algorithm (server offers diffie-hellman-group1-sha1,"
            + "diffie-hellman-group14-sha1)"));

        Assert.Contains(
            "El servidor ofrece: diffie-hellman-group1-sha1,diffie-hellman-group14-sha1.",
            fallo.UserMessage);
    }

    [Fact]
    public void Sin_informacion_de_conexion_no_dice_que_ofrecia_el_cliente()
    {
        var fallo = SshSession.Map(new SshConnectionException(
            "No matching key exchange algorithm (server offers curve25519-sha256)"));

        Assert.DoesNotContain("Este cliente ofrece", fallo.UserMessage);
    }

    [Fact]
    public void Con_informacion_de_conexion_tambien_dice_que_ofrecia_este_cliente()
    {
        var info = new ConnectionInfo(
            "servidor", 22, "usuario", new PasswordAuthenticationMethod("usuario", "clave"));

        var fallo = SshSession.Map(
            new SshConnectionException(
                "No matching key exchange algorithm (server offers curve25519-sha256)"),
            info);

        Assert.Contains("Este cliente ofrece: ", fallo.UserMessage);
        Assert.True(info.KeyExchangeAlgorithms.Count > 0);
    }

    [Fact]
    public void El_detalle_tecnico_conserva_el_mensaje_original_de_SshNet()
    {
        const string mensaje = "No matching server MAC algorithm (server offers hmac-md5)";

        var fallo = SshSession.Map(new SshConnectionException(mensaje));

        Assert.Equal(mensaje, fallo.TechnicalDetail);
    }

    [Fact]
    public void No_se_confunde_con_una_huella_de_host_no_coincidente()
    {
        var fallo = SshSession.Map(new SshConnectionException(
            "No matching host key algorithm (server offers ssh-ed25519)"));

        Assert.Equal(SessionFailureReason.AlgorithmNegotiationFailed, fallo.Reason);
        Assert.NotEqual(SessionFailureReason.HostKeyMismatch, fallo.Reason);
    }

    [Fact]
    public void Una_huella_de_host_no_verificada_sigue_siendo_HostKeyMismatch()
    {
        var fallo = SshSession.Map(
            new SshConnectionException("Host key could not be verified."));

        Assert.Equal(SessionFailureReason.HostKeyMismatch, fallo.Reason);
    }

    [Fact]
    public void Agotar_los_intentos_de_contraseña_se_traduce_a_credenciales_rechazadas()
    {
        var fallo = SshSession.Map(new TooManyPasswordAttemptsException(3));

        Assert.Equal(SessionFailureReason.AuthenticationRejected, fallo.Reason);
        Assert.Contains("3", fallo.UserMessage);
    }
}
