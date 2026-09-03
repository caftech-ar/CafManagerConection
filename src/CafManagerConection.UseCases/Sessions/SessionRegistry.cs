using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.UseCases.Sessions;

public sealed record SessionInfo(
    Guid SessionId,
    Guid ConnectionId,
    string DisplayName,
    SessionState State,
    DateTimeOffset StartedAt);

public sealed class SessionRegistry
{
    private readonly Dictionary<Guid, SessionInfo> _sesiones = [];
    private readonly Lock _candado = new();

    public event EventHandler? Changed;

    public IReadOnlyList<SessionInfo> ActiveSessions
    {
        get
        {
            lock (_candado)
            {
                return _sesiones.Values.OrderBy(s => s.StartedAt).ToList();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_candado)
            {
                return _sesiones.Count;
            }
        }
    }

    public int CountForConnection(Guid connectionId)
    {
        lock (_candado)
        {
            return _sesiones.Values.Count(s => s.ConnectionId == connectionId);
        }
    }

    /// <summary>Primera sesión abierta de una conexión, para traerla al frente en vez de duplicarla (FR-044a).</summary>
    public SessionInfo? FirstForConnection(Guid connectionId)
    {
        lock (_candado)
        {
            return _sesiones.Values
                .Where(s => s.ConnectionId == connectionId)
                .OrderBy(s => s.StartedAt)
                .FirstOrDefault();
        }
    }

    public SessionInfo Register(
        Guid sessionId, Guid connectionId, string displayName, DateTimeOffset startedAt)
    {
        var info = new SessionInfo(
            sessionId, connectionId, displayName, SessionState.Connecting, startedAt);

        lock (_candado)
        {
            _sesiones[sessionId] = info;
        }

        Avisar();
        return info;
    }

    public void UpdateState(Guid sessionId, SessionState state)
    {
        lock (_candado)
        {
            if (!_sesiones.TryGetValue(sessionId, out var actual))
            {
                return;
            }

            _sesiones[sessionId] = actual with { State = state };
        }

        Avisar();
    }

    public void Unregister(Guid sessionId)
    {
        bool quitada;

        lock (_candado)
        {
            quitada = _sesiones.Remove(sessionId);
        }

        if (quitada)
        {
            Avisar();
        }
    }

    public void Clear()
    {
        bool habia;

        lock (_candado)
        {
            habia = _sesiones.Count > 0;
            _sesiones.Clear();
        }

        if (habia)
        {
            Avisar();
        }
    }

    public string Resumen => Count switch
    {
        0 => "Sin sesiones abiertas",
        1 => "1 sesión abierta",
        var n => $"{n} sesiones abiertas",
    };

    private void Avisar() => Changed?.Invoke(this, EventArgs.Empty);
}
