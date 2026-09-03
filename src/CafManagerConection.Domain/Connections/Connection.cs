namespace CafManagerConection.Domain.Connections;

public sealed class Connection
{
    public const int MaxNameLength = 100;
    public const int MaxNotesLength = 4000;

    public const int MaxDescriptionLength = 200;


    private readonly Dictionary<string, string> _customFields =
        new(StringComparer.OrdinalIgnoreCase);

    public Connection(Guid id, string name, Protocol protocol, string host)
    {
        Id = id;
        Name = ValidateName(name);
        Protocol = protocol;
        Host = ValidateHost(host);
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public Protocol Protocol { get; }

    public string Host { get; private set; }

    public Guid? FolderId { get; set; }

    public int? Port { get; private set; }

    public string? UserName { get; set; }

    /// <summary><c>null</c> hereda. Nunca contiene el secreto (Principio II).</summary>
    public string? CredentialKey { get; set; }

    public string? Notes
    {
        get;
        set => field = ValidateNotes(value);
    }

    /// <summary>Conexión de la que ésta cuelga (FR-125); el único nivel lo verifica <c>ConnectionValidator</c> (FR-127).</summary>
    public Guid? ParentConnectionId
    {
        get;
        set
        {
            if (value == Id)
            {
                throw new ArgumentException(
                    "Una conexión no puede colgar de sí misma.", nameof(value));
            }

            field = value;
        }
    }

    /// <summary><c>null</c> cae en el color global del protocolo. No se hereda (FR-195b).</summary>
    public string? ClaveDeColor { get; set; }

    /// <summary><c>null</c> usa el icono del protocolo. No se hereda (FR-195b).</summary>
    public string? ClaveDeIcono { get; set; }

    public string? Description
    {
        get;
        set => field = ValidateDescription(value);
    }

    /// <summary>Etiqueta del catálogo, por identificador; <c>null</c> hereda la de la carpeta (FR-130).</summary>
    public Guid? TagId { get; set; }

    public bool IsFavorite { get; set; }

    public string? DocumentationUrl
    {
        get;
        set => field = ValidateDocumentationUrl(value);
    }

    /// <summary>Pares nombre/valor sueltos; no se indexan ni se buscan (FR-133).</summary>
    public IReadOnlyDictionary<string, string> CustomFields => _customFields;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastConnectedAt { get; set; }

    public static int DefaultPortFor(Protocol protocol) => protocol switch
    {
        Protocol.Rdp => 3389,
        Protocol.Ssh => 22,
        Protocol.Web => 443,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null),
    };

    public void Rename(string name)
    {
        Name = ValidateName(name);
        Touch();
    }

    public void ChangeHost(string host)
    {
        Host = ValidateHost(host);
        Touch();
    }

    public void SetPort(int? port)
    {
        if (port is { } value && (value < 1 || value > 65535))
        {
            throw new ArgumentOutOfRangeException(
                nameof(port), value, "El puerto debe estar entre 1 y 65535.");
        }

        Port = port;
        Touch();
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new ArgumentException("El nombre de la conexión no puede estar vacío.", nameof(name));
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"El nombre no puede superar los {MaxNameLength} caracteres.", nameof(name));
        }

        return trimmed;
    }

    private static string ValidateHost(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var trimmed = host.Trim();

        if (trimmed.Length == 0)
        {
            throw new ArgumentException("El host no puede estar vacío.", nameof(host));
        }

        return trimmed;
    }

    public void SetCustomField(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El campo propio necesita un nombre.", nameof(name));
        }

        var clave = name.Trim();

        if (value is null)
        {
            _customFields.Remove(clave);
        }
        else
        {
            _customFields[clave] = value;
        }

        Touch();
    }

    public void ClearCustomFields()
    {
        _customFields.Clear();
        Touch();
    }

    private static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var limpia = description.Trim();

        if (limpia.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"La descripción no puede superar los {MaxDescriptionLength} caracteres.",
                nameof(description));
        }

        return limpia;
    }

    private static string? ValidateDocumentationUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var limpia = url.Trim();

        if (!Uri.TryCreate(limpia, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "La dirección de documentación debe ser una URL http o https.", nameof(url));
        }

        return limpia;
    }

    private static string? ValidateNotes(string? notes)
    {
        if (notes is not null && notes.Length > MaxNotesLength)
        {
            throw new ArgumentException(
                $"Las notas no pueden superar los {MaxNotesLength} caracteres.", nameof(notes));
        }

        return notes;
    }
}
