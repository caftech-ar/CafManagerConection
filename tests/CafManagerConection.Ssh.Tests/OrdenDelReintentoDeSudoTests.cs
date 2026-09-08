namespace CafManagerConection.Ssh.Tests;

public sealed class OrdenDelReintentoDeSudoTests
{
    private const string DeLaConexion = "clave-de-conexion-de-prueba-4c81";

    private const string DelUsuario = "clave-de-sudo-tecleada-7be2";

    private const string Comando = "cat /etc/shadow";

    private const int Espera = 15;

    [Fact]
    public async Task Primero_se_prueba_la_contrasena_de_la_conexion()
    {
        var servidor = new ServidorFalso(aceptando: DeLaConexion);
        var pedido = new PedidoQueCuenta(DelUsuario);
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var resultado = await Orden(servidor, deSesion, pedido).IntentarAsync(Comando, Espera, default);

        Assert.True(resultado?.Success);
        Assert.Equal(1, servidor.Intentos);
        Assert.Equal(0, pedido.Veces);
        Assert.False(deSesion.Tiene);
    }

    [Fact]
    public async Task Solo_cuando_la_de_la_conexion_no_sirve_se_le_pide_una_al_usuario()
    {
        var servidor = new ServidorFalso(aceptando: DelUsuario);
        var pedido = new PedidoQueCuenta(DelUsuario);
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var resultado = await Orden(servidor, deSesion, pedido).IntentarAsync(Comando, Espera, default);

        Assert.True(resultado?.Success);
        Assert.Equal(1, pedido.Veces);
        Assert.Equal(new[] { DeLaConexion, DelUsuario }, servidor.Probadas);
        Assert.True(deSesion.Tiene);
    }

    [Fact]
    public async Task La_contrasena_que_sirvio_se_reusa_sin_volver_a_pedirla()
    {
        var servidor = new ServidorFalso(aceptando: DelUsuario);
        var pedido = new PedidoQueCuenta(DelUsuario);
        using var deSesion = new ContrasenaDeSudoDeSesion();
        var orden = Orden(servidor, deSesion, pedido);

        await orden.IntentarAsync(Comando, Espera, default);
        var segundo = await orden.IntentarAsync(Comando, Espera, default);

        Assert.True(segundo?.Success);
        Assert.Equal(1, pedido.Veces);
        Assert.Equal(new[] { DeLaConexion, DelUsuario, DelUsuario }, servidor.Probadas);
    }

    [Fact]
    public async Task Si_el_usuario_cancela_la_escalada_queda_imposible()
    {
        var servidor = new ServidorFalso(aceptando: DelUsuario);
        var pedido = new PedidoQueCuenta(contrasena: null);
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var resultado = await Orden(servidor, deSesion, pedido).IntentarAsync(Comando, Espera, default);

        Assert.Equal(OrdenDelReintentoDeSudo.SeCancelo, resultado?.Error);
        Assert.False(resultado?.Success);
        Assert.False(deSesion.Tiene);
    }

    [Fact]
    public async Task Si_la_que_escribio_el_usuario_tampoco_sirve_la_escalada_queda_imposible()
    {
        var servidor = new ServidorFalso(aceptando: "ninguna-de-las-dos");
        var pedido = new PedidoQueCuenta(DelUsuario);
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var resultado = await Orden(servidor, deSesion, pedido).IntentarAsync(Comando, Espera, default);

        Assert.Equal(OrdenDelReintentoDeSudo.SudoLaRechazo, resultado?.Error);
        Assert.False(deSesion.Tiene);
        Assert.True(deSesion.YaSePidio);
    }

    [Fact]
    public async Task Una_contrasena_equivocada_no_se_reintenta_en_bucle()
    {
        var servidor = new ServidorFalso(aceptando: "ninguna-de-las-dos");
        var pedido = new PedidoQueCuenta(DelUsuario);
        using var deSesion = new ContrasenaDeSudoDeSesion();
        var orden = Orden(servidor, deSesion, pedido);

        await orden.IntentarAsync(Comando, Espera, default);
        await orden.IntentarAsync(Comando, Espera, default);
        await orden.IntentarAsync(Comando, Espera, default);

        Assert.Equal(1, pedido.Veces);
        Assert.Equal(new[] { DeLaConexion, DelUsuario }, servidor.Probadas);
    }

    [Fact]
    public async Task Sin_quien_pregunte_no_hay_nada_mas_que_probar()
    {
        var servidor = new ServidorFalso(aceptando: DelUsuario);
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var resultado = await Orden(servidor, deSesion, pedido: null).IntentarAsync(Comando, Espera, default);

        Assert.Null(resultado);
        Assert.Equal(new[] { DeLaConexion }, servidor.Probadas);
    }

    [Fact]
    public async Task Sin_contrasena_de_conexion_se_le_pide_una_al_usuario_de_entrada()
    {
        var servidor = new ServidorFalso(aceptando: DelUsuario);
        var pedido = new PedidoQueCuenta(DelUsuario);
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var resultado = await new OrdenDelReintentoDeSudo(
                servidor.ConLaDeLaConexionAsync,
                servidor.ConUnaContrasenaAsync,
                () => false,
                deSesion,
                pedido,
                "servidor-de-prueba",
                "testuser")
            .IntentarAsync(Comando, Espera, default);

        Assert.True(resultado?.Success);
        Assert.Equal(new[] { DelUsuario }, servidor.Probadas);
    }

    [Fact]
    public async Task Un_fallo_que_no_es_de_contrasena_se_devuelve_tal_cual_y_no_pide_nada()
    {
        var servidor = new ServidorFalso(aceptando: DelUsuario)
        {
            FalloAjeno = "sudo: unable to resolve host servidor-uno",
        };

        var pedido = new PedidoQueCuenta(DelUsuario);
        using var deSesion = new ContrasenaDeSudoDeSesion();

        var resultado = await Orden(servidor, deSesion, pedido).IntentarAsync(Comando, Espera, default);

        Assert.Equal("sudo: unable to resolve host servidor-uno", resultado?.Error);
        Assert.Equal(0, pedido.Veces);
    }

    [Fact]
    public async Task Dos_paneles_a_la_vez_piden_la_contrasena_una_sola_vez()
    {
        var servidor = new ServidorFalso(aceptando: DelUsuario);
        var pedido = new PedidoQueCuenta(DelUsuario) { Demora = TimeSpan.FromMilliseconds(60) };
        using var deSesion = new ContrasenaDeSudoDeSesion();
        var orden = Orden(servidor, deSesion, pedido);

        var ambos = await Task.WhenAll(
            orden.IntentarAsync(Comando, Espera, default), orden.IntentarAsync(Comando, Espera, default));

        Assert.Equal(1, pedido.Veces);
        Assert.All(ambos, r => Assert.True(r?.Success));
    }

    private static OrdenDelReintentoDeSudo Orden(
        ServidorFalso servidor,
        ContrasenaDeSudoDeSesion deSesion,
        IPedidoDeContrasenaDeSudo? pedido) =>
        new(
            servidor.ConLaDeLaConexionAsync,
            servidor.ConUnaContrasenaAsync,
            () => true,
            deSesion,
            pedido,
            "servidor-de-prueba",
            "testuser");

    /// <summary>Un sudo que acepta una sola contraseña y anota todas las que le llegaron.</summary>
    private sealed class ServidorFalso(string aceptando)
    {
        public List<string> Probadas { get; } = [];

        public string? FalloAjeno { get; init; }

        public int Intentos => Probadas.Count;

        public Task<IntentoDeEscalada> ConLaDeLaConexionAsync(
            string comando, int espera, CancellationToken ct) =>
            ConUnaContrasenaAsync(comando, espera, DeLaConexion.AsMemory(), ct);

        public Task<IntentoDeEscalada> ConUnaContrasenaAsync(
            string comando, int espera, ReadOnlyMemory<char> contrasena, CancellationToken ct)
        {
            var probada = new string(contrasena.Span);

            lock (Probadas)
            {
                Probadas.Add(probada);
            }

            if (FalloAjeno is { } ajeno)
            {
                return Task.FromResult(
                    new IntentoDeEscalada(new CommandResult(1, string.Empty, ajeno), false));
            }

            return Task.FromResult(
                string.Equals(probada, aceptando, StringComparison.Ordinal)
                    ? new IntentoDeEscalada(new CommandResult(0, "root", string.Empty), false)
                    : new IntentoDeEscalada(
                        new CommandResult(1, string.Empty, "sudo: 1 incorrect password attempt"),
                        true));
        }
    }

    private sealed class PedidoQueCuenta(string? contrasena) : IPedidoDeContrasenaDeSudo
    {
        public int Veces { get; private set; }

        public TimeSpan Demora { get; init; }

        public async Task<bool> PedirAsync(
            string servidor, string usuario, ContrasenaDeSudoDeSesion destino, CancellationToken ct)
        {
            Veces++;

            if (Demora > TimeSpan.Zero)
            {
                await Task.Delay(Demora, ct).ConfigureAwait(false);
            }

            if (contrasena is null)
            {
                return false;
            }

            destino.Guardar(contrasena);

            return true;
        }
    }
}
