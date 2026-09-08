using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;

namespace CafManagerConection.App.Panels;

/// <summary>Las filas que consumen las plantillas del XAML del panel de estado.</summary>
[SupportedOSPlatform("windows")]
public partial class StatusPanel
{
    public sealed record FilaDisco(
        string Punto, string Tipo, string Detalle, double Porcentaje, Brush Color, string Nivel);

    /// <summary>Fila de actividad de un dispositivo de bloques.</summary>
    public sealed record FilaIo(string Dispositivo, string Detalle);

    public sealed record FilaRed(
        string Interfaz,
        string Detalle,
        string Direcciones,
        string Enlace,
        Brush Color)
    {
        /// <summary>Una interfaz sin ninguna dirección no muestra una línea vacía.</summary>
        public Visibility VisibilidadDirecciones =>
            Direcciones.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public sealed record FilaRuta(string Destino, string Detalle, Brush Color);

    public sealed record FilaProceso(
        int Pid,
        string Nombre,
        string Cpu,
        string Memoria,
        Brush ColorCpu,
        string Ayuda,
        string Comando);

    public sealed record FilaTemperatura(string Sensor, string Grados, Brush Color);
}
