namespace CafManagerConection.Monitoring;

public enum NivelDeMedida
{
    Normal,
    Advertencia,
    Critico,
}

public static class NivelDeUso
{
    public const double UmbralAdvertencia = 75;

    public const double UmbralCritico = 90;

    public static NivelDeMedida DePorcentaje(double porcentaje) => porcentaje switch
    {
        >= UmbralCritico => NivelDeMedida.Critico,
        >= UmbralAdvertencia => NivelDeMedida.Advertencia,
        _ => NivelDeMedida.Normal,
    };

    public static NivelDeMedida DeCarga(double carga, int nucleos)
    {
        if (nucleos <= 0)
        {
            return NivelDeMedida.Normal;
        }

        return (carga / nucleos) switch
        {
            >= 1.5 => NivelDeMedida.Critico,
            >= 1.0 => NivelDeMedida.Advertencia,
            _ => NivelDeMedida.Normal,
        };
    }

    /// <summary>Texto corto del tramo, para decirlo también sin color; el normal no lleva texto (FR-087b).</summary>
    public static string? Etiqueta(NivelDeMedida nivel) => nivel switch
    {
        NivelDeMedida.Critico => "crítico",
        NivelDeMedida.Advertencia => "atención",
        _ => null,
    };
}
