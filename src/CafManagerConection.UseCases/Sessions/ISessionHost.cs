using CafManagerConection.Domain.Sessions;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Sessions;

public interface ISessionHost
{
    /// <summary>Crea la superficie y la muestra sin conectar: el ActiveX de RDP exige la ventana antes de conectar.</summary>
    ISessionSurface Create(Guid sessionId, ConnectionRecord connection);
}

public interface ISessionSurface : IDisposable
{
    SessionState State { get; }

    event EventHandler<SessionStateChanged>? StateChanged;

    Task ConnectAsync(CancellationToken ct = default);

    void Activate();
}
