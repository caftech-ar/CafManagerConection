namespace CafManagerConection.Domain.Settings;

public sealed record ComandoGuardado(Guid Id, string Nombre, string Comando, Guid? Conexion = null)
{
    public bool EsGlobal => Conexion is null;

    public bool EsValido =>
        !string.IsNullOrWhiteSpace(Nombre) && !string.IsNullOrWhiteSpace(Comando);

    public bool AplicaA(Guid conexion) => Conexion is null || Conexion == conexion;

    public ComandoGuardado Normalizado() => this with
    {
        Nombre = (Nombre ?? string.Empty).Trim(),
        Comando = (Comando ?? string.Empty).Trim(),
    };
}

public sealed class PaletaDeComandos
{
    private readonly List<ComandoGuardado> _comandos;

    public PaletaDeComandos(IEnumerable<ComandoGuardado>? comandos = null) =>
        _comandos = [.. (comandos ?? []).Where(c => c.EsValido)];

    public IReadOnlyList<ComandoGuardado> Todos => _comandos;

    public int Cantidad => _comandos.Count;

    /// <summary>Los aplicables a la conexión, filtrados por texto; los propios antes que los globales.</summary>
    public IReadOnlyList<ComandoGuardado> Visibles(Guid? conexion, string? filtro = null)
    {
        var aguja = (filtro ?? string.Empty).Trim();

        var aplicables = conexion is { } id
            ? _comandos.Where(c => c.AplicaA(id))
            : _comandos.Where(c => c.EsGlobal);

        if (aguja.Length > 0)
        {
            aplicables = aplicables.Where(c => Coincide(c, aguja));
        }

        return
        [
            .. aplicables
                .OrderBy(c => c.EsGlobal)
                .ThenBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    private static bool Coincide(ComandoGuardado c, string aguja) =>
        c.Nombre.Contains(aguja, StringComparison.CurrentCultureIgnoreCase)
        || c.Comando.Contains(aguja, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Agrega uno y lo devuelve con su identidad; <c>null</c> si no es válido.</summary>
    public ComandoGuardado? Agregar(string nombre, string comando, Guid? conexion = null)
    {
        var nuevo = new ComandoGuardado(Guid.NewGuid(), nombre, comando, conexion).Normalizado();

        if (!nuevo.EsValido)
        {
            return null;
        }

        _comandos.Add(nuevo);
        return nuevo;
    }

    /// <summary>Reemplaza uno existente. <c>false</c> si no está o si quedaría inválido.</summary>
    public bool Actualizar(ComandoGuardado comando)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var normalizado = comando.Normalizado();
        var indice = _comandos.FindIndex(c => c.Id == normalizado.Id);

        if (indice < 0 || !normalizado.EsValido)
        {
            return false;
        }

        _comandos[indice] = normalizado;
        return true;
    }

    public bool Quitar(Guid id) => _comandos.RemoveAll(c => c.Id == id) > 0;
}
