using System.Net.Sockets;

namespace CafManagerConection.App.Services;

/// <summary>Prueba si un puerto de este equipo acepta una conexión (FR-168f).</summary>
public static class SondaDePuerto
{
    public static async Task<bool> RespondeAsync(
        int puerto, TimeSpan? limite = null, CancellationToken ct = default)
    {
        if (puerto is < 1 or > 65535)
        {
            return false;
        }

        using var cliente = new TcpClient();
        using var corte = CancellationTokenSource.CreateLinkedTokenSource(ct);

        corte.CancelAfter(limite ?? TimeSpan.FromSeconds(1));

        try
        {
            await cliente.ConnectAsync("127.0.0.1", puerto, corte.Token).ConfigureAwait(false);

            return cliente.Connected;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
