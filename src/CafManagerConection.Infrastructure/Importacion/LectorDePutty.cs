using System.Runtime.Versioning;
using System.Text;
using CafManagerConection.Domain.Importacion;
using Microsoft.Win32;

namespace CafManagerConection.Infrastructure.Importacion;

public static class LectorDePutty
{
    private const string RutaDeSesiones = @"Software\SimonTatham\PuTTY\Sessions";

    private const string SesionPlantilla = "Default Settings";

    // WinSCP la escribe en el registro de PuTTY al abrir una sesión; su clave PuttySession la nombra.
    private const string SesionTemporalDeWinScp = "WinSCP temporary session";

    public const string ProtocoloSsh = "SSH";

    // SSH.NET no lee .ppk en toda versión: la clave de PuTTY puede necesitar conversión a OpenSSH.
    public const string AvisoDeClavePutty =
        "La clave es un .ppk de PuTTY y puede necesitar conversión a formato OpenSSH.";

    private static readonly UTF8Encoding Utf8Estricto = new(false, throwOnInvalidBytes: true);

    public readonly record struct ResultadoDeSesion(
        ConexionImportada? Conexion,
        ImportacionOmitida? Omitida)
    {
        public static ResultadoDeSesion Ignorada => default;

        public bool EsIgnorada => Conexion is null && Omitida is null;
    }

    /// <summary>Lee <c>HKCU\Software\SimonTatham\PuTTY\Sessions</c>.</summary>
    [SupportedOSPlatform("windows")]
    public static LecturaDeImportacion LeerRegistro()
    {
        using var sesiones = Registry.CurrentUser.OpenSubKey(RutaDeSesiones);

        if (sesiones is null)
        {
            return LecturaDeImportacion.Vacia;
        }

        var compatibles = new List<ConexionImportada>();
        var omitidas = new List<ImportacionOmitida>();

        foreach (var nombreCrudo in sesiones.GetSubKeyNames())
        {
            using var sesion = sesiones.OpenSubKey(nombreCrudo);

            if (sesion is null)
            {
                continue;
            }

            var resultado = DesdeValores(
                nombreCrudo,
                LeerTexto(sesion, "HostName"),
                LeerEntero(sesion, "PortNumber"),
                LeerTexto(sesion, "Protocol"),
                LeerTexto(sesion, "UserName"),
                LeerTexto(sesion, "PublicKeyFile"));

            if (resultado.Conexion is { } conexion)
            {
                compatibles.Add(conexion);
            }
            else if (resultado.Omitida is { } omitida)
            {
                omitidas.Add(omitida);
            }
        }

        return new LecturaDeImportacion(compatibles, omitidas);
    }

    public static ResultadoDeSesion DesdeValores(
        string nombreCrudo,
        string? hostName,
        int? portNumber,
        string? protocol,
        string? userName,
        string? publicKeyFile)
    {
        ArgumentNullException.ThrowIfNull(nombreCrudo);

        var nombre = DecodificarNombre(nombreCrudo);

        if (NoEsUnaSesionDelUsuario(nombre))
        {
            return ResultadoDeSesion.Ignorada;
        }

        if (!EsSsh(protocol, out var motivoDelProtocolo))
        {
            return Omitir(nombre, motivoDelProtocolo);
        }

        var (host, usuarioPegadoAlHost) = SepararUsuarioDelHost(hostName);

        if (host is null)
        {
            return Omitir(nombre, "sin servidor guardado");
        }

        var usuario = Normalizar(userName) ?? usuarioPegadoAlHost;
        var clave = Normalizar(publicKeyFile);

        return new ResultadoDeSesion(
            new ConexionImportada(
                OrigenDeImportacion.Putty,
                nombre,
                Carpetas: [],
                host,
                PuertoValido(portNumber),
                usuario,
                clave,
                ProtocoloSsh,
                Credencial: null,
                Advertencias: clave is null ? null : [AvisoDeClavePutty]),
            null);
    }

    public static string DecodificarNombre(string nombreCrudo)
    {
        ArgumentNullException.ThrowIfNull(nombreCrudo);

        if (!nombreCrudo.Contains('%', StringComparison.Ordinal))
        {
            return nombreCrudo;
        }

        var texto = new StringBuilder(nombreCrudo.Length);
        var escapados = new List<byte>();
        var i = 0;

        while (i < nombreCrudo.Length)
        {
            if (nombreCrudo[i] == '%'
                && i + 2 < nombreCrudo.Length
                && Digito(nombreCrudo[i + 1]) is { } alto
                && Digito(nombreCrudo[i + 2]) is { } bajo)
            {
                escapados.Add((byte)((alto << 4) | bajo));
                i += 3;
                continue;
            }

            VolcarEscapados(escapados, texto);
            texto.Append(nombreCrudo[i]);
            i++;
        }

        VolcarEscapados(escapados, texto);

        return texto.ToString();
    }

    private static bool NoEsUnaSesionDelUsuario(string nombre) =>
        string.Equals(nombre, SesionPlantilla, StringComparison.Ordinal)
        || string.Equals(nombre, SesionTemporalDeWinScp, StringComparison.Ordinal);

    private static bool EsSsh(string? protocol, out string motivo)
    {
        // Protocol vale ssh, telnet, rlogin, serial, raw o ssh-connection en las versiones nuevas.
        var declarado = Normalizar(protocol);

        if (declarado is null)
        {
            motivo = "sin protocolo guardado: no se supone SSH";
            return false;
        }

        if (string.Equals(declarado, "ssh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(declarado, "ssh-connection", StringComparison.OrdinalIgnoreCase))
        {
            motivo = string.Empty;
            return true;
        }

        motivo = $"protocolo {declarado}: CMC sólo abre SSH";
        return false;
    }

    private static (string? Host, string? Usuario) SepararUsuarioDelHost(string? hostName)
    {
        var crudo = Normalizar(hostName);

        if (crudo is null)
        {
            return (null, null);
        }

        var arroba = crudo.LastIndexOf('@');

        return arroba < 0
            ? (crudo, null)
            : (Normalizar(crudo[(arroba + 1)..]), Normalizar(crudo[..arroba]));
    }

    private static int? PuertoValido(int? portNumber) =>
        portNumber is > 0 and <= 65535 ? portNumber : null;

    private static ResultadoDeSesion Omitir(string nombre, string motivo) =>
        new(null, new ImportacionOmitida(OrigenDeImportacion.Putty, nombre, motivo));

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    [SupportedOSPlatform("windows")]
    private static string? LeerTexto(RegistryKey clave, string nombre) =>
        Normalizar(clave.GetValue(nombre) as string);

    [SupportedOSPlatform("windows")]
    private static int? LeerEntero(RegistryKey clave, string nombre) =>
        clave.GetValue(nombre) is int valor ? valor : null;

    private static void VolcarEscapados(List<byte> escapados, StringBuilder destino)
    {
        if (escapados.Count == 0)
        {
            return;
        }

        var crudos = escapados.ToArray();

        try
        {
            destino.Append(Utf8Estricto.GetString(crudos));
        }
        catch (DecoderFallbackException)
        {
            destino.Append(Encoding.Latin1.GetString(crudos));
        }

        escapados.Clear();
    }

    private static int? Digito(char caracter) => caracter switch
    {
        >= '0' and <= '9' => caracter - '0',
        >= 'a' and <= 'f' => caracter - 'a' + 10,
        >= 'A' and <= 'F' => caracter - 'A' + 10,
        _ => null,
    };
}
