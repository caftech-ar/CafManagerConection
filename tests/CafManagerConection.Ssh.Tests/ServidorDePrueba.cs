using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Ssh;

namespace CafManagerConection.Ssh.Tests;

internal static class ServidorDePrueba
{
    public const string VariableHost = "CMC_SSH_PRUEBA_HOST";
    public const string VariableUsuario = "CMC_SSH_PRUEBA_USUARIO";
    public const string VariableContrasena = "CMC_SSH_PRUEBA_CONTRASENA";
    public const string VariablePuerto = "CMC_SSH_PRUEBA_PUERTO";
    public const string VariableUsuarioSudo = "CMC_SSH_PRUEBA_USUARIO_SUDO";
    public const string VariableContrasenaSudo = "CMC_SSH_PRUEBA_CONTRASENA_SUDO";

    private const int PuertoPorOmision = 22;

    private static readonly string[] Obligatorias =
        [VariableHost, VariableUsuario, VariableContrasena];

    private static readonly string[] ObligatoriasDelSudo =
        [VariableHost, VariableUsuarioSudo, VariableContrasenaSudo];

    public static string Host => Valor(VariableHost);

    public static string Usuario => Valor(VariableUsuario);

    public static string UsuarioSudo => Valor(VariableUsuarioSudo);

    public static int Puerto =>
        int.TryParse(Environment.GetEnvironmentVariable(VariablePuerto), out var puerto)
            ? puerto
            : PuertoPorOmision;

    public static string? MotivoDeOmision()
    {
        var faltantes = Obligatorias.Where(SinDefinir).ToArray();

        return faltantes.Length == 0 ? null : ComoConfigurarlo(faltantes);
    }

    public static string? MotivoDeOmisionDelSudoConContrasena()
    {
        var faltantes = ObligatoriasDelSudo.Where(SinDefinir).ToArray();

        return faltantes.Length == 0 ? null : ComoConfigurarlo(faltantes);
    }

    private static bool SinDefinir(string variable) =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable));

    private static string ComoConfigurarlo(IEnumerable<string> faltantes) =>
        $"Prueba contra un servidor SSH real. Falta definir {string.Join(", ", faltantes)}. "
        + "Las variables se definen en la sesión que corre las pruebas y nunca se escriben en el "
        + "repositorio, en un .runsettings ni en ningún archivo: el Principio II lo prohíbe.\n"
        + $"  $env:{VariableHost} = 'mi-servidor'\n"
        + $"  $env:{VariableUsuario} = 'mi-usuario'\n"
        + $"  $env:{VariableContrasena} = Read-Host 'Contraseña' -MaskInput\n"
        + $"  $env:{VariablePuerto} = '2222'   # opcional, {PuertoPorOmision} por omisión\n"
        + $"  $env:{VariableUsuarioSudo} = 'otro-usuario'   # su sudo pide contraseña\n"
        + $"  $env:{VariableContrasenaSudo} = Read-Host 'Contraseña' -MaskInput\n"
        + "scripts/sshd-prueba.ps1 levanta el contenedor y las imprime todas al terminar.\n"
        + "El servidor tiene que ser Linux con shell POSIX; cada archivo de pruebas anota en su "
        + "primera línea qué más necesita.";

    private static string Valor(string variable) =>
        Environment.GetEnvironmentVariable(variable)
        ?? throw new InvalidOperationException($"Falta la variable de entorno {variable}.");

    public static SshSessionRequest Pedido(
        int columnas = 80,
        int filas = 24,
        string? usuario = null,
        string? fingerprintConocido = null) =>
        new(
            ConnectionId: Guid.NewGuid(),
            Host: Host,
            Port: Puerto,
            UserName: usuario ?? Usuario,
            AuthMethod: SshAuthMethod.Password,
            PrivateKeyPath: null,
            KnownHostFingerprint: fingerprintConocido,
            KeepAliveSeconds: 0,
            InitialColumns: columnas,
            InitialRows: filas,
            TimeoutSeconds: 15);

    public static StoredCredential Credencial(string? contrasena = null) =>
        new(Usuario, null, contrasena ?? Valor(VariableContrasena));

    public static SshSessionRequest PedidoDelSudo() => Pedido(usuario: UsuarioSudo);

    public static StoredCredential CredencialDelSudo() =>
        new(UsuarioSudo, null, Valor(VariableContrasenaSudo));

    public sealed class AceptaTodo : IHostKeyVerifier
    {
        public string? Visto { get; private set; }

        public string? Conocido { get; private set; }

        public int Veces { get; private set; }

        public HostKeyDecision Verify(
            Guid connectionId, string host, string fingerprint, string? known)
        {
            Veces++;
            Visto = fingerprint;
            Conocido = known;
            return HostKeyDecision.Accept;
        }
    }

    public sealed class RechazaTodo : IHostKeyVerifier
    {
        public HostKeyDecision Verify(
            Guid connectionId, string host, string fingerprint, string? known) =>
            HostKeyDecision.Reject;
    }
}
