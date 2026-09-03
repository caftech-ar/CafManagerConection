using System.Runtime.Versioning;
using System.Windows;

namespace CafManagerConection.App.Services;

/// <summary>Avisos y confirmaciones.</summary>
[SupportedOSPlatform("windows")]
public static class Dialogos
{
    public static bool Confirmar(
        Window owner, string titulo, string mensaje, string? verbo = null) =>
        Views.MessageWindow.Confirmar(
            owner, titulo, mensaje, string.IsNullOrEmpty(verbo) ? "Aceptar" : verbo);

    /// <summary>Confirmación doble para un borrado que arrastra otras cosas con él.</summary>
    public static bool ConfirmarEnCascada(
        Window owner, string titulo, string mensaje, string nombre)
    {
        if (!Confirmar(owner, titulo, mensaje, "Continuar"))
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
