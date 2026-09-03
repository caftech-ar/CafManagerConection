namespace CafManagerConection.UseCases.Abstractions;

// El escape a mano se olvida: en PlatformInventory estaba puesto en una llamada y olvidado catorce líneas más abajo.
public static class ShellPosix
{
    public static string EntreComillas(string texto) =>
        "'" + texto.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    public static string ComoUnSoloComando(string guion) => $"sh -c {EntreComillas(guion)}";
}
