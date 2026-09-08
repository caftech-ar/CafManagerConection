using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Ssh.Tests;

/// <summary>Guarda todo lo que se anota, para poder afirmar que un secreto no está ahí.</summary>
internal sealed class TrazasQueGuardan : IRegistroDeTrazas
{
    public List<string> Textos { get; } = [];

    public bool Activo => true;

    public void Anotar(EntradaDeTraza entrada) =>
        Textos.Add($"{entrada.Enviado}\n{entrada.Salida}\n{entrada.Error}");

    public string Todo() => string.Join("\n", Textos);
}

internal sealed class RegistroQueGuarda : IAppLogger
{
    public List<string> Textos { get; } = [];

    public string Todo() => string.Join("\n", Textos);

    public void ApplicationStarted(string version) => Textos.Add(version);

    public void ApplicationStopping(int activeSessions) => Textos.Add($"{activeSessions}");

    public void ConnectionOpening(Guid connectionId, string protocol, string host, int port) =>
        Textos.Add($"{protocol} {host} {port}");

    public void ConnectionSucceeded(Guid connectionId, TimeSpan elapsed) =>
        Textos.Add($"{elapsed}");

    public void ConnectionFailed(
        Guid connectionId, SessionFailureReason reason, string? technicalDetail) =>
        Textos.Add($"{reason} {technicalDetail}");

    public void ConnectionClosed(Guid connectionId, TimeSpan duration) => Textos.Add($"{duration}");

    public void TunnelStarted(Guid tunnelId, int localPort) => Textos.Add($"{localPort}");

    public void TunnelStopped(Guid tunnelId, int localPort) => Textos.Add($"{localPort}");

    public void DatabaseMigrated(int fromVersion, int toVersion) =>
        Textos.Add($"{fromVersion} {toVersion}");

    public void DatabaseCorruptionRecovered(string preservedPath) => Textos.Add(preservedPath);

    public void TechnicalError(string operation, Exception exception) =>
        Textos.Add($"{operation} {exception}");

    public void PlatformActionPerformed(Guid connectionId, string action) => Textos.Add(action);

    public void WorkCompleted(Guid connectionId, RemoteWork work, TimeSpan elapsed) =>
        Textos.Add($"{work} {elapsed}");
}
