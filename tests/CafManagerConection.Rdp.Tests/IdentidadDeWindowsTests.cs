using CafManagerConection.Domain.Sessions;
using CafManagerConection.Rdp;

namespace CafManagerConection.Rdp.Tests;

/// <summary>Qué se le asigna al ActiveX con el tilde de identidad de Windows y sin él (FR-186). No instancia el control: el plan es una decisión, no una llamada a COM.</summary>
public sealed class IdentidadDeWindowsTests
{
    private static RdpSessionRequest Pedido(bool identidad, string usuario = "operador") => new(
        ConnectionId: Guid.NewGuid(),
        Host: "srv01.interno",
        Port: 3389,
        UserName: usuario,
        Domain: "CORP",
        ClipboardEnabled: false,
        FitToTab: true,
        IgnoreCertificateWarnings: false,
        TimeoutSeconds: 15,
        UseWindowsIdentity: identidad);

    private static PlanDeSesionRdp Plan(bool identidad, bool hayContrasena = true) =>
        PlanDeSesionRdp.Para(Pedido(identidad), "guardado", hayContrasena);

    private static bool Asigna(PlanDeSesionRdp plan, string propiedad) =>
        plan.Ajustes.Any(a => a.Propiedad == propiedad);

    private static object? Valor(PlanDeSesionRdp plan, string propiedad) =>
        plan.Ajustes.First(a => a.Propiedad == propiedad).Valor;

    [Fact]
    public void Con_la_identidad_de_Windows_no_se_asigna_la_contrasena_aunque_haya_una_guardada()
    {
        var plan = Plan(identidad: true, hayContrasena: true);

        Assert.False(plan.AsignaContrasena);
    }

    [Fact]
    public void Con_la_identidad_de_Windows_no_se_asigna_usuario_ni_dominio()
    {
        var plan = Plan(identidad: true);

        Assert.False(plan.AsignaUsuario);
        Assert.False(Asigna(plan, "UserName"));
        Assert.False(Asigna(plan, "Domain"));
    }

    [Fact]
    public void Con_la_identidad_de_Windows_se_pide_CredSsp_y_la_negociacion_de_seguridad()
    {
        var plan = Plan(identidad: true);

        Assert.Equal(true, Valor(plan, "EnableCredSspSupport"));
        Assert.Equal(true, Valor(plan, "NegotiateSecurityLayer"));
    }

    [Fact]
    public void Sin_el_tilde_se_asignan_usuario_dominio_y_contrasena_como_siempre()
    {
        var plan = Plan(identidad: false);

        Assert.True(plan.AsignaUsuario);
        Assert.True(plan.AsignaContrasena);
        Assert.Equal("operador", Valor(plan, "UserName"));
        Assert.Equal("CORP", Valor(plan, "Domain"));
    }

    [Fact]
    public void Sin_el_tilde_y_sin_contrasena_guardada_no_se_asigna_ninguna()
    {
        var plan = Plan(identidad: false, hayContrasena: false);

        Assert.False(plan.AsignaContrasena);
    }

    [Fact]
    public void Sin_usuario_propio_se_usa_el_de_la_credencial()
    {
        var plan = PlanDeSesionRdp.Para(Pedido(identidad: false, usuario: "  "), "guardado", true);

        Assert.Equal("guardado", Valor(plan, "UserName"));
    }

    [Fact]
    public void El_plan_nunca_lleva_la_contrasena_entre_sus_ajustes()
    {
        var plan = Plan(identidad: false);

        Assert.False(Asigna(plan, "ClearTextPassword"));
    }

    [Theory]
    [InlineData("RedirectDrives")]
    [InlineData("RedirectPrinters")]
    [InlineData("RedirectPorts")]
    [InlineData("RedirectSmartCards")]
    [InlineData("RedirectPOSDevices")]
    [InlineData("RedirectDirectX")]
    [InlineData("AudioCaptureRedirectionMode")]
    public void La_identidad_de_Windows_no_reabre_ninguna_redireccion(string propiedad)
    {
        Assert.Equal(false, Valor(Plan(identidad: true), propiedad));
    }

    [Fact]
    public void El_audio_sigue_en_no_reproducir_y_rdpdr_deshabilitado()
    {
        var plan = Plan(identidad: true);

        Assert.Equal(2, Valor(plan, "AudioRedirectionMode"));
        Assert.Equal(1, Valor(plan, "DisableRdpdr"));
    }

    [Fact]
    public void El_servidor_y_el_puerto_se_asignan_en_los_dos_casos()
    {
        foreach (var identidad in new[] { true, false })
        {
            var plan = Plan(identidad);

            Assert.Equal("srv01.interno", Valor(plan, "Server"));
            Assert.Equal(3389, Valor(plan, "RDPPort"));
        }
    }

    [Theory]
    [InlineData(SessionFailureReason.AuthenticationRejected, true)]
    [InlineData(SessionFailureReason.UnexpectedDisconnect, true)]
    [InlineData(SessionFailureReason.CredentialMissing, true)]
    [InlineData(SessionFailureReason.Other, true)]
    [InlineData(SessionFailureReason.Timeout, false)]
    [InlineData(SessionFailureReason.HostUnreachable, false)]
    [InlineData(SessionFailureReason.CertificateUntrusted, false)]
    public void El_respaldo_se_activa_cuando_el_servidor_no_confia_y_no_cuando_no_se_llega(
        SessionFailureReason motivo, bool cae)
    {
        Assert.Equal(
            cae,
            RdpSession.ConvieneCaerAlPedidoDeCredenciales(
                usoIdentidadDeWindows: true, llegoAConectar: false, motivo));
    }

    [Fact]
    public void Sin_el_tilde_no_hay_respaldo_que_activar()
    {
        Assert.False(RdpSession.ConvieneCaerAlPedidoDeCredenciales(
            usoIdentidadDeWindows: false,
            llegoAConectar: false,
            SessionFailureReason.AuthenticationRejected));
    }

    [Fact]
    public void Una_sesion_que_ya_estuvo_conectada_no_vuelve_a_pedir_credenciales_al_cortarse()
    {
        Assert.False(RdpSession.ConvieneCaerAlPedidoDeCredenciales(
            usoIdentidadDeWindows: true,
            llegoAConectar: true,
            SessionFailureReason.UnexpectedDisconnect));
    }
}
