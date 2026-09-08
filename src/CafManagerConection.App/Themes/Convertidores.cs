using CafManagerConection.Domain.Settings;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.App.Themes;

/// <summary>Convertidores para los enlaces del XAML.</summary>
internal static class Pinceles
{
    /// <summary>Pincel de la paleta activa, con una reserva por si el recurso no está.</summary>
    public static Brush De(string clave)
    {
        if (Application.Current?.TryFindResource(clave) is Brush pincel)
        {
            return pincel;
        }

        return Brushes.Gray;
    }

    /// <summary>Pincel de una clave de la paleta de iconos («azul», «verde»...).</summary>
    public static Brush DeColor(string? clave) => De(PaletaIconos.ClaveDeRecurso(clave));
}

/// <summary>Seminegrita para las carpetas, normal para el resto.</summary>
public sealed class SiNegrita : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeights.SemiBold : FontWeights.Normal;

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class TextoDeEstado : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            SessionState.Connected => "Sesión conectada",
            SessionState.Connecting => "Conectando…",
            SessionState.Error => "La sesión falló",
            SessionState.Disconnected => "Sesión desconectada",
            _ => "Sin sesión",
        };

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class PincelPorClave : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Pinceles.De(value as string ?? "EstadoInactivo");

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

/// <summary>Convierte la clave de un icono en su geometría. Devuelve una geometría vacía si la clave no existe: un icono que falta no puede tirar abajo la fila entera.</summary>
public sealed class GeometriaPorClave : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string clave || clave.Length == 0)
        {
            return Geometry.Empty;
        }

        return Application.Current?.TryFindResource(clave) as Geometry ?? Geometry.Empty;
    }

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class ColorDeEstado : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Pinceles.De(value switch
        {
            SessionState.Connected => "EstadoConectado",
            SessionState.Connecting => "EstadoConectando",
            SessionState.Error => "EstadoError",
            _ => "EstadoInactivo",
        });

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}
