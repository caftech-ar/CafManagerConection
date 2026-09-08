using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Ssh.Tests;

public sealed class ContrasenaDeSudoDeSesionTests
{
    private const string Conocida = "clave-de-sudo-de-prueba-9f3a";

    [Fact]
    public void Una_contrasena_recien_nacida_no_tiene_nada_y_no_se_pidio()
    {
        using var contrasena = new ContrasenaDeSudoDeSesion();

        Assert.False(contrasena.Tiene);
        Assert.False(contrasena.YaSePidio);
        Assert.Equal(0, contrasena.LargoDelBufer);
    }

    [Fact]
    public void La_contrasena_se_entrega_mientras_la_sesion_vive()
    {
        using var contrasena = new ContrasenaDeSudoDeSesion();

        contrasena.Guardar(Conocida);

        Assert.True(contrasena.Tiene);
        Assert.Equal(Conocida, new string(contrasena.Prestada().Span));
    }

    [Fact]
    public void Guardar_nada_no_deja_una_contrasena_utilizable()
    {
        using var contrasena = new ContrasenaDeSudoDeSesion();

        contrasena.Guardar(string.Empty);

        Assert.False(contrasena.Tiene);
        Assert.True(contrasena.Prestada().IsEmpty);
    }

    [Fact]
    public void Al_cerrar_la_sesion_el_bufer_queda_en_ceros()
    {
        var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.Guardar(Conocida);

        contrasena.Cerrar();

        Assert.Equal(Conocida.Length, contrasena.LargoDelBufer);
        Assert.True(contrasena.BuferEnCeros);
        Assert.False(contrasena.Tiene);
        Assert.True(contrasena.Prestada().IsEmpty);
    }

    [Fact]
    public void Descartarla_la_pisa_con_ceros_pero_no_la_vuelve_a_pedir()
    {
        using var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.MarcarPedida();
        contrasena.Guardar(Conocida);

        contrasena.Descartar();

        Assert.True(contrasena.BuferEnCeros);
        Assert.False(contrasena.Tiene);
        Assert.True(contrasena.YaSePidio);
    }

    [Fact]
    public void Cerrar_la_sesion_deja_la_contrasena_como_recien_nacida()
    {
        var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.MarcarPedida();
        contrasena.Guardar(Conocida);

        contrasena.Cerrar();

        Assert.False(contrasena.YaSePidio);
    }

    [Fact]
    public void Reemplazar_la_contrasena_pisa_con_ceros_la_anterior()
    {
        using var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.Guardar(Conocida);

        var anterior = contrasena.Prestada();

        contrasena.Guardar("otra-distinta");

        Assert.DoesNotContain(Conocida, new string(anterior.Span), StringComparison.Ordinal);
        Assert.Equal("otra-distinta", new string(contrasena.Prestada().Span));
    }

    [Fact]
    public void Desecharla_pisa_el_bufer_con_ceros()
    {
        var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.Guardar(Conocida);

        contrasena.Dispose();

        Assert.True(contrasena.BuferEnCeros);
    }

    [Fact]
    public void Su_texto_no_revela_nada()
    {
        using var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.Guardar(Conocida);

        Assert.DoesNotContain(Conocida, contrasena.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cerrar_la_sesion_ssh_pisa_el_bufer_con_ceros()
    {
        var sesion = new SshSession(Pedido(), new SiempreAcepta());
        sesion.ContrasenaDeSudo.Guardar(Conocida);

        await sesion.DisconnectAsync();

        Assert.True(sesion.ContrasenaDeSudo.BuferEnCeros);
        Assert.False(sesion.ContrasenaDeSudo.Tiene);

        await sesion.DisposeAsync();
    }

    [Fact]
    public async Task Desechar_la_sesion_ssh_tambien_pisa_el_bufer_con_ceros()
    {
        var sesion = new SshSession(Pedido(), new SiempreAcepta());
        sesion.ContrasenaDeSudo.Guardar(Conocida);

        await sesion.DisposeAsync();

        Assert.True(sesion.ContrasenaDeSudo.BuferEnCeros);
        Assert.False(sesion.ContrasenaDeSudo.Tiene);
    }

    [Fact]
    public async Task Una_sesion_nueva_sobre_la_misma_conexion_nace_sin_contrasena()
    {
        var conexion = Guid.NewGuid();

        var primera = new SshSession(Pedido(conexion), new SiempreAcepta());
        primera.ContrasenaDeSudo.Guardar(Conocida);
        primera.ContrasenaDeSudo.MarcarPedida();
        await primera.DisposeAsync();

        await using var segunda = new SshSession(Pedido(conexion), new SiempreAcepta());

        Assert.False(segunda.ContrasenaDeSudo.Tiene);
        Assert.False(segunda.ContrasenaDeSudo.YaSePidio);
    }

    private static SshSessionRequest Pedido(Guid? conexion = null) =>
        new(
            ConnectionId: conexion ?? Guid.NewGuid(),
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

    private sealed class SiempreAcepta : IHostKeyVerifier
    {
        public HostKeyDecision Verify(
            Guid connectionId, string host, string fingerprint, string? known) =>
            HostKeyDecision.Accept;
    }
}
