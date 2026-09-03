namespace CafManagerConection.Domain.Connections;

public sealed class Folder
{
    public const int MaxNameLength = 100;

    public Folder(Guid id, string name, Guid? parentId = null, int sortOrder = 0)
    {
        Id = id;
        Name = Validate(name);
        ParentId = parentId;
        SortOrder = sortOrder;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public Guid? ParentId { get; private set; }

    /// <summary><c>null</c> usa el color global del protocolo (FR-135).</summary>
    public string? ClaveDeColor { get; set; }

    /// <summary><c>null</c> usa el icono de la aplicación. No se hereda (FR-195b).</summary>
    public string? ClaveDeIcono { get; set; }

    /// <summary>Línea corta que acompaña al nombre en el árbol (FR-131).</summary>
    public string? Description
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }


    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public FolderSettings Settings { get; init; } = new();

    public void Rename(string name)
    {
        Name = Validate(name);
        Touch();
    }

    /// <summary>Quien llama debe descartar los ciclos: la carpeta no conoce el árbol completo.</summary>
    public void MoveTo(Guid? newParentId)
    {
        if (newParentId == Id)
        {
            throw new InvalidOperationException(
                "Una carpeta no puede ser su propia carpeta contenedora.");
        }

        ParentId = newParentId;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new ArgumentException("El nombre de la carpeta no puede estar vacío.", nameof(name));
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"El nombre de la carpeta no puede superar los {MaxNameLength} caracteres.",
                nameof(name));
        }

        return trimmed;
    }
}
