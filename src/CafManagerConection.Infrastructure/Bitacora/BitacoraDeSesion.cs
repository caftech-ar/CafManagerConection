using System.Text;
using CafManagerConection.Domain.Settings;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Bitacora;

/// <summary>Escribe a disco, línea por línea, lo que pasa por una sesión SSH.</summary>
public sealed class BitacoraDeSesion : IDisposable
{
    private readonly object _candado = new();
    private readonly IAppLogger? _logger;
    private StreamWriter? _escritor;

    private BitacoraDeSesion(StreamWriter escritor, string ruta, IAppLogger? logger)
    {
        _escritor = escritor;
        _logger = logger;
        Ruta = ruta;
    }

    /// <summary>Dónde se está escribiendo.</summary>
    public string Ruta { get; }

    /// <summary>Si sigue escribiendo; un fallo la da de baja sin cortar la sesión.</summary>
    public bool Activa => _escritor is not null;

    /// <summary>Abre la bitácora de una sesión. Devuelve <c>null</c> si no se pudo.</summary>
    /// <param name="carpeta">Dónde se guardan las bitácoras.</param>
    /// <param name="conexion">Nombre de la conexión.</param>
    /// <param name="inicio">Cuándo empezó la sesión.</param>
    /// <param name="logger">Para dejar el motivo de un fallo.</param>
    /// <param name="host">Host de la conexión, que va adelante en el nombre del archivo.</param>
    public static BitacoraDeSesion? Abrir(
        string carpeta,
        string conexion,
        DateTimeOffset inicio,
        IAppLogger? logger = null,
        string? host = null)
    {
        try
        {
            Directory.CreateDirectory(carpeta);

            var ruta = Path.Combine(
                carpeta, PoliticaDeBitacora.NombreDeArchivo(conexion, inicio, host));

            var escritor = new StreamWriter(ruta, append: true, new UTF8Encoding(false))
            {
                AutoFlush = false,
            };

            return new BitacoraDeSesion(escritor, ruta, logger);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or NotSupportedException)
        {
            logger?.TechnicalError("abrir la bitácora de la sesión", ex);
            return null;
        }
    }

    /// <summary>Anota una línea ya interpretada, sin códigos de control.</summary>
    /// <param name="linea">Texto de la línea.</param>
    public void Anotar(string linea)
    {
        lock (_candado)
        {
            if (_escritor is not { } escritor)
            {
                return;
            }

            try
            {
                escritor.WriteLine(linea);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                DarDeBaja(ex);
            }
        }
    }

    /// <summary>Vuelca a disco lo escrito hasta ahora.</summary>
    public void Volcar()
    {
        lock (_candado)
        {
            if (_escritor is not { } escritor)
            {
                return;
            }

            try
            {
                escritor.Flush();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                DarDeBaja(ex);
            }
        }
    }

    public void Dispose()
    {
        lock (_candado)
        {
            try
            {
                _escritor?.Flush();
                _escritor?.Dispose();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _logger?.TechnicalError("cerrar la bitácora de la sesión", ex);
            }

            _escritor = null;
        }
    }

    private void DarDeBaja(Exception ex)
    {
        _logger?.TechnicalError("escribir la bitácora de la sesión", ex);

        try
        {
            _escritor?.Dispose();
        }
        catch (Exception otra) when (otra is IOException or ObjectDisposedException)
        {
            // La sesión sigue andando: acá ya no queda nada que salvar.
        }

        _escritor = null;
    }
}
