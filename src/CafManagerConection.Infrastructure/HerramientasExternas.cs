using System.Runtime.Versioning;
using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Infrastructure;

public enum HerramientaExterna
{
    Putty,

    FileZilla,

    WinScp,

    Mstsc,
}

public static class ProtocolosDeHerramienta
{
    /// <summary>Si la herramienta sirve para abrir una conexión de ese protocolo.</summary>
    /// <param name="herramienta">La herramienta externa.</param>
    /// <param name="protocolo">El protocolo de la conexión.</param>
    public static bool Atiende(HerramientaExterna herramienta, Protocol protocolo) =>
        herramienta switch
        {
            HerramientaExterna.Putty or HerramientaExterna.FileZilla or HerramientaExterna.WinScp =>
                protocolo == Protocol.Ssh,

            HerramientaExterna.Mstsc => protocolo == Protocol.Rdp,

            _ => false,
        };
}

// No hay campo para la contraseña a propósito: lo que va en la línea de comandos queda visible en la lista de procesos de la máquina.
public sealed record DestinoRemoto(
    string Host,
    int Puerto,
    string? Usuario = null,
    string? RutaDeClave = null);

public static class LineaDeComando
{
    public static string Para(HerramientaExterna herramienta, DestinoRemoto destino)
    {
        ArgumentNullException.ThrowIfNull(destino);

        return herramienta switch
        {
            HerramientaExterna.Putty => Putty(destino),
            HerramientaExterna.FileZilla => FileZilla(destino),
            HerramientaExterna.WinScp => WinScp(destino),
            HerramientaExterna.Mstsc => Mstsc(destino),
            _ => throw new ArgumentOutOfRangeException(nameof(herramienta)),
        };
    }

    // <c>-ssh</c> explícito: sin el protocolo, PuTTY abre con lo que tenga por omisión, que puede ser Telnet o una sesión guardada.
    private static string Putty(DestinoRemoto d)
    {
        var partes = new List<string> { "-ssh" };

        partes.Add(string.IsNullOrWhiteSpace(d.Usuario) ? d.Host : $"{d.Usuario}@{d.Host}");
        partes.Add("-P");
        partes.Add(d.Puerto.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(d.RutaDeClave))
        {
            partes.Add("-i");
            partes.Add(Citar(d.RutaDeClave));
        }

        return string.Join(' ', partes);
    }

    // Sin usuario: mstsc no lo toma en el destino, lo pide su propio cuadro de credenciales.
    private static string Mstsc(DestinoRemoto d) => $"/v:{d.Host}:{d.Puerto}";

    // Sin clave: FileZilla no la toma por línea de comandos y escribirla en su configuración está prohibido.
    private static string FileZilla(DestinoRemoto d) => Url(d);

    private static string WinScp(DestinoRemoto d)
    {
        var direccion = Url(d, barraFinal: true);

        return string.IsNullOrWhiteSpace(d.RutaDeClave)
            ? direccion
            : $"{direccion} /privatekey={Citar(d.RutaDeClave)}";
    }

    private static string Url(DestinoRemoto d, bool barraFinal = false)
    {
        var credencial = string.IsNullOrWhiteSpace(d.Usuario)
            ? string.Empty
            : $"{Uri.EscapeDataString(d.Usuario)}@";

        var final = barraFinal ? "/" : string.Empty;

        return Citar($"sftp://{credencial}{d.Host}:{d.Puerto}{final}");
    }

    private static string Citar(string valor) => $"\"{valor}\"";
}

[SupportedOSPlatform("windows")]
public sealed class BuscadorDeHerramientas(Func<string, bool> existe)
{
    private readonly Func<string, bool> _existe = existe;

    public static BuscadorDeHerramientas DelSistema { get; } = new(File.Exists);

    public string? Buscar(HerramientaExterna herramienta) =>
        DesdeElRegistro(herramienta) ?? Candidatas(herramienta).FirstOrDefault(_existe);

    // Las dos carpetas de programas: PuTTY y WinSCP se distribuyen en 32 y en 64 bits y las dos instalaciones pueden convivir.
    internal static IEnumerable<string> Candidatas(HerramientaExterna herramienta)
    {
        if (herramienta == HerramientaExterna.Mstsc)
        {
            // Viene con Windows: no está en Archivos de programa ni en App Paths.
            return
            [
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe"),
            ];
        }

        var programas = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        var relativas = herramienta switch
        {
            HerramientaExterna.Putty => new[] { @"PuTTY\putty.exe" },
            HerramientaExterna.FileZilla => new[]
            {
                @"FileZilla FTP Client\filezilla.exe",
                @"FileZilla\filezilla.exe",
            },
            HerramientaExterna.WinScp => new[] { @"WinSCP\WinSCP.exe" },
            _ => [],
        };

        return from raiz in programas
               where !string.IsNullOrEmpty(raiz)
               from relativa in relativas
               select Path.Combine(raiz, relativa);
    }

    /// <summary>Lo que dice el registro en <c>App Paths</c>, donde Windows guarda dónde quedó cada programa instalado.</summary>
    private string? DesdeElRegistro(HerramientaExterna herramienta)
    {
        var exe = herramienta switch
        {
            HerramientaExterna.Putty => "putty.exe",
            HerramientaExterna.FileZilla => "filezilla.exe",
            HerramientaExterna.WinScp => "WinSCP.exe",
            _ => null,
        };

        if (exe is null)
        {
            return null;
        }

        try
        {
            using var clave = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}");

            return clave?.GetValue(null) is string ruta
                   && ruta.Length > 0
                   && _existe(ruta.Trim('"'))
                ? ruta.Trim('"')
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
