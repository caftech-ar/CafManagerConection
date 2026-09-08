namespace CafManagerConection.Domain.Settings;

public sealed record EscalonDeTamano(string Nombre, double Ajuste);

public sealed record AjustesDelArbol(
    double AjusteDeTamano = AjustesDelArbol.AjustePorOmision, bool MuestraHost = false)
{
    public const double Paso = 1.5;

    public const double AjustePorOmision = -Paso;

    public static IReadOnlyList<EscalonDeTamano> Escalones { get; } =
    [
        new("Muy chico", -2 * Paso),
        new("Chico", -Paso),
        new("Normal", 0),
        new("Grande", Paso),
        new("Muy grande", 2 * Paso),
    ];

    public static double MinimoAjuste => Escalones[0].Ajuste;

    public static double MaximoAjuste => Escalones[^1].Ajuste;

    public int IndiceDeEscalon()
    {
        var mejor = 0;

        for (var i = 1; i < Escalones.Count; i++)
        {
            if (Math.Abs(Escalones[i].Ajuste - AjusteDeTamano)
                < Math.Abs(Escalones[mejor].Ajuste - AjusteDeTamano))
            {
                mejor = i;
            }
        }

        return mejor;
    }

    public AjustesDelArbol ConEscalon(int indice) => this with
    {
        AjusteDeTamano = Escalones[Math.Clamp(indice, 0, Escalones.Count - 1)].Ajuste,
    };

    public AjustesDelArbol Acotado() => this with
    {
        AjusteDeTamano = Math.Clamp(AjusteDeTamano, MinimoAjuste, MaximoAjuste),
    };
}
