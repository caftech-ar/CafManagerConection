namespace CafManagerConection.Ssh;

public static class HostKeyPolicy
{
    // La comparación es ordinal y sensible a mayúsculas: el fingerprint es base64 y ahí <c>a</c> y <c>A</c> son valores distintos.
    public static bool YaEsConocida(string presentada, string? conocida)
    {
        if (string.IsNullOrWhiteSpace(conocida) || string.IsNullOrWhiteSpace(presentada))
        {
            return false;
        }

        return string.Equals(presentada.Trim(), conocida.Trim(), StringComparison.Ordinal);
    }
}
