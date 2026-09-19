namespace CafManagerConection.Domain.Settings;

/// <summary>Los topes que comparten el dominio, la validación y la interfaz, para que un mismo campo no tenga dos máximos.</summary>
public static class Limites
{
    /// <summary>Máximo del intervalo de keep-alive SSH, en segundos. Lo repite el <c>CHECK</c> del esquema.</summary>
    public const int MaxKeepAliveSeconds = 3600;

    public const int PuertoMinimo = 1;

    public const int PuertoMaximo = 65535;

    /// <summary>Si el puerto es nulo (hereda) o cae dentro del rango válido.</summary>
    public static bool PuertoAdmisible(int? puerto) =>
        puerto is null || (puerto >= PuertoMinimo && puerto <= PuertoMaximo);

    /// <summary>Si el intervalo es nulo (hereda) o cae dentro del rango válido.</summary>
    public static bool KeepAliveAdmisible(int? segundos) =>
        segundos is null || (segundos >= 0 && segundos <= MaxKeepAliveSeconds);
}
