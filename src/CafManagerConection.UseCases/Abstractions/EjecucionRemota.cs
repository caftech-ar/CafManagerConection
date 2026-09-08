namespace CafManagerConection.UseCases.Abstractions;

// RunAsync se declara una sola vez y la hereda quien agrega privilegios. Estaba repetida en dos
// proyectos con la misma firma, y eso obligaba a Ssh a referenciar a Platform y a dos adaptadores
// de App a escribir el mismo cuerpo dos veces.
public interface IEjecutorRemoto
{
    Task<(bool Success, string Output, string Error)> RunAsync(
        string command, int timeoutSeconds, CancellationToken ct = default);
}

/// <summary>Un ejecutor que además sabe reintentar con <c>sudo</c>. Sólo para lecturas.</summary>
public interface IEjecutorConPrivilegios : IEjecutorRemoto
{
    Task<(bool Success, string Output, string Error)> RunWithSudoAsync(
        string command, int timeoutSeconds, CancellationToken ct = default);
}

/// <summary>Canal propio para comandos que no terminan y van entregando líneas, como <c>docker logs -f</c> o <c>tail -F</c>.</summary>
public interface ISeguidorDeRegistro
{
    /// <summary>Entrega cada línea a <paramref name="onLinea"/> sin esperar a que el comando termine.</summary>
    /// <returns>Lo que hay que desechar para cerrar el canal y la conexión que lo sostiene.</returns>
    Task<IAsyncDisposable> SeguirAsync(
        string command,
        Action<string> onLinea,
        Action<string?> onCerrado,
        CancellationToken ct = default);
}
