using CafManagerConection.Domain.Sessions;

namespace CafManagerConection.App.Services;

/// <summary>Arma el título de la ventana principal a partir de lo que está pasando (FR-041a).</summary>
public static class TituloDeVentana
{
    /// <summary>Nombre corto de la aplicación, el que se ve en la barra de tareas.</summary>
    public const string Aplicacion = "CMC";

    public static string Componer(
        string? conexion, SessionState estado, int sesiones, string? version = null)
    {
        var nombre = string.IsNullOrWhiteSpace(version)
            ? Aplicacion
            : $"{Aplicacion} {version}";

        if (sesiones <= 0 || string.IsNullOrWhiteSpace(conexion))
        {
            return nombre;
        }

        var detalles = new List<string>(2);

        if (estado != SessionState.Connected)
        {
            detalles.Add(estado switch
            {
                SessionState.Connecting => "conectando",
                SessionState.Disconnected => "desconectada",
                _ => "con error",
            });
        }

        if (sesiones > 1)
        {
            detalles.Add($"{sesiones} sesiones");
        }

        return detalles.Count == 0
            ? $"{nombre} - {conexion.Trim()}"
            : $"{nombre} - {conexion.Trim()} ({string.Join(", ", detalles)})";
    }
}
