namespace CafManagerConection.Domain.Settings;

/// <summary>Compara por componentes numéricos y no como texto: <c>0.0.10</c> es posterior a <c>0.0.9</c> (FR-163).</summary>
public sealed class VersionDeAplicacion : IComparable<VersionDeAplicacion>, IEquatable<VersionDeAplicacion>
{
    private readonly IReadOnlyList<int> _componentes;

    private VersionDeAplicacion(IReadOnlyList<int> componentes, string? prerelease)
    {
        _componentes = componentes;
        Prerelease = prerelease;
    }

    public IReadOnlyList<int> Componentes => _componentes;

    public string? Prerelease { get; }

    public static bool TryParse(string? texto, out VersionDeAplicacion? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var limpio = texto.Trim();

        if (limpio.StartsWith('v') || limpio.StartsWith('V'))
        {
            limpio = limpio[1..];
        }

        var indiceGuion = limpio.IndexOf('-');
        string nucleo;
        string? prerelease;

        if (indiceGuion < 0)
        {
            nucleo = limpio;
            prerelease = null;
        }
        else
        {
            nucleo = limpio[..indiceGuion];
            prerelease = limpio[(indiceGuion + 1)..];

            if (prerelease.Length == 0)
            {
                return false;
            }
        }

        var partes = nucleo.Split('.');

        if (partes.Length == 0)
        {
            return false;
        }

        var componentes = new int[partes.Length];

        for (var i = 0; i < partes.Length; i++)
        {
            if (!int.TryParse(
                    partes[i],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var componente))
            {
                return false;
            }

            componentes[i] = componente;
        }

        version = new VersionDeAplicacion(componentes, prerelease);
        return true;
    }

    public static VersionDeAplicacion Parse(string texto) =>
        TryParse(texto, out var version)
            ? version!
            : throw new FormatException($"'{texto}' no es un número de versión válido.");

    public int CompareTo(VersionDeAplicacion? otra)
    {
        if (otra is null)
        {
            return 1;
        }

        var cantidad = Math.Max(_componentes.Count, otra._componentes.Count);

        for (var i = 0; i < cantidad; i++)
        {
            var propio = i < _componentes.Count ? _componentes[i] : 0;
            var ajeno = i < otra._componentes.Count ? otra._componentes[i] : 0;

            if (propio != ajeno)
            {
                return propio.CompareTo(ajeno);
            }
        }

        if (Prerelease is null && otra.Prerelease is null)
        {
            return 0;
        }

        if (Prerelease is null)
        {
            return 1;
        }

        if (otra.Prerelease is null)
        {
            return -1;
        }

        return string.CompareOrdinal(Prerelease, otra.Prerelease);
    }

    public bool Equals(VersionDeAplicacion? otra) => otra is not null && CompareTo(otra) == 0;

    public override bool Equals(object? obj) => Equals(obj as VersionDeAplicacion);

    public override int GetHashCode()
    {
        var acumulado = 0;

        for (var i = 0; i < _componentes.Count; i++)
        {
            if (_componentes[i] != 0)
            {
                acumulado = HashCode.Combine(acumulado, i, _componentes[i]);
            }
        }

        return HashCode.Combine(acumulado, Prerelease);
    }

    public override string ToString() =>
        Prerelease is null ? string.Join('.', _componentes) : $"{string.Join('.', _componentes)}-{Prerelease}";

    public static bool operator ==(VersionDeAplicacion? izquierda, VersionDeAplicacion? derecha) =>
        izquierda is null ? derecha is null : izquierda.Equals(derecha);

    public static bool operator !=(VersionDeAplicacion? izquierda, VersionDeAplicacion? derecha) =>
        !(izquierda == derecha);

    public static bool operator <(VersionDeAplicacion izquierda, VersionDeAplicacion derecha) =>
        izquierda.CompareTo(derecha) < 0;

    public static bool operator >(VersionDeAplicacion izquierda, VersionDeAplicacion derecha) =>
        izquierda.CompareTo(derecha) > 0;

    public static bool operator <=(VersionDeAplicacion izquierda, VersionDeAplicacion derecha) =>
        izquierda.CompareTo(derecha) <= 0;

    public static bool operator >=(VersionDeAplicacion izquierda, VersionDeAplicacion derecha) =>
        izquierda.CompareTo(derecha) >= 0;
}

public static class PoliticaDeActualizaciones
{
    /// <summary>Posponer silencia el aviso hasta el día siguiente y sólo para esa versión (FR-160a).</summary>
    public static bool CorrespondeAvisar(
        VersionDeAplicacion versionDisponible,
        VersionDeAplicacion? versionPospuesta,
        DateTimeOffset? momentoDePosposicion,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(versionDisponible);

        if (versionPospuesta is null || momentoDePosposicion is null)
        {
            return true;
        }

        if (versionDisponible != versionPospuesta)
        {
            return true;
        }

        return momentoDePosposicion.Value.LocalDateTime.Date != ahora.LocalDateTime.Date;
    }
}
