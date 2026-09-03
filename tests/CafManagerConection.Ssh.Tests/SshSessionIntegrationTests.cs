using System.Text;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.Ssh;

namespace CafManagerConection.Ssh.Tests;

// El servidor necesita autenticación por contraseña habilitada en sshd, un shell POSIX que
// resuelva $((21*2)), y que el usuario "nadie" no exista.
[Trait("Categoria", "IntegracionSsh")]
public sealed class SshSessionIntegrationTests
{
    private static async Task<bool> EsperarTexto(
        StringBuilder acumulado, Func<string, bool> coincide, int segundos = 15)
    {
        var limite = DateTime.UtcNow.AddSeconds(segundos);

        while (DateTime.UtcNow < limite)
        {
            lock (acumulado)
            {
                if (coincide(acumulado.ToString()))
                {
                    return true;
                }
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return false;
    }

    private static (SshSession Sesion, StringBuilder Salida) Armar(
        IHostKeyVerifier verificador, SshSessionRequest? pedido = null)
    {
        var sesion = new SshSession(pedido ?? ServidorDePrueba.Pedido(), verificador);
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
    public async Task Se_conecta_con_usuario_y_contrasena()
    {
        var (sesion, _) = Armar(new ServidorDePrueba.AceptaTodo());
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        Assert.Equal(SessionState.Connected, sesion.State);
        Assert.Null(sesion.Failure);
    }

    [PruebaDeIntegracionSsh]
    public async Task El_shell_interactivo_ejecuta_y_devuelve_lo_que_escribe_el_servidor()
    {
        var (sesion, salida) = Armar(new ServidorDePrueba.AceptaTodo());
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        sesion.Send(Encoding.UTF8.GetBytes("echo CMC-$((21*2))-FIN\n"));

        var llego = await EsperarTexto(salida, t => t.Contains("CMC-42-FIN", StringComparison.Ordinal));

        Assert.True(llego, $"El servidor nunca devolvió la marca. Recibido:\n{salida}");
    }

    [PruebaDeIntegracionSsh]
    public async Task Los_contadores_cuentan_trafico_real()
    {
        var (sesion, salida) = Armar(new ServidorDePrueba.AceptaTodo());
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        var orden = "echo CMC-CONTADORES\n";
        sesion.Send(Encoding.UTF8.GetBytes(orden));
        await EsperarTexto(salida, t => t.Contains("CMC-CONTADORES", StringComparison.Ordinal));

        Assert.True(
            sesion.BytesSent >= orden.Length,
            $"Enviados {sesion.BytesSent} para una orden de {orden.Length} bytes.");

        Assert.True(sesion.BytesReceived > 0, "No contó nada recibido.");
    }

    [PruebaDeIntegracionSsh]
    public async Task Rechazar_la_clave_del_host_aborta_sin_conectar()
    {
        var (sesion, _) = Armar(new ServidorDePrueba.RechazaTodo());
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        Assert.NotEqual(SessionState.Connected, sesion.State);
        Assert.NotNull(sesion.Failure);
    }

    [PruebaDeIntegracionSsh]
    public async Task La_clave_del_host_se_verifica_y_llega_en_formato_de_OpenSSH()
    {
        var verificador = new ServidorDePrueba.AceptaTodo();
        var (sesion, _) = Armar(verificador);
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        Assert.Equal(1, verificador.Veces);
        Assert.NotNull(verificador.Visto);

        Assert.StartsWith("SHA256:", verificador.Visto, StringComparison.Ordinal);
    }

    [PruebaDeIntegracionSsh]
    public async Task La_huella_ya_conocida_se_le_pasa_al_verificador()
    {
        var verificador = new ServidorDePrueba.AceptaTodo();
        var (sesion, _) = Armar(
            verificador, ServidorDePrueba.Pedido(fingerprintConocido: "SHA256:loQueSea"));

        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        Assert.Equal("SHA256:loQueSea", verificador.Conocido);
    }

    [PruebaDeIntegracionSsh]
    public async Task Una_contrasena_incorrecta_deja_la_sesion_en_Error_y_no_lanza()
    {
        var (sesion, _) = Armar(new ServidorDePrueba.AceptaTodo());
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial("la que no es");
        await sesion.ConnectAsync(credencial);

        Assert.Equal(SessionState.Error, sesion.State);
        Assert.NotNull(sesion.Failure);
        Assert.Equal(SessionFailureReason.AuthenticationRejected, sesion.Failure.Reason);
    }

    [PruebaDeIntegracionSsh]
    public async Task Un_usuario_que_no_existe_tambien_es_rechazo_de_autenticacion()
    {
        var (sesion, _) = Armar(
            new ServidorDePrueba.AceptaTodo(), ServidorDePrueba.Pedido(usuario: "nadie"));

        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);

        Assert.Equal(SessionState.Error, sesion.State);
    }

    [PruebaDeIntegracionSsh]
    public async Task Desconectar_deja_la_sesion_desconectada()
    {
        var (sesion, _) = Armar(new ServidorDePrueba.AceptaTodo());
        await using var _s = sesion;

        using var credencial = ServidorDePrueba.Credencial();
        await sesion.ConnectAsync(credencial);
        await sesion.DisconnectAsync();

        Assert.NotEqual(SessionState.Connected, sesion.State);
    }
}
