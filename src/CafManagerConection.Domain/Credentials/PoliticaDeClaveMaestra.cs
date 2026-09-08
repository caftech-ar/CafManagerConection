namespace CafManagerConection.Domain.Credentials;

public enum FaltaEnLaClaveMaestra
{
    Nada,
    EsCorta,
    SinLetra,
    SinDigito,
    SinCaracterEspecial,
}

public enum FuerzaDeLaClaveMaestra
{
    Insuficiente,
    Debil,
    Aceptable,
    Buena,
    Fuerte,
}

/// <summary>La forma que tiene que tener la clave maestra. Sin E/S: entra texto, sale qué falta y cuánta fuerza tiene.</summary>
public static class PoliticaDeClaveMaestra
{
    public const int LargoMinimo = 8;

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
            else if (EsEspecial(letra))
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

    /// <summary>Un espacio no cuenta: se acepta dentro de una frase, pero «abcdefg1 » no es una clave con carácter especial.</summary>
    public static bool EsEspecial(char letra) =>
        !char.IsLetterOrDigit(letra) && !char.IsWhiteSpace(letra) && !char.IsControl(letra);

    public static bool Cumple(ReadOnlySpan<char> clave) =>
        Revisar(clave) == FaltaEnLaClaveMaestra.Nada;

    /// <summary>El largo manda porque el KDF encarece cada intento pero no reduce cuántos hacen falta. La variedad sólo mueve el escalón cuando la clave es corta.</summary>
    public static FuerzaDeLaClaveMaestra Fuerza(ReadOnlySpan<char> clave)
    {
        if (!Cumple(clave))
        {
            return FuerzaDeLaClaveMaestra.Insuficiente;
        }

        var variedad = Variedad(clave);

        return clave.Length switch
        {
            >= 24 => FuerzaDeLaClaveMaestra.Fuerte,
            >= 16 => variedad >= 3 ? FuerzaDeLaClaveMaestra.Fuerte : FuerzaDeLaClaveMaestra.Buena,
            >= 12 => variedad >= 4 ? FuerzaDeLaClaveMaestra.Buena : FuerzaDeLaClaveMaestra.Aceptable,
            _ => variedad >= 4 ? FuerzaDeLaClaveMaestra.Aceptable : FuerzaDeLaClaveMaestra.Debil,
        };
    }

    public static string Explicar(FaltaEnLaClaveMaestra falta) => falta switch
    {
        FaltaEnLaClaveMaestra.EsCorta => $"Tiene que tener al menos {LargoMinimo} caracteres.",
        FaltaEnLaClaveMaestra.SinLetra => "Le falta al menos una letra.",
        FaltaEnLaClaveMaestra.SinDigito => "Le falta al menos un número.",
        FaltaEnLaClaveMaestra.SinCaracterEspecial =>
            "Le falta al menos un carácter especial, como ! # o $. Un espacio no cuenta.",
        _ => string.Empty,
    };

    public static string Nombrar(FuerzaDeLaClaveMaestra fuerza) => fuerza switch
    {
        FuerzaDeLaClaveMaestra.Insuficiente => "no alcanza",
        FuerzaDeLaClaveMaestra.Debil => "débil — una frase larga es mucho mejor",
        FuerzaDeLaClaveMaestra.Aceptable => "aceptable — una frase larga es mejor",
        FuerzaDeLaClaveMaestra.Buena => "buena",
        _ => "fuerte",
    };

    private static int Variedad(ReadOnlySpan<char> clave)
    {
        bool minuscula = false, mayuscula = false, digito = false, especial = false;

        foreach (var letra in clave)
        {
            if (char.IsLower(letra)) { minuscula = true; }
            else if (char.IsUpper(letra)) { mayuscula = true; }
            else if (char.IsDigit(letra)) { digito = true; }
            else if (EsEspecial(letra)) { especial = true; }
        }

        return (minuscula ? 1 : 0) + (mayuscula ? 1 : 0) + (digito ? 1 : 0) + (especial ? 1 : 0);
    }
}
