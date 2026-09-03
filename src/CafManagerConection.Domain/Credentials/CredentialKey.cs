using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Credentials;

/// <summary>Referencia opaca a una credencial del almacén del sistema; nunca el secreto (Principio II).</summary>
public readonly record struct CredentialKey
{
    public const string Prefix = "cmc";

    private CredentialKey(string value) => Value = value;

    public string Value { get; }

    public static CredentialKey ForConnection(Guid connectionId, Protocol protocol) =>
        new($"{Prefix}:{Scope(protocol)}:{connectionId:D}");

    /// <summary>Credencial heredable de carpeta: <c>cmc:folder:{id}:ssh</c>, una por protocolo (FR-064a).</summary>
    public static CredentialKey ForFolder(Guid folderId, Protocol protocol) =>
        new($"{Prefix}:folder:{folderId:D}:{Scope(protocol)}");

    public static CredentialKey FromStored(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!value.StartsWith(Prefix + ":", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Una clave de credencial debe empezar con '{Prefix}:'.", nameof(value));
        }

        return new CredentialKey(value);
    }

    public static bool TryParse(string? value, out CredentialKey key)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            value.StartsWith(Prefix + ":", StringComparison.Ordinal))
        {
            key = new CredentialKey(value);
            return true;
        }

        key = default;
        return false;
    }

    private static string Scope(Protocol protocol) => protocol switch
    {
        Protocol.Rdp => "rdp",
        Protocol.Ssh => "ssh",
        Protocol.Web => "web",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null),
    };

    public override string ToString() => Value;
}
