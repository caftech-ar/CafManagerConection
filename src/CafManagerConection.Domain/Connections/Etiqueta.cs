using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Connections;

public enum TipoDeElemento
{
    Conexion,
    Carpeta,
}

public sealed class Etiqueta
{
    public const int LargoMaximoDeCodigo = 5;

    public const int LargoMaximoDeNombre = 40;

    public Etiqueta(Guid id, string codigo, string nombre, string claveDeColor, int orden = 0)
    {
        Id = id;
        Codigo = Normalizar(codigo, LargoMaximoDeCodigo).ToUpperInvariant();
        Nombre = Normalizar(nombre, LargoMaximoDeNombre);
        ClaveDeColor = claveDeColor;
        Orden = orden;
    }

    public Guid Id { get; }

    /// <summary>Sigla del árbol, siempre en mayúsculas: «prd» y «PRD» son el mismo código.</summary>
    public string Codigo { get; private set; }

    public string Nombre { get; private set; }

    public string ClaveDeColor { get; private set; }

    public int Orden { get; set; }

    public bool EsValida =>
        Codigo.Length > 0
        && Nombre.Length > 0
        && PaletaIconos.EsValido(ClaveDeColor);

    public void Renombrar(string codigo, string nombre, string claveDeColor)
    {
        Codigo = Normalizar(codigo, LargoMaximoDeCodigo).ToUpperInvariant();
        Nombre = Normalizar(nombre, LargoMaximoDeNombre);
        ClaveDeColor = claveDeColor;
    }

    public string ClaveDePincel => PaletaIconos.ClaveDeRecurso(ClaveDeColor);

    private static string Normalizar(string? valor, int largoMaximo)
    {
        var limpio = (valor ?? string.Empty).Trim();

        return limpio.Length <= largoMaximo ? limpio : limpio[..largoMaximo];
    }
}

public sealed class CatalogoDeEtiquetas
{
    private readonly List<Etiqueta> _etiquetas;

    public CatalogoDeEtiquetas(IEnumerable<Etiqueta>? etiquetas = null) =>
        _etiquetas = [.. (etiquetas ?? []).OrderBy(e => e.Orden).ThenBy(e => e.Nombre)];

    public IReadOnlyList<Etiqueta> Todas => _etiquetas;

    public Etiqueta? Por(Guid id) => _etiquetas.FirstOrDefault(e => e.Id == id);

    /// <summary>Por qué no se puede, o <c>null</c> si se puede; <paramref name="excepto"/> no cuenta como conflicto.</summary>
    public string? PorQueNo(string codigo, string nombre, string claveDeColor, Guid? excepto = null)
    {
        var propuesta = new Etiqueta(Guid.Empty, codigo, nombre, claveDeColor);

        if (propuesta.Codigo.Length == 0)
        {
            return "El código no puede estar vacío.";
        }

        if (propuesta.Nombre.Length == 0)
        {
            return "El nombre no puede estar vacío.";
        }

        if (!PaletaIconos.EsValido(claveDeColor))
        {
            return "Hay que elegir un color de la paleta.";
        }

        if (_etiquetas.Any(e => e.Id != excepto
                                && string.Equals(e.Codigo, propuesta.Codigo, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Ya hay una etiqueta con el código «{propuesta.Codigo}».";
        }

        return _etiquetas.Any(e => e.Id != excepto
                                   && string.Equals(e.Nombre, propuesta.Nombre, StringComparison.CurrentCultureIgnoreCase))
            ? $"Ya hay una etiqueta llamada «{propuesta.Nombre}»."
            : null;
    }

    public Etiqueta? Agregar(string codigo, string nombre, string claveDeColor)
    {
        if (PorQueNo(codigo, nombre, claveDeColor) is not null)
        {
            return null;
        }

        var siguiente = _etiquetas.Count == 0 ? 1 : _etiquetas.Max(e => e.Orden) + 1;
        var nueva = new Etiqueta(Guid.NewGuid(), codigo, nombre, claveDeColor, siguiente);

        _etiquetas.Add(nueva);
        return nueva;
    }

    public bool Actualizar(Guid id, string codigo, string nombre, string claveDeColor)
    {
        if (Por(id) is not { } etiqueta || PorQueNo(codigo, nombre, claveDeColor, id) is not null)
        {
            return false;
        }

        etiqueta.Renombrar(codigo, nombre, claveDeColor);
        return true;
    }

    public bool Quitar(Guid id) => _etiquetas.RemoveAll(e => e.Id == id) > 0;
}
