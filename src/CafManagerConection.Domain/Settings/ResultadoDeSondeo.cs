namespace CafManagerConection.Domain.Settings;

public enum ResultadoDeSondeo
{
    Imposible,
    PideContrasena,
    SinContrasena,
}

public static class SondeoDeSudo
{
    private static readonly string[] PideLaContrasena =
    [
        "a password is required",
        "no tty present",
        "a terminal is required",
        "askpass",
    ];

    public static ResultadoDeSondeo Interpretar(int codigoDeSalida, string salida, string error)
    {
        if (codigoDeSalida == 0)
        {
            return ResultadoDeSondeo.SinContrasena;
        }

        var dicho = salida + "\n" + error;

        return PideLaContrasena.Any(m => dicho.Contains(m, StringComparison.OrdinalIgnoreCase))
            ? ResultadoDeSondeo.PideContrasena
            : ResultadoDeSondeo.Imposible;
    }

    public static bool PuedeEscalar(this ResultadoDeSondeo resultado) =>
        resultado is not ResultadoDeSondeo.Imposible;

    public static bool EscalaSinPreguntar(this ResultadoDeSondeo resultado) =>
        resultado is ResultadoDeSondeo.SinContrasena;
}
