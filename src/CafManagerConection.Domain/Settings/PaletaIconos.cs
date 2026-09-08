namespace CafManagerConection.Domain.Settings;

public sealed record ColorIcono(string Clave, string Nombre);

public static class PaletaIconos
{
    public static IReadOnlyList<ColorIcono> Colores { get; } =
    [
        new("azul", "Azul"),
        new("verde", "Verde"),
        new("ambar", "Ámbar"),
        new("rojo", "Rojo"),
        new("violeta", "Violeta"),
        new("cyan", "Cyan"),
        new("rosa", "Rosa"),
        new("lima", "Lima"),
        new("naranja", "Naranja"),
        new("gris", "Gris"),
    ];

    public const string PorOmisionRdp = "azul";
    public const string PorOmisionSsh = "verde";
    public const string PorOmisionWeb = "naranja";

    public static bool EsValido(string? clave) =>
        clave is not null && Colores.Any(c => c.Clave == clave);

    /// <summary>«azul» se convierte en el recurso «IconoAzul».</summary>
    public static string ClaveDeRecurso(string? clave) =>
        EsValido(clave)
            ? "Icono" + char.ToUpperInvariant(clave![0]) + clave[1..]
            : "TextoTenue";

    public static ColorIcono Resolver(string? clave, string porOmision) =>
        Colores.FirstOrDefault(c => c.Clave == clave)
        ?? Colores.First(c => c.Clave == porOmision);
}

public sealed record ColoresDeIconos(string Rdp, string Ssh, string Web)
{
    public static ColoresDeIconos Default { get; } = new(
        PaletaIconos.PorOmisionRdp,
        PaletaIconos.PorOmisionSsh,
        PaletaIconos.PorOmisionWeb);
}
