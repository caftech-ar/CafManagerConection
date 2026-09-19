using System.Net.Sockets;

namespace CafManagerConection.App.Services;

/// <summary>Prueba si un puerto acepta una conexión.</summary>
public static class SondaDePuerto
{
    /// <summary>Si un puerto de este equipo acepta una conexión.</summary>
    /// <param name="puerto">Puerto a probar.</param>
    /// <param name="limite">Cuánto esperar antes de darlo por no disponible.</param>
    /// <param name="ct">Para cortar la prueba desde afuera.</param>
    public static Task<bool> RespondeAsync(
        int puerto, TimeSpan? limite = null, CancellationToken ct = default) =>
        RespondeAsync("127.0.0.1", puerto, limite, ct);

    /// <summary>Si un puerto de un equipo acepta una conexión.</summary>
    /// <param name="host">Nombre o dirección del equipo.</param>
    /// <param name="puerto">Puerto a probar.</param>
    /// <param name="limite">Cuánto esperar antes de darlo por no disponible.</param>
    /// <param name="ct">Para cortar la prueba desde afuera.</param>
    public static async Task<bool> RespondeAsync(
        string host, int puerto, TimeSpan? limite = null, CancellationToken ct = default)
    {
        if (puerto is < 1 or > 65535 || string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        using var cliente = new TcpClient();
        using var corte = CancellationTokenSource.CreateLinkedTokenSource(ct);

        corte.CancelAfter(limite ?? TimeSpan.FromSeconds(1));

        try
        {
            await cliente.ConnectAsync(host, puerto, corte.Token).ConfigureAwait(false);

            return cliente.Connected;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
