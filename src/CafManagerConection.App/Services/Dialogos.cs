using System.Runtime.Versioning;
using System.Windows;

namespace CafManagerConection.App.Services;

/// <summary>Avisos y confirmaciones.</summary>
[SupportedOSPlatform("windows")]
public static class Dialogos
{
    /// <summary>Pregunta y devuelve si se confirmó.</summary>
    /// <param name="owner">Ventana sobre la que se abre.</param>
    /// <param name="titulo">Título de la ventana.</param>
    /// <param name="mensaje">Lo que se pregunta.</param>
    /// <param name="verbo">Texto del botón que confirma.</param>
    /// <param name="destructivo">Si confirmar borra o descarta algo; entonces Enter cancela.</param>
    public static bool Confirmar(
        Window owner,
        string titulo,
        string mensaje,
        string? verbo = null,
        bool destructivo = false) =>
        Views.MessageWindow.Confirmar(
            owner,
            titulo,
            mensaje,
            string.IsNullOrEmpty(verbo) ? "Aceptar" : verbo,
            destructivo: destructivo);

    /// <summary>Confirmación doble para un borrado que arrastra otras cosas con él.</summary>
    public static bool ConfirmarEnCascada(
        Window owner, string titulo, string mensaje, string nombre)
    {
        if (!Confirmar(owner, titulo, mensaje, "Continuar", destructivo: true))
        {
            return false;
        }

        var escrito = Views.TextPromptWindow.Pedir(
            owner,
            titulo,
            $"Para confirmar, escribí el nombre exacto:{Environment.NewLine}{nombre}");

        if (string.Equals(escrito, nombre, StringComparison.Ordinal))
        {
            return true;
        }

        if (escrito is not null)
        {
            Advertir(
                owner,
                titulo,
                "El nombre no coincide, así que no se eliminó nada.");
        }

        return false;
    }

    public static void Informar(Window owner, string titulo, string mensaje) =>
        Views.MessageWindow.Avisar(owner, titulo, mensaje);

    public static void Advertir(Window owner, string titulo, string mensaje) =>
        Views.MessageWindow.Avisar(owner, titulo, mensaje);
}
