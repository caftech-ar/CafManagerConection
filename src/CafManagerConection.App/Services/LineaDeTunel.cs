namespace CafManagerConection.App.Services;

/// <summary>Arma la línea de ssh -L equivalente a un túnel (FR-168g).</summary>
public static class LineaDeTunel
{
    /// <summary>Puerto por omisión de SSH, el único que no hace falta escribir.</summary>
    private const int PuertoSshHabitual = 22;

    public static string Armar(
        int puertoLocal,
        string hostRemoto,
        int puertoRemoto,
        string usuario,
        string host,
        int puertoSsh)
    {
        var destino = string.IsNullOrWhiteSpace(usuario)
            ? host
            : $"{usuario.Trim()}@{host}";

        var puerto = puertoSsh == PuertoSshHabitual || puertoSsh is < 1 or > 65535
            ? string.Empty
            : $"-p {puertoSsh} ";

        return $"ssh -N {puerto}-L {puertoLocal}:{hostRemoto}:{puertoRemoto} {destino}";
    }
}
