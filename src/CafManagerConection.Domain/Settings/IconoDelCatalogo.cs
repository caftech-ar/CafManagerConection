namespace CafManagerConection.Domain.Settings;

/// <summary>Si el icono se dibuja pintando su contorno o rellenando su silueta.</summary>
public enum ModoDePintado
{
    Trazo,
    Relleno,
}

/// <summary>Si el icono representa una idea o identifica un producto de un tercero.</summary>
public enum FamiliaDeIcono
{
    Concepto,
    Logo,
}

/// <summary>De qué paquete salió un icono, para poder volver a la fuente y saber qué licencia le toca.</summary>
public sealed record OrigenDeIcono(string Paquete, string Version, string Ruta, string Licencia)
{
    public override string ToString() => $"{Paquete} {Version} · {Ruta} · {Licencia}";
}

/// <summary>Conjunto temático de iconos: ordena la carpeta, el encabezado de la grilla y el filtro.</summary>
/// <param name="Clave">Nombre de la carpeta bajo Assets/Iconos.</param>
/// <param name="Nombre">Cómo se muestra el grupo en la interfaz.</param>
/// <param name="Familia">Si el grupo reúne conceptos o logos de producto.</param>
/// <param name="EnElSelector">Falso para los glifos con los que se dibuja la propia aplicación.</param>
public sealed record GrupoDeIconos(string Clave, string Nombre, FamiliaDeIcono Familia, bool EnElSelector);

/// <summary>Un icono del catálogo, con todo lo que hace falta para buscarlo y para dibujarlo.</summary>
/// <param name="Clave">Nombre del archivo SVG, sin extensión. Es estable y no se traduce.</param>
/// <param name="Grupo">Grupo al que pertenece.</param>
/// <param name="Etiqueta">Cómo se lo nombra en la interfaz.</param>
/// <param name="Sinonimos">Palabras extra con las que el buscador lo encuentra.</param>
/// <param name="Modo">Si se dibuja por trazo o por relleno.</param>
/// <param name="Lienzo">Lado del lienzo cuadrado en el que está dibujado: 24 casi siempre, 128 en Devicon.</param>
/// <param name="EnElSelector">Falso en los glifos con los que se dibuja la propia aplicación.</param>
/// <param name="Origen">Paquete, versión, ruta y licencia de los que salió.</param>
public sealed record IconoDelCatalogo(
    string Clave,
    GrupoDeIconos Grupo,
    string Etiqueta,
    IReadOnlyList<string> Sinonimos,
    ModoDePintado Modo,
    double Lienzo,
    bool EnElSelector,
    OrigenDeIcono Origen)
{
    /// <summary>Clave con la que el diccionario de recursos de WPF guarda su geometría.</summary>
    public string ClaveDeRecurso => "Icono." + Clave;

    public override string ToString() => $"{Clave} ({Etiqueta})";
}
