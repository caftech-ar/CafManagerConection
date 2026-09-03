using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Sessions;

namespace CafManagerConection.UseCases.Tests.Sessions;

// T057, T134, T135.
public sealed class SessionManagerTests
{
    private static Connection Conexion(string nombre = "Aplicaciones") =>
        new(Guid.NewGuid(), nombre, Protocol.Ssh, "192.0.2.1");

    private sealed class SuperficieFalsa : ISessionSurface
    {
        public SessionState State { get; set; } = SessionState.Disconnected;

        public int Conexiones { get; private set; }

        public int Activaciones { get; private set; }

        public bool Cerrada { get; private set; }

        public Exception? FalloAlConectar { get; set; }

        public Exception? FalloAlCerrar { get; set; }

        public SessionState EstadoAlConectar { get; set; } = SessionState.Connected;

        public event EventHandler<SessionStateChanged>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            Conexiones++;

            if (FalloAlConectar is { } fallo)
            {
                Avisar(SessionState.Error);
                return Task.FromException(fallo);
            }

            Avisar(EstadoAlConectar);
            return Task.CompletedTask;
        }

        public void Activate() => Activaciones++;

        public void Dispose()
        {
            Cerrada = true;

            if (FalloAlCerrar is { } fallo)
            {
                throw fallo;
            }
        }

        public void Avisar(SessionState estado)
        {
            State = estado;
            StateChanged?.Invoke(this, new SessionStateChanged(estado));
        }
    }

    private sealed class AnfitrionFalso : ISessionHost
    {
        private readonly Queue<SuperficieFalsa> _preparadas = new();

        public List<SuperficieFalsa> Creadas { get; } = [];

        public Exception? FalloAlCrear { get; set; }

        public void Preparar(params SuperficieFalsa[] superficies)
        {
            foreach (var s in superficies)
            {
                _preparadas.Enqueue(s);
            }
        }

        public ISessionSurface Create(Guid sessionId, ConnectionRecord connection)
        {
            if (FalloAlCrear is { } fallo)
            {
                throw fallo;
            }

            var superficie = _preparadas.Count > 0 ? _preparadas.Dequeue() : new SuperficieFalsa();
            Creadas.Add(superficie);
            return superficie;
        }
    }

    private sealed class RelojFalso(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _ahora = inicio;

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan cuanto) => _ahora = _ahora.Add(cuanto);
    }

    private sealed class HistorialFalso : IConnectionHistoryRepository
    {
        public List<ConnectionHistoryEntry> Anotados { get; } = [];

        public Exception? FalloAlEscribir { get; set; }

        public Task AddAsync(ConnectionHistoryEntry entry, CancellationToken ct = default)
        {
            if (FalloAlEscribir is { } fallo)
            {
                return Task.FromException(fallo);
            }

            Anotados.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConnectionHistoryEntry>> GetForConnectionAsync(
            Guid connectionId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ConnectionHistoryEntry>>(
                [.. Anotados.Where(a => a.ConnectionId == connectionId)]);

        public Task<IReadOnlyList<ConnectionHistoryEntry>> GetRecentAsync(
            int limit = 500, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ConnectionHistoryEntry>>([.. Anotados]);
    }

    private sealed class Banco
    {
        public SessionRegistry Registro { get; } = new();

        public HistorialFalso Historial { get; } = new();

        public RelojFalso Reloj { get; } = new(
            new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero));

        public AnfitrionFalso Anfitrion { get; } = new();

        public Connection Conexion { get; } = SessionManagerTests.Conexion();

        public bool ConexionBorrada { get; set; }

        public Exception? FalloAlBuscar { get; set; }

        public SessionManager Crear() => new(
            Registro,
            (id, _) =>
            {
                if (FalloAlBuscar is { } fallo)
                {
                    return Task.FromException<ConnectionRecord?>(fallo);
                }

                return Task.FromResult<ConnectionRecord?>(
                    ConexionBorrada || id != Conexion.Id
                        ? null
                        : new ConnectionRecord(Conexion));
            },
            Anfitrion,
            logger: null,
            historial: Historial,
            reloj: Reloj);
    }

    [Fact]
    public async Task Abrir_registra_la_sesion_y_la_conecta()
    {
        var banco = new Banco();
        var gestor = banco.Crear();

        var r = await gestor.OpenAsync(banco.Conexion.Id);

        Assert.True(r.Success);
        Assert.Single(gestor.ActiveSessions);
        Assert.Equal(1, banco.Anfitrion.Creadas[0].Conexiones);
    }

    // FR-044a.
    [Fact]
    public async Task Abrir_una_conexion_ya_abierta_trae_al_frente_la_que_esta()
    {
        var banco = new Banco();
        var gestor = banco.Crear();

        var primera = await gestor.OpenAsync(banco.Conexion.Id);
        var segunda = await gestor.OpenAsync(banco.Conexion.Id);

        Assert.Equal(primera.Value, segunda.Value);
        Assert.Single(gestor.ActiveSessions);
        Assert.Single(banco.Anfitrion.Creadas);
        Assert.Equal(1, banco.Anfitrion.Creadas[0].Activaciones);
    }

    [Fact]
    public async Task Con_forceNew_se_abre_otra_sesion_de_la_misma_conexion()
    {
        var banco = new Banco();
        var gestor = banco.Crear();

        await gestor.OpenAsync(banco.Conexion.Id);
        var segunda = await gestor.OpenAsync(banco.Conexion.Id, forceNew: true);

        Assert.True(segunda.Success);
        Assert.Equal(2, gestor.ActiveSessions.Count);
        Assert.Equal(2, gestor.CountForConnection(banco.Conexion.Id));
    }

    [Fact]
    public async Task Una_conexion_que_ya_no_existe_falla_con_mensaje_y_no_deja_sesion()
    {
        var banco = new Banco { ConexionBorrada = true };
        var gestor = banco.Crear();

        var r = await gestor.OpenAsync(banco.Conexion.Id);

        Assert.False(r.Success);
        Assert.Equal("La conexión ya no existe.", r.ErrorMessage);
        Assert.Empty(gestor.ActiveSessions);
    }

    [Fact]
    public async Task Si_la_base_explota_al_buscar_la_conexion_no_se_propaga()
    {
        var banco = new Banco { FalloAlBuscar = new InvalidOperationException("base caída") };
        var gestor = banco.Crear();

        var r = await gestor.OpenAsync(banco.Conexion.Id);

        Assert.False(r.Success);
        Assert.Empty(gestor.ActiveSessions);
    }

    [Fact]
    public async Task Si_no_se_puede_crear_la_superficie_no_queda_una_sesion_fantasma()
    {
        var banco = new Banco();
        banco.Anfitrion.FalloAlCrear = new InvalidOperationException("sin control ActiveX");
        var gestor = banco.Crear();

        var r = await gestor.OpenAsync(banco.Conexion.Id);

        Assert.False(r.Success);
        Assert.Empty(gestor.ActiveSessions);
    }

    // T057.
    [Fact]
    public async Task Un_fallo_al_conectar_deja_la_sesion_en_Error_y_no_lanza()
    {
        var banco = new Banco();
        banco.Anfitrion.Preparar(new SuperficieFalsa
        {
            FalloAlConectar = new TimeoutException("no respondió"),
        });

        var gestor = banco.Crear();

        var r = await gestor.OpenAsync(banco.Conexion.Id);

        Assert.True(r.Success);
        Assert.Single(gestor.ActiveSessions);
        Assert.Equal(SessionState.Error, gestor.ActiveSessions[0].State);
    }

    [Fact]
    public async Task El_estado_que_reporta_la_sesion_llega_al_registro()
    {
        var banco = new Banco();
        var superficie = new SuperficieFalsa();
        banco.Anfitrion.Preparar(superficie);
        var gestor = banco.Crear();

        await gestor.OpenAsync(banco.Conexion.Id);
        superficie.Avisar(SessionState.Disconnected);

        Assert.Equal(SessionState.Disconnected, gestor.ActiveSessions[0].State);
    }

    [Fact]
    public async Task Reconectar_una_sesion_caida_la_vuelve_a_conectar_en_su_lugar()
    {
        var banco = new Banco();
        var superficie = new SuperficieFalsa();
        banco.Anfitrion.Preparar(superficie);
        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        superficie.Avisar(SessionState.Disconnected);

        var r = await gestor.ReconnectAsync(abierta.Value);

        Assert.True(r.Success);
        Assert.Equal(2, superficie.Conexiones);

        Assert.Single(banco.Anfitrion.Creadas);
        Assert.Single(gestor.ActiveSessions);
    }

    [Fact]
    public async Task Reconectar_una_sesion_que_ya_esta_conectada_no_hace_nada()
    {
        var banco = new Banco();
        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        var r = await gestor.ReconnectAsync(abierta.Value);

        Assert.False(r.Success);
        Assert.Equal(1, banco.Anfitrion.Creadas[0].Conexiones);
    }

    [Fact]
    public async Task Reconectar_una_sesion_que_ya_no_esta_abierta_falla_con_mensaje()
    {
        var gestor = new Banco().Crear();

        var r = await gestor.ReconnectAsync(Guid.NewGuid());

        Assert.False(r.Success);
        Assert.Equal("La sesión ya no está abierta.", r.ErrorMessage);
    }

    [Fact]
    public async Task Si_la_reconexion_falla_la_sesion_queda_en_Error()
    {
        var banco = new Banco();
        var superficie = new SuperficieFalsa();
        banco.Anfitrion.Preparar(superficie);
        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        superficie.Avisar(SessionState.Disconnected);
        superficie.FalloAlConectar = new TimeoutException("sigue sin responder");

        var r = await gestor.ReconnectAsync(abierta.Value);

        Assert.False(r.Success);
        Assert.Equal(SessionState.Error, gestor.ActiveSessions[0].State);
    }

    [Fact]
    public async Task Cerrar_saca_la_sesion_del_registro_y_desmonta_la_superficie()
    {
        var banco = new Banco();
        var superficie = new SuperficieFalsa();
        banco.Anfitrion.Preparar(superficie);
        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        gestor.Close(abierta.Value);

        Assert.Empty(gestor.ActiveSessions);
        Assert.True(superficie.Cerrada);
    }

    [Fact]
    public void Cerrar_una_sesion_que_no_esta_no_es_un_error()
    {
        var gestor = new Banco().Crear();

        gestor.Close(Guid.NewGuid());

        Assert.Empty(gestor.ActiveSessions);
    }

    // FR-054, SC-012.
    [Fact]
    public async Task Una_sesion_que_explota_al_cerrarse_no_impide_cerrar_las_demas()
    {
        var banco = new Banco();
        var explota = new SuperficieFalsa { FalloAlCerrar = new InvalidOperationException("COM") };
        var sana1 = new SuperficieFalsa();
        var sana2 = new SuperficieFalsa();
        banco.Anfitrion.Preparar(sana1, explota, sana2);

        var gestor = banco.Crear();

        await gestor.OpenAsync(banco.Conexion.Id);
        await gestor.OpenAsync(banco.Conexion.Id, forceNew: true);
        await gestor.OpenAsync(banco.Conexion.Id, forceNew: true);

        gestor.CloseAll();

        Assert.Empty(gestor.ActiveSessions);
        Assert.True(sana1.Cerrada);
        Assert.True(sana2.Cerrada);
        Assert.True(explota.Cerrada);
    }

    [Fact]
    public async Task Cerrar_todo_sin_sesiones_abiertas_no_rompe()
    {
        var gestor = new Banco().Crear();

        gestor.CloseAll();
        gestor.CloseAll();

        Assert.Empty(gestor.ActiveSessions);
        await Task.CompletedTask;
    }

    // FR-009: cuenta si llegó a conectar alguna vez, no el estado en que quedó.
    [Fact]
    public async Task Cerrar_una_sesion_que_conecto_anota_exito_con_su_duracion()
    {
        var banco = new Banco();
        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        banco.Reloj.Avanzar(TimeSpan.FromMinutes(37));
        await gestor.CloseAsync(abierta.Value);

        var anotado = Assert.Single(banco.Historial.Anotados);
        Assert.Equal(ConnectionOutcome.Success, anotado.Outcome);
        Assert.Equal(banco.Conexion.Id, anotado.ConnectionId);
        Assert.Equal(37 * 60, anotado.DurationSeconds);
    }

    [Fact]
    public async Task La_duracion_se_mide_desde_que_se_abrio_la_sesion()
    {
        var banco = new Banco();
        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        var inicio = banco.Reloj.GetUtcNow();

        banco.Reloj.Avanzar(TimeSpan.FromSeconds(90));
        await gestor.CloseAsync(abierta.Value);

        Assert.Equal(inicio, banco.Historial.Anotados[0].AttemptedAt);
        Assert.Equal(90, banco.Historial.Anotados[0].DurationSeconds);
    }

    [Fact]
    public async Task Un_fallo_de_conexion_se_anota_al_momento_de_fallar()
    {
        var banco = new Banco();
        banco.Anfitrion.Preparar(new SuperficieFalsa
        {
            FalloAlConectar = new TimeoutException("no respondió"),
        });

        var gestor = banco.Crear();

        await gestor.OpenAsync(banco.Conexion.Id);

        var anotado = Assert.Single(banco.Historial.Anotados);
        Assert.Equal(ConnectionOutcome.Failed, anotado.Outcome);
        Assert.Null(anotado.DurationSeconds);
    }

    [Fact]
    public async Task Una_sesion_que_fallo_no_se_anota_dos_veces_al_cerrarla()
    {
        var banco = new Banco();
        banco.Anfitrion.Preparar(new SuperficieFalsa
        {
            FalloAlConectar = new TimeoutException("no respondió"),
        });

        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        await gestor.CloseAsync(abierta.Value);

        Assert.Single(banco.Historial.Anotados);
    }

    [Fact]
    public async Task Cerrar_mientras_conectaba_se_anota_como_cancelada()
    {
        var banco = new Banco();

        banco.Anfitrion.Preparar(new SuperficieFalsa
        {
            EstadoAlConectar = SessionState.Connecting,
        });

        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        await gestor.CloseAsync(abierta.Value);

        var anotado = Assert.Single(banco.Historial.Anotados);
        Assert.Equal(ConnectionOutcome.Cancelled, anotado.Outcome);
        Assert.Null(anotado.DurationSeconds);
    }

    [Fact]
    public async Task Cerrar_todo_anota_cada_sesion()
    {
        var banco = new Banco();
        var gestor = banco.Crear();

        await gestor.OpenAsync(banco.Conexion.Id);
        await gestor.OpenAsync(banco.Conexion.Id, forceNew: true);
        await gestor.OpenAsync(banco.Conexion.Id, forceNew: true);

        banco.Reloj.Avanzar(TimeSpan.FromMinutes(5));
        gestor.CloseAll();

        Assert.Equal(3, banco.Historial.Anotados.Count);
        Assert.All(banco.Historial.Anotados, a => Assert.Equal(300, a.DurationSeconds));
    }

    [Fact]
    public async Task Si_el_historial_falla_la_sesion_se_cierra_igual()
    {
        var banco = new Banco();
        banco.Historial.FalloAlEscribir = new InvalidOperationException("base bloqueada");
        var gestor = banco.Crear();

        var abierta = await gestor.OpenAsync(banco.Conexion.Id);
        await gestor.CloseAsync(abierta.Value);

        Assert.Empty(gestor.ActiveSessions);
        Assert.Empty(banco.Historial.Anotados);
    }

    [Fact]
    public async Task Reusar_una_sesion_abierta_no_anota_nada()
    {
        var banco = new Banco();
        var gestor = banco.Crear();

        await gestor.OpenAsync(banco.Conexion.Id);
        await gestor.OpenAsync(banco.Conexion.Id);

        Assert.Empty(banco.Historial.Anotados);
    }
}
