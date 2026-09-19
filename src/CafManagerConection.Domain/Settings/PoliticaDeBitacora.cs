namespace CafManagerConection.Domain.Settings;

/// <summary>Qué sesiones SSH dejan bitácora, dónde y por cuánto tiempo.</summary>
public sealed record AjustesDeBitacora(
    bool Activa = true,
    string Carpeta = "",
    int DiasQueSeGuardan = 30)
{
    public const int MinimoDeDias = 0;

    public const int MaximoDeDias = 3650;

    public static AjustesDeBitacora Default { get; } = new();

    public AjustesDeBitacora Normalizados() => this with
    {
        DiasQueSeGuardan = Math.Clamp(DiasQueSeGuardan, MinimoDeDias, MaximoDeDias),
        Carpeta = (Carpeta ?? string.Empty).Trim(),
    };
}

public static class PoliticaDeBitacora
{
    public const string FormatoDeSello = "yyyyMMdd-HHmmss";

    public const string Extension = ".txt";

    /// <summary>Separa el host del nombre de la conexión. <see cref="Sanear"/> lo quita de las dos partes, así que el del nombre es siempre el separador.</summary>
    public const char SeparadorDeHost = '@';

    /// <summary>Nombre del archivo de una sesión: el host adelante para que las del mismo equipo queden juntas.</summary>
    /// <param name="conexion">Nombre de la conexión.</param>
    /// <param name="inicio">Cuándo empezó la sesión.</param>
    /// <param name="host">Host de la conexión; vacío lo deja fuera del nombre.</param>
    public static string NombreDeArchivo(
        string conexion, DateTimeOffset inicio, string? host = null)
    {
        var sello = inicio.ToLocalTime().ToString(FormatoDeSello, null);
        var nombre = $"{Sanear(conexion)}-{sello}{Extension}";

        return string.IsNullOrWhiteSpace(host)
            ? nombre
            : $"{Sanear(host)}{SeparadorDeHost}{nombre}";
    }

    /// <summary>Lo que dice el nombre de un archivo de bitácora.</summary>
    public sealed record DatosDeBitacora(string Host, string Conexion, DateTimeOffset Inicio);

    /// <summary>Lee host, conexión y momento del nombre. <c>null</c> si el archivo no es una bitácora.</summary>
    /// <param name="nombre">Nombre del archivo, sin la carpeta.</param>
    public static DatosDeBitacora? Leer(string nombre)
    {
        if (!EsBitacora(nombre))
        {
            return null;
        }

        var sinExtension = nombre[..^Extension.Length];
        var sello = sinExtension[^FormatoDeSello.Length..];

        if (!DateTime.TryParseExact(
                sello,
                FormatoDeSello,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var inicio))
        {
            return null;
        }

        var origen = sinExtension[..^(FormatoDeSello.Length + 1)];
        var arroba = origen.IndexOf(SeparadorDeHost, StringComparison.Ordinal);

        return arroba < 0
            ? new DatosDeBitacora(string.Empty, origen, new DateTimeOffset(inicio))
            : new DatosDeBitacora(
                origen[..arroba], origen[(arroba + 1)..], new DateTimeOffset(inicio));
    }

    /// <summary>Si el archivo tiene la forma que genera <see cref="NombreDeArchivo"/>.</summary>
    /// <param name="nombre">Nombre del archivo, sin la carpeta.</param>
    public static bool EsBitacora(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)
            || !nombre.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // La purga borra sin preguntar y la carpeta la elige el usuario: sólo se tocan los archivos
        // cuyo nombre termina en «-» más el sello de tiempo que escribe esta aplicación.
        var sinExtension = nombre[..^Extension.Length];

        if (sinExtension.Length < FormatoDeSello.Length + 1
            || sinExtension[^(FormatoDeSello.Length + 1)] != '-')
        {
            return false;
        }

        return DateTime.TryParseExact(
            sinExtension[^FormatoDeSello.Length..],
            FormatoDeSello,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out _);
    }

    /// <summary>Si una bitácora escrita en ese momento ya pasó el plazo de retención.</summary>
    /// <param name="escrita">Cuándo se escribió por última vez.</param>
    /// <param name="ahora">Momento de referencia.</param>
    /// <param name="dias">Días que se guardan; cero no purga nada.</param>
    public static bool HayQueBorrar(DateTimeOffset escrita, DateTimeOffset ahora, int dias) =>
        dias > 0 && ahora - escrita > TimeSpan.FromDays(dias);

    /// <summary>Deja el nombre usable como nombre de archivo.</summary>
    /// <param name="nombre">Nombre de la conexión, tal como lo escribió el usuario.</param>
    public static string Sanear(string nombre)
    {
        var invalidos = Path.GetInvalidFileNameChars();

        var limpio = new string([.. (nombre ?? string.Empty)
            .Select(c => invalidos.Contains(c) || c == SeparadorDeHost ? '-' : c)]).Trim();

        return limpio.Length == 0 ? "sesion" : limpio;
    }
}
