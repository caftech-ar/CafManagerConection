using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Rdp.Tests;

/// <summary>Qué ajustes arma el plan para rendimiento, escala, modo de tamaño y programa inicial. No instancia el control.</summary>
public sealed class OpcionesDePantallaTests
{
    private static RdpSessionRequest Pedido(
        ModoDeTamanoRdp modo = ModoDeTamanoRdp.EscalarPixeles,
        BanderasDeRendimientoRdp rendimiento = BanderasDeRendimientoRdp.Ninguna,
        TipoDeRedRdp? tipoDeRed = null,
        int? escritorio = null,
        int? dispositivo = null,
        string? programa = null,
        string? directorio = null,
        bool remoteApp = false) => new(
        ConnectionId: Guid.NewGuid(),
        Host: "srv01.interno",
        Port: 3389,
        UserName: "operador",
        Domain: null,
        ClipboardEnabled: false,
        ModoDeTamano: modo,
        IgnoreCertificateWarnings: false,
        TimeoutSeconds: 15,
        UseWindowsIdentity: false,
        Rendimiento: rendimiento,
        TipoDeRed: tipoDeRed,
        EscalaDeEscritorio: escritorio,
        EscalaDeDispositivo: dispositivo,
        ProgramaInicial: programa,
        DirectorioDeTrabajo: directorio,
        ComoRemoteApp: remoteApp);

    private static PlanDeSesionRdp Plan(RdpSessionRequest peticion) =>
        PlanDeSesionRdp.Para(peticion, "guardado", hayContrasena: true);

    private static bool Asigna(PlanDeSesionRdp plan, AmbitoDeRdp ambito, string propiedad) =>
        plan.Ajustes.Any(a => a.Ambito == ambito && a.Propiedad == propiedad);

    private static object? Valor(PlanDeSesionRdp plan, string propiedad) =>
        plan.Ajustes.First(a => a.Propiedad == propiedad).Valor;

    [Fact]
    public void Escalar_pixeles_prende_SmartSizing()
    {
        var plan = Plan(Pedido(modo: ModoDeTamanoRdp.EscalarPixeles));

        Assert.Equal(true, Valor(plan, "SmartSizing"));
    }

    [Theory]
    [InlineData(ModoDeTamanoRdp.Ninguno)]
    [InlineData(ModoDeTamanoRdp.RenegociarResolucion)]
    public void Los_demas_modos_dejan_SmartSizing_apagado(ModoDeTamanoRdp modo)
    {
        var plan = Plan(Pedido(modo: modo));

        Assert.Equal(false, Valor(plan, "SmartSizing"));
    }

    [Fact]
    public void Las_banderas_de_rendimiento_van_como_mascara_de_bits_en_avanzados()
    {
        var rendimiento = BanderasDeRendimientoRdp.SinFondoDeEscritorio
                          | BanderasDeRendimientoRdp.SinAnimacionesDeMenu;

        var plan = Plan(Pedido(rendimiento: rendimiento));

        Assert.True(Asigna(plan, AmbitoDeRdp.Avanzados, "PerformanceFlags"));
        Assert.Equal(0x01 | 0x04, Valor(plan, "PerformanceFlags"));
    }

    [Fact]
    public void Sin_banderas_de_rendimiento_no_se_asigna_PerformanceFlags()
    {
        var plan = Plan(Pedido(rendimiento: BanderasDeRendimientoRdp.Ninguna));

        Assert.False(Asigna(plan, AmbitoDeRdp.Avanzados, "PerformanceFlags"));
    }

    [Fact]
    public void El_tipo_de_red_va_como_entero_en_avanzados()
    {
        var plan = Plan(Pedido(tipoDeRed: TipoDeRedRdp.Lan));

        Assert.Equal((int)TipoDeRedRdp.Lan, Valor(plan, "NetworkConnectionType"));
    }

    [Fact]
    public void La_escala_va_al_ambito_extendido()
    {
        var plan = Plan(Pedido(escritorio: 150, dispositivo: 140));

        Assert.True(Asigna(plan, AmbitoDeRdp.Extendida, "DesktopScaleFactor"));
        Assert.True(Asigna(plan, AmbitoDeRdp.Extendida, "DeviceScaleFactor"));
        Assert.Equal(150u, Valor(plan, "DesktopScaleFactor"));
        Assert.Equal(140u, Valor(plan, "DeviceScaleFactor"));
    }

    [Fact]
    public void Sin_escala_no_se_asigna_ninguna_extendida()
    {
        var plan = Plan(Pedido());

        Assert.False(Asigna(plan, AmbitoDeRdp.Extendida, "DesktopScaleFactor"));
        Assert.False(Asigna(plan, AmbitoDeRdp.Extendida, "DeviceScaleFactor"));
    }

    [Fact]
    public void El_programa_inicial_va_a_asegurados_con_su_directorio()
    {
        var plan = Plan(Pedido(programa: "C:/app/gestion.exe", directorio: "C:/app"));

        Assert.Equal("C:/app/gestion.exe", Valor(plan, "StartProgram"));
        Assert.Equal("C:/app", Valor(plan, "WorkDir"));
        Assert.False(Asigna(plan, AmbitoDeRdp.Avanzados, "RemoteProgramMode"));
    }

    [Fact]
    public void Sin_programa_inicial_no_se_asigna_StartProgram()
    {
        var plan = Plan(Pedido());

        Assert.False(Asigna(plan, AmbitoDeRdp.Asegurados, "StartProgram"));
    }

    [Fact]
    public void RemoteApp_prende_el_modo_de_programa_remoto_y_no_usa_StartProgram()
    {
        var plan = Plan(Pedido(programa: "GestionRemota", remoteApp: true));

        Assert.Equal(true, Valor(plan, "RemoteProgramMode"));
        Assert.False(Asigna(plan, AmbitoDeRdp.Asegurados, "StartProgram"));
    }

    [Fact]
    public void RemoteApp_sin_programa_no_prende_nada()
    {
        var plan = Plan(Pedido(remoteApp: true));

        Assert.False(Asigna(plan, AmbitoDeRdp.Avanzados, "RemoteProgramMode"));
    }
}
