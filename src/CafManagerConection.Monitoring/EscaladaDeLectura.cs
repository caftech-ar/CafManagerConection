using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Monitoring;

/// <summary>Prepara una lectura para que el reintento con privilegios de <c>sudo</c> llegue a hacerse (FR-184a).</summary>
public static class EscaladaDeLectura
{
    // /proc/1/io lo lee sólo root. ss y /proc/<pid>/io no fallan sin privilegios, devuelven menos: sin algo que falle de verdad, RunWithSudoFallbackAsync (Ssh/SshCommandRunner.cs:180) nunca llega a la rama de sudo.
    // LC_ALL=C obligatorio: cat traduce «Permission denied» y RunWithSudoFallbackAsync busca el texto en inglés para decidir si escala.
    public const string Canario = "LC_ALL=C cat /proc/1/io >/dev/null";

    // «cat … || exit 1;» y no «&&»: en A && B; C el punto y coma deja correr C igual y el estado final vuelve a ser cero.
    private const string Corte = " || exit 1; ";

    public static string Guardado(string comando) =>
        comando.StartsWith(Canario, StringComparison.Ordinal)
            ? comando
            : Canario + Corte + comando;
}

/// <summary>Qué se le dice al usuario en cada resultado del sondeo de <c>sudo</c> (FR-184a, FR-184d).</summary>
public static class MensajeDeEscalada
{
    public static bool MuestraElBoton(ResultadoDeSondeo? sondeo) =>
        sondeo is { } resultado && resultado.PuedeEscalar();

    public static string Texto(ResultadoDeSondeo? sondeo, string queNoSeVe) => sondeo switch
    {
        ResultadoDeSondeo.SinContrasena => $"Hay privilegios para ver {queNoSeVe}.",

        ResultadoDeSondeo.PideContrasena =>
            $"Se puede reintentar con privilegios para ver {queNoSeVe}, pero sudo va a pedir la "
            + "contraseña: se prueba la de la conexión y, si no sirve, se pide una vez por sesión.",

        ResultadoDeSondeo.Imposible =>
            $"No se puede escalar privilegios en este servidor: el usuario no está en sudoers, "
            + $"así que {queNoSeVe} no se va a poder ver.",

        _ => $"Todavía no se sabe si este usuario puede escalar privilegios para ver {queNoSeVe}.",
    };
}
