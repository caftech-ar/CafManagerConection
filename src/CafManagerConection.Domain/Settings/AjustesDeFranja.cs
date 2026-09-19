namespace CafManagerConection.Domain.Settings;

/// <summary>Si las sesiones SSH muestran la franja de métricas y cada cuánto se refresca.</summary>
public sealed record AjustesDeFranja(bool Activa = true, int Segundos = 5)
{
    public const int MinimoDeSegundos = 2;

    public const int MaximoDeSegundos = 60;

    public static AjustesDeFranja Default { get; } = new();

    public AjustesDeFranja Normalizados() => this with
    {
        Segundos = Math.Clamp(Segundos, MinimoDeSegundos, MaximoDeSegundos),
    };
}
