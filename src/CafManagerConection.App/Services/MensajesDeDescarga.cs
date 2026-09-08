using CafManagerConection.Infrastructure.Actualizaciones;

namespace CafManagerConection.App.Services;

/// <summary>Qué decirle al usuario, y si corresponde ejecutar el instalador, para cada desenlace posible de DescargarYVerificarAsync.</summary>
public static class MensajesDeDescarga
{
    public static (string Mensaje, bool DebeEjecutarse) Interpretar(ResultadoDeDescarga resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.Estado switch
        {
            EstadoDeDescarga.Verificada =>
                ("Descarga verificada. Iniciando el instalador…", true),

            EstadoDeDescarga.HashNoCoincide =>
                ($"No se instaló: {resultado.Motivo}", false),

            EstadoDeDescarga.SinHashPublicado =>
                ($"No se instaló: {resultado.Motivo}", false),

            EstadoDeDescarga.Fallo =>
                ($"No se pudo descargar: {resultado.Motivo}", false),

            _ => ("No se pudo determinar qué pasó con la descarga.", false),
        };
    }
}
