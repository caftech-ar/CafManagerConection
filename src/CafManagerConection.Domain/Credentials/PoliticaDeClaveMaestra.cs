namespace CafManagerConection.Domain.Credentials;

public enum FaltaEnLaClaveMaestra
{
    Nada,
    EsCorta,
    SinLetra,
    SinDigito,
    SinCaracterEspecial,
}

/// <summary>La forma que tiene que tener la clave maestra. Sin E/S: entra texto, sale qué falta.</summary>
public static class PoliticaDeClaveMaestra
{
    public const int LargoMinimo = 8;

    /// <summary>No hay tope real; el campo lo acota para que un pegado accidental no cuelgue la derivación.</summary>
    public const int LargoMaximo = 1024;

    public static FaltaEnLaClaveMaestra Revisar(ReadOnlySpan<char> clave)
    {
        if (clave.Length < LargoMinimo)
        {
            return FaltaEnLaClaveMaestra.EsCorta;
        }

        var hayLetra = false;
        var hayDigito = false;
        var hayEspecial = false;

        foreach (var letra in clave)
        {
            if (char.IsLetter(letra))
            {
                hayLetra = true;
            }
            else if (char.IsDigit(letra))
            {
                hayDigito = true;
            }
            else
            {
                hayEspecial = true;
            }
        }

        if (!hayLetra)
        {
            return FaltaEnLaClaveMaestra.SinLetra;
        }

        if (!hayDigito)
        {
            return FaltaEnLaClaveMaestra.SinDigito;
        }

        return hayEspecial ? FaltaEnLaClaveMaestra.Nada : FaltaEnLaClaveMaestra.SinCaracterEspecial;
    }

    public static bool Cumple(ReadOnlySpan<char> clave) =>
        Revisar(clave) == FaltaEnLaClaveMaestra.Nada;

    public static string Explicar(FaltaEnLaClaveMaestra falta) => falta switch
    {
        FaltaEnLaClaveMaestra.EsCorta => $"Tiene que tener al menos {LargoMinimo} caracteres.",
        FaltaEnLaClaveMaestra.SinLetra => "Le falta al menos una letra.",
        FaltaEnLaClaveMaestra.SinDigito => "Le falta al menos un número.",
        FaltaEnLaClaveMaestra.SinCaracterEspecial =>
            "Le falta al menos un carácter especial, como ! # o $.",
        _ => string.Empty,
    };
}
