using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.Domain.Settings;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.App.ViewModels;

/// <summary>Un nodo del árbol de servidores: una carpeta o una conexión.</summary>
public sealed class NodoArbol : INotifyPropertyChanged
{
    /// <summary>Tamaño base del nombre, el mismo que Cuerpo en Estilos.xaml.</summary>
    private const double TamanoBase = 13;

    /// <summary>Tamaño base del texto secundario, el mismo que CuerpoChico en Estilos.xaml.</summary>
    private const double TamanoBaseSecundario = 12;

    /// <summary>Ajuste de tamaño de letra vigente para todo el árbol, en puntos relativos al base.</summary>
    public static double AjusteDeTamano { get; set; } = Domain.Settings.AjustesDelArbol.AjustePorOmision;

    public static bool MuestraServidor { get; set; }

    private bool _expandido;
    private bool _seleccionado;
    private SessionState? _estadoSesion;
    private Etiqueta? _etiquetaPropia;

    private NodoArbol(
        Guid id,
        string nombre,
        bool esCarpeta,
        Protocol? protocolo,
        ConnectionSummary? conexion,
        string? colorIcono,
        string? iconoElegido,
        string? descripcionPropia = null)
    {
        Id = id;
        Nombre = nombre;
        EsCarpeta = esCarpeta;
        Protocolo = protocolo;
        Conexion = conexion;
        ColorIcono = colorIcono;
        IconoElegido = iconoElegido;
        DescripcionPropia = descripcionPropia;
    }

    /// <summary>Descripcion de la carpeta. Las conexiones traen la suya dentro de su resumen.</summary>
    public string? DescripcionPropia { get; }

    public Guid Id { get; }

    public string Nombre { get; }

    public bool EsCarpeta { get; }

    public Protocol? Protocolo { get; }

    public ConnectionSummary? Conexion { get; }

    /// <summary>Clave del color elegido a mano, o null si usa el que le toca por omisión.</summary>
    public string? ColorIcono { get; }

    /// <summary>Clave del icono elegido a mano, o null si usa el que le toca por omisión. Nunca sale de la carpeta contenedora (FR-195b).</summary>
    public string? IconoElegido { get; }

    /// <summary>Recurso de geometría que gana: el elegido le gana al del protocolo (FR-195).</summary>
    public string ClaveDeIcono =>
        JuegoDeIconos.ClaveDeRecurso(IconoElegido) ?? IconoDeLaAplicacion;

    public Geometry? Icono =>
        Application.Current?.TryFindResource(ClaveDeIcono) as Geometry;

    private string IconoDeLaAplicacion => EsCarpeta
        ? "IconoCarpeta"
        : Protocolo switch
        {
            Protocol.Rdp => "IconoRdp",
            Protocol.Ssh => "IconoSsh",
            Protocol.Web => "IconoWeb",
            _ => "IconoAplicacion",
        };

    public string ClaveDePincel
    {
        get
        {
            if (PaletaIconos.EsValido(ColorIcono))
            {
                return "Icono" + char.ToUpperInvariant(ColorIcono![0]) + ColorIcono[1..];
            }

            if (EsCarpeta)
            {
                return "TextoTenue";
            }

            return Protocolo switch
            {
                Protocol.Rdp => "ProtocoloRdp",
                Protocol.Ssh => "ProtocoloSsh",
                Protocol.Web => "ProtocoloWeb",
                _ => "EstadoInactivo",
            };
        }
    }

    public bool EsFavorita => Conexion?.IsFavorite == true;

    public ObservableCollection<NodoArbol> Hijos { get; } = [];

    public NodoArbol? Padre { get; private set; }

    public bool Expandido
    {
        get => _expandido;
        set => Asignar(ref _expandido, value);
    }

    public bool Seleccionado
    {
        get => _seleccionado;
        set => Asignar(ref _seleccionado, value);
    }

    public SessionState? EstadoSesion
    {
        get => _estadoSesion;
        set
        {
            if (_estadoSesion == value)
            {
                return;
            }

            _estadoSesion = value;
            Avisar(nameof(EstadoSesion));
            Avisar(nameof(Conectada));
        }
    }

    public bool Conectada => EstadoSesion is not null;

    /// <summary>Etiqueta efectiva, propia o heredada de la carpeta. null si nadie la definio.</summary>
    public Etiqueta? Etiqueta => Conexion?.Etiqueta ?? EtiquetaPropia;

    /// <summary>La etiqueta de una carpeta, que no tiene ConnectionSummary de donde sacarla.</summary>
    public Etiqueta? EtiquetaPropia
    {
        get => _etiquetaPropia;
        set
        {
            if (ReferenceEquals(_etiquetaPropia, value))
            {
                return;
            }

            _etiquetaPropia = value;

            foreach (var derivada in new[]
                     {
                         nameof(EtiquetaPropia), nameof(Etiqueta), nameof(EtiquetaSigla),
                         nameof(TieneEtiqueta), nameof(ClaveDePincelDeEtiqueta), nameof(Resumen),
                     })
            {
                Avisar(derivada);
            }
        }
    }

    public string EtiquetaSigla => Etiqueta?.Codigo ?? string.Empty;

    public bool TieneEtiqueta => Etiqueta is not null;

    public string ClaveDePincelDeEtiqueta => Etiqueta?.ClaveDePincel ?? "Seleccion";

    public string Descripcion =>
        Conexion?.Description ?? DescripcionPropia ?? string.Empty;

    public double TamanoDeFuente => TamanoBase + AjusteDeTamano;

    public double TamanoDeFuenteSecundario => TamanoBaseSecundario + AjusteDeTamano;

    /// <summary>Servidor de la conexión —el campo Host tal como está cargado, sea IP o nombre—, o null si no corresponde mostrarlo.</summary>
    public string? Servidor => !EsCarpeta && MuestraServidor ? Conexion?.Host : null;

    public string? ServidorEntreParentesis =>
        TieneServidor ? $"({Servidor})" : null;

    public bool TieneServidor => !string.IsNullOrWhiteSpace(Servidor);

    /// <summary>Texto del tooltip: lo que no entra en la fila.</summary>
    public string Resumen
    {
        get
        {
            if (Conexion is not { } c)
            {
                var deCarpeta = new List<string> { Nombre };

                if (!string.IsNullOrWhiteSpace(DescripcionPropia))
                {
                    deCarpeta.Add(DescripcionPropia);
                }

                if (EtiquetaPropia is { } suya)
                {
                    deCarpeta.Add($"Etiqueta: {suya.Nombre}");
                }

                return string.Join(Environment.NewLine, deCarpeta);
            }

            var partes = new List<string> { Nombre };

            if (!string.IsNullOrEmpty(c.Description))
            {
                partes.Add(c.Description);
            }

            var destino = string.IsNullOrEmpty(c.EffectiveUserName)
                ? $"{c.Host}:{c.EffectivePort}"
                : $"{c.EffectiveUserName}@{c.Host}:{c.EffectivePort}";

            partes.Add($"{c.Protocol} · {destino}");

            if (Etiqueta is { } etiqueta)
            {
                partes.Add($"Etiqueta: {etiqueta.Nombre}");
            }

            partes.Add(c.LastConnectedAt is { } cuando
                ? $"Última conexión: {FormatearUltimaConexion(cuando, DateTimeOffset.Now)}"
                : "Última conexión: nunca");

            return string.Join(Environment.NewLine, partes);
        }
    }

    /// <summary>Convierte una marca de tiempo en un texto relativo y legible: «hace 5 minutos», «ayer», «el 12/03».</summary>
    public static string FormatearUltimaConexion(DateTimeOffset cuando, DateTimeOffset ahora)
    {
        var transcurrido = ahora - cuando;

        if (transcurrido < TimeSpan.Zero)
        {
            transcurrido = TimeSpan.Zero;
        }

        if (transcurrido < TimeSpan.FromMinutes(1))
        {
            return "hace un momento";
        }

        if (transcurrido < TimeSpan.FromMinutes(60))
        {
            var minutos = (int)transcurrido.TotalMinutes;
            return minutos == 1 ? "hace 1 minuto" : $"hace {minutos} minutos";
        }

        if (transcurrido < TimeSpan.FromHours(24))
        {
            var horas = (int)transcurrido.TotalHours;
            return horas == 1 ? "hace 1 hora" : $"hace {horas} horas";
        }

        if (transcurrido < TimeSpan.FromHours(48))
        {
            return "ayer";
        }

        if (transcurrido < TimeSpan.FromDays(7))
        {
            var dias = (int)transcurrido.TotalDays;
            return $"hace {dias} días";
        }

        var local = cuando.ToLocalTime();
        return local.Year == ahora.ToLocalTime().Year
            ? $"el {local.ToString("dd/MM", CultureInfo.InvariantCulture)}"
            : $"el {local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
    }

    public string CantidadDeHijos =>
        Conexion is null && Hijos.Count > 0 ? Hijos.Count.ToString() : string.Empty;

    public static NodoArbol Carpeta(Folder folder) =>
        new(
            folder.Id,
            folder.Name,
            esCarpeta: true,
            null,
            null,
            folder.ClaveDeColor,
            folder.ClaveDeIcono,
            folder.Description);

    public static NodoArbol Conectable(ConnectionSummary c) =>
        new(c.Id, c.Name, esCarpeta: false, c.Protocol, c, c.ClaveDeColor, c.ClaveDeIcono);

    public void Agregar(NodoArbol hijo)
    {
        hijo.Padre = this;
        Hijos.Add(hijo);
    }

    public IEnumerable<NodoArbol> Recorrer()
    {
        yield return this;

        foreach (var hijo in Hijos)
        {
            foreach (var nieto in hijo.Recorrer())
            {
                yield return nieto;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Asignar<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return;
        }

        campo = valor;
        Avisar(propiedad);
    }

    private void Avisar(string? propiedad) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}
