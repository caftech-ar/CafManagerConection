using System.Security;

namespace CafManagerConection.Domain.Credentials;

public sealed class StoredCredential : IDisposable
{
    private char[] _secret;
    private bool _disposed;

    public StoredCredential(string userName, string? domain, ReadOnlySpan<char> secret)
    {
        UserName = userName ?? string.Empty;
        Domain = domain;
        _secret = secret.ToArray();
    }

    public string UserName { get; }

    public string? Domain { get; }

    /// <summary>Secreto en claro. Sólo debe leerse en el momento de usarlo.</summary>
    public ReadOnlySpan<char> Secret
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _secret;
        }
    }

    public bool HasSecret => !_disposed && _secret.Length > 0;

    /// <summary>Copia el secreto a una cadena; sólo donde la API destino exija <c>string</c> (SSH.NET).</summary>
    public string RevealSecret()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new string(_secret);
    }

    public override string ToString() => "StoredCredential(redactada)";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Array.Clear(_secret);
        _secret = [];
        _disposed = true;
    }
}
