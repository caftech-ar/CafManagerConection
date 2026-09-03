using System.Text;
using System.Text.RegularExpressions;
using CafManagerConection.Ssh;
using Xunit;

namespace CafManagerConection.Ssh.Tests;

// El servidor necesita stty y tr en el PATH, y un pty donde "stty size" informe el tamaño.
[Trait("Categoria", "IntegracionSsh")]
public sealed class SshResizeTests
{
    private static async Task<(int Filas, int Columnas)?> PreguntarTamano(
        SshSession sesion, StringBuilder salida, string marca)
    {
        lock (salida)
        {
            salida.Clear();
        }

        sesion.Send(Encoding.UTF8.GetBytes($"echo {marca}-$(stty size | tr ' ' 'x')-{marca}\n"));

        var patron = new Regex($"{marca}-(\\d+)x(\\d+)-{marca}", RegexOptions.None);
        var limite = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < limite)
        {
            string texto;

            lock (salida)
            {
                texto = salida.ToString();
            }

            var m = patron.Matches(texto).LastOrDefault();

            if (m is not null && m.Success)
            {
                return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return null;
    }

    private static (SshSession Sesion, StringBuilder Salida) Armar(int columnas, int filas)
    {
        var sesion = new SshSession(
            ServidorDePrueba.Pedido(columnas, filas), new ServidorDePrueba.AceptaTodo());

        var salida = new StringBuilder();

        sesion.DataReceived += (_, datos) =>
        {
            lock (salida)
            {
                salida.Append(Encoding.UTF8.GetString(datos.Span));
            }
        };

        return (sesion, salida);
    }

    [PruebaDeIntegracionSsh]
    public async Task El_tamano_inicial_llega_al_servidor()
    {
        var (sesion, salida) = Armar(columnas: 100, filas: 30);
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        var tamano = await PreguntarTamano(sesion, salida, "INI");

        Assert.True(tamano.HasValue, $"El servidor no contestó el tamaño. Recibido:\n{salida}");
        Assert.Equal((30, 100), tamano!.Value);
    }

    [PruebaDeIntegracionSsh]
    public async Task Redimensionar_le_llega_al_servidor()
    {
        var (sesion, salida) = Armar(columnas: 80, filas: 24);
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        var antes = await PreguntarTamano(sesion, salida, "ANTES");
        Assert.Equal((24, 80), antes!.Value);

        sesion.Resize(columns: 132, rows: 43);

        var despues = await PreguntarTamano(sesion, salida, "DESPUES");

        Assert.True(despues.HasValue, $"El servidor no contestó tras redimensionar.\n{salida}");
        Assert.Equal((43, 132), despues!.Value);
    }

    [PruebaDeIntegracionSsh]
    public async Task Redimensionar_una_sesion_que_no_esta_conectada_no_rompe()
    {
        var (sesion, _) = Armar(columnas: 80, filas: 24);
        await using var _s = sesion;

        sesion.Resize(120, 40);

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);
        await sesion.DisconnectAsync();

        sesion.Resize(90, 30);
    }
}
