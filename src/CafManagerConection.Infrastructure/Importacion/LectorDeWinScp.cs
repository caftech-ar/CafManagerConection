using System.Globalization;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Importacion;
using Microsoft.Win32;

namespace CafManagerConection.Infrastructure.Importacion;

public sealed record RutaDeSesion(IReadOnlyList<string> Carpetas, string Nombre)
{
    public string Completa => Carpetas.Count == 0
        ? Nombre
        : string.Join(" › ", Carpetas) + " › " + Nombre;
}

/// <summary>Lee las sesiones guardadas de WinSCP, del registro o de un <c>WinSCP.ini</c>.</summary>
public static class LectorDeWinScp
{
    private const string ClaveDeSesionesEnHkcu = @"Software\Martin Prikryl\WinSCP 2\Sessions";
    private const string PrefijoDeSeccionDeSesion = @"Sessions\";
    private const string PlantillaDeValoresPorOmision = "Default Settings";
    private const string NombreDelIni = "WinSCP.ini";
    private const int MagicoDeOfuscacion = 0xA3;
    private const int BanderaDeLargoExtendido = 0xFF;

    public const string AvisoContrasenaSinVerificar =
        "Su contraseña guardada no pasó la verificación al decodificarla: hay que " +
        "escribirla a mano la primera vez.";

    public static LecturaDeImportacion LeerIni(string contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var compatibles = new List<ConexionImportada>();
        var omitidas = new List<ImportacionOmitida>();
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? rutaCrudaDeLaSeccion = null;

        void CerrarSeccion()
        {
            if (rutaCrudaDeLaSeccion is not null)
            {
                Convertir(
                    OrigenDeImportacion.WinScpIni,
                    rutaCrudaDeLaSeccion,
                    valores,
                    compatibles,
                    omitidas);
            }
        }

        foreach (var linea in contenido.Split('\n'))
        {
            var texto = linea.Trim();

            if (texto.Length == 0 || texto[0] == ';')
            {
                continue;
            }

            if (texto.Length > 1 && texto[0] == '[' && texto[^1] == ']')
            {
                CerrarSeccion();
                valores.Clear();

                var seccion = texto[1..^1].Trim();

                rutaCrudaDeLaSeccion = seccion.StartsWith(
                    PrefijoDeSeccionDeSesion, StringComparison.OrdinalIgnoreCase)
                        ? seccion[PrefijoDeSeccionDeSesion.Length..]
                        : null;

                continue;
            }

            var igual = texto.IndexOf('=');

            if (rutaCrudaDeLaSeccion is null || igual <= 0)
            {
                continue;
            }

            valores[texto[..igual].Trim()] = texto[(igual + 1)..].Trim();
        }

        CerrarSeccion();

        return new LecturaDeImportacion(compatibles, omitidas);
    }

    [SupportedOSPlatform("windows")]
    public static LecturaDeImportacion LeerRegistro()
    {
        using var sesiones = AbrirSesionesDelRegistro();

        if (sesiones is null)
        {
            return LecturaDeImportacion.Vacia;
        }

        var compatibles = new List<ConexionImportada>();
        var omitidas = new List<ImportacionOmitida>();

        foreach (var rutaCruda in sesiones.GetSubKeyNames())
        {
            var valores = LeerValoresDeLaSesion(sesiones, rutaCruda);

            if (valores is null)
            {
                omitidas.Add(new ImportacionOmitida(
                    OrigenDeImportacion.WinScpRegistro,
                    SepararRuta(rutaCruda).Completa,
                    "No se pudo leer su clave del registro."));

                continue;
            }

            Convertir(
                OrigenDeImportacion.WinScpRegistro, rutaCruda, valores, compatibles, omitidas);
        }

        return new LecturaDeImportacion(compatibles, omitidas);
    }

    public static IEnumerable<string> RutasHabitualesDelIni()
    {
        var juntoAlEjecutable = new[]
        {
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
        }.Select(carpeta => RutaDentroDe(carpeta, "WinSCP", NombreDelIni));

        var enElPerfil = new[]
        {
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.LocalApplicationData,
        }.Select(carpeta => RutaDentroDe(carpeta, NombreDelIni));

        return juntoAlEjecutable
            .Concat(enElPerfil)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Deshace el escapado <c>%XX</c> con el que WinSCP guarda los nombres.</summary>
    public static string DecodificarNombre(string nombreCrudo)
    {
        ArgumentNullException.ThrowIfNull(nombreCrudo);

        return Uri.UnescapeDataString(nombreCrudo);
    }

    /// <summary>Separa las carpetas por «/» antes de decodificar, así un «%2F» no las parte.</summary>
    public static RutaDeSesion SepararRuta(string rutaCruda)
    {
        ArgumentNullException.ThrowIfNull(rutaCruda);

        var segmentos = rutaCruda
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DecodificarNombre)
            .ToArray();

        return segmentos.Length == 0
            ? new RutaDeSesion([], string.Empty)
            : new RutaDeSesion(segmentos[..^1], segmentos[^1]);
    }

    /// <summary>Devuelve <c>null</c> si el texto recuperado no empieza por usuario+host.</summary>
    public static string? DecodificarContrasena(string hex, string usuario, string host)
    {
        var claveEsperada = usuario + host;

        if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0 || claveEsperada.Length == 0)
        {
            return null;
        }

        var bytes = new byte[hex.Length / 2];

        try
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                var alto = DigitoHex(hex[i * 2]);
                var bajo = DigitoHex(hex[(i * 2) + 1]);

                if (alto < 0 || bajo < 0)
                {
                    return null;
                }

                bytes[i] = (byte)(~(((alto << 4) + bajo) ^ MagicoDeOfuscacion) & 0xFF);
            }

            var posicion = 0;

            if (!Tomar(bytes, ref posicion, out var bandera))
            {
                return null;
            }

            var largo = (int)bandera;

            if (bandera == BanderaDeLargoExtendido)
            {
                if (!Tomar(bytes, ref posicion, out _)
                    || !Tomar(bytes, ref posicion, out var largoExtendido))
                {
                    return null;
                }

                largo = largoExtendido;
            }

            if (!Tomar(bytes, ref posicion, out var salto))
            {
                return null;
            }

            posicion += salto;

            if (posicion + largo > bytes.Length)
            {
                return null;
            }

            var recuperado = Encoding.UTF8.GetString(bytes, posicion, largo);

            return recuperado.StartsWith(claveEsperada, StringComparison.Ordinal)
                ? recuperado[claveEsperada.Length..]
                : null;
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static void Convertir(
        OrigenDeImportacion origen,
        string rutaCruda,
        IReadOnlyDictionary<string, string> valores,
        List<ConexionImportada> compatibles,
        List<ImportacionOmitida> omitidas)
    {
        var ruta = SepararRuta(rutaCruda);

        if (ruta.Nombre.Length == 0 || EsLaPlantillaDeWinScp(ruta))
        {
            return;
        }

        var (protocolo, vaSobreSsh) = TraducirFsProtocol(Valor(valores, "FSProtocol"));

        if (!vaSobreSsh)
        {
            omitidas.Add(new ImportacionOmitida(
                origen,
                ruta.Completa,
                $"WinSCP la guardó como {protocolo}, y CMC sólo abre sesiones sobre SSH."));

            return;
        }

        var host = Valor(valores, "HostName");

        if (host is null)
        {
            omitidas.Add(new ImportacionOmitida(
                origen, ruta.Completa, "No tiene servidor guardado: le falta el HostName."));

            return;
        }

        var usuario = Valor(valores, "UserName");
        var hexDeLaContrasena = Valor(valores, "Password");

        var contrasena = hexDeLaContrasena is null
            ? null
            : DecodificarContrasena(hexDeLaContrasena, usuario ?? string.Empty, host);

        var contrasenaDescartada = hexDeLaContrasena is not null && contrasena is null;

        compatibles.Add(new ConexionImportada(
            origen,
            ruta.Nombre,
            ruta.Carpetas,
            host,
            PuertoValido(Valor(valores, "PortNumber")),
            usuario,
            Valor(valores, "PublicKeyFile"),
            protocolo,
            contrasena is null ? null : new StoredCredential(usuario ?? string.Empty, null, contrasena),
            contrasenaDescartada ? [AvisoContrasenaSinVerificar] : null));
    }

    private static bool EsLaPlantillaDeWinScp(RutaDeSesion ruta) =>
        ruta.Carpetas.Count == 0
        && string.Equals(ruta.Nombre, PlantillaDeValoresPorOmision, StringComparison.Ordinal);

    private static (string Nombre, bool VaSobreSsh) TraducirFsProtocol(string? codigo)
    {
        if (codigo is null)
        {
            return ("SFTP", true);
        }

        if (!int.TryParse(codigo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor))
        {
            return ($"un protocolo desconocido (FSProtocol={codigo})", false);
        }

        // 0=SCP 1=SFTP+SCP 2=SFTP 3=FTP 4=WebDAV 5=S3
        return valor switch
        {
            0 => ("SCP", true),
            1 => ("SFTP (con respaldo SCP)", true),
            2 => ("SFTP", true),
            3 => ("FTP", false),
            4 => ("WebDAV", false),
            5 => ("S3", false),
            _ => ($"un protocolo desconocido (FSProtocol={valor})", false),
        };
    }

    private static int? PuertoValido(string? valor) =>
        int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var puerto)
        && puerto is > 0 and <= 65535
            ? puerto
            : null;

    private static string? Valor(IReadOnlyDictionary<string, string> valores, string clave) =>
        valores.TryGetValue(clave, out var valor) && !string.IsNullOrWhiteSpace(valor)
            ? valor
            : null;

    private static string? RutaDentroDe(Environment.SpecialFolder carpeta, params string[] partes)
    {
        var raiz = Environment.GetFolderPath(carpeta);

        return raiz.Length == 0 ? null : Path.Combine([raiz, .. partes]);
    }

    private static bool Tomar(byte[] bytes, ref int posicion, out byte valor)
    {
        if (posicion >= bytes.Length)
        {
            valor = 0;
            return false;
        }

        valor = bytes[posicion++];

        return true;
    }

    private static int DigitoHex(char caracter) => caracter switch
    {
        >= '0' and <= '9' => caracter - '0',
        >= 'A' and <= 'F' => caracter - 'A' + 10,
        >= 'a' and <= 'f' => caracter - 'a' + 10,
        _ => -1,
    };

    private static string? ComoTexto(object? valor) => valor switch
    {
        string texto => texto,
        int numero => numero.ToString(CultureInfo.InvariantCulture),
        long numero => numero.ToString(CultureInfo.InvariantCulture),
        _ => null,
    };

    [SupportedOSPlatform("windows")]
    private static Dictionary<string, string>? LeerValoresDeLaSesion(
        RegistryKey sesiones, string rutaCruda)
    {
        try
        {
            using var sesion = sesiones.OpenSubKey(rutaCruda);

            if (sesion is null)
            {
                return null;
            }

            var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var clave in sesion.GetValueNames())
            {
                if (ComoTexto(sesion.GetValue(clave)) is { } texto)
                {
                    valores[clave] = texto;
                }
            }

            return valores;
        }
        catch (Exception ex) when (
            ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static RegistryKey? AbrirSesionesDelRegistro()
    {
        try
        {
            return Registry.CurrentUser.OpenSubKey(ClaveDeSesionesEnHkcu);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
