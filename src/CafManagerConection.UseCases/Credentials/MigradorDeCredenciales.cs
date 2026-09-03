using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Credentials;

public sealed record ResultadoDeMigracion(
    int Traidas, IReadOnlyList<string> QueQuedaron)
{
    public bool HuboAlgoQueHacer => Traidas > 0 || QueQuedaron.Count > 0;

    public string Resumen()
    {
        if (!HuboAlgoQueHacer)
        {
            return "No había credenciales para traer.";
        }

        var texto = Traidas == 1
            ? "Se trajo 1 credencial al vault."
            : $"Se trajeron {Traidas} credenciales al vault.";

        return QueQuedaron.Count == 0
            ? texto
            : texto + $" Quedaron {QueQuedaron.Count} sin traer:"
              + Environment.NewLine
              + string.Join(Environment.NewLine, QueQuedaron.Select(q => $"· {q}"));
    }
}

/// <summary>Trae al vault las credenciales que estaban en el Administrador de credenciales de Windows. Corre una vez, y desde la 0.1.2 no existe más.</summary>
public sealed class MigradorDeCredenciales
{
    public const string Prefijo = "cmc:";

    private readonly ICredentialStore _origen;
    private readonly Vault _vault;

    public MigradorDeCredenciales(ICredentialStore origen, Vault vault)
    {
        _origen = origen;
        _vault = vault;
    }

    public async Task<ResultadoDeMigracion> MigrarAsync(CancellationToken ct = default)
    {
        var claves = await _origen.EnumerateKeysAsync(Prefijo, ct).ConfigureAwait(false);

        var traidas = 0;
        var quedaron = new List<string>();

        foreach (var clave in claves)
        {
            try
            {
                if (await TraerUnaAsync(clave, ct).ConfigureAwait(false))
                {
                    traidas++;
                }
            }
            catch (Exception ex)
            {
                // El motivo va en el resumen. Una lista corta sin explicacion se lee como un
                // fallo de la aplicacion, y lo que hay que saber es cual recargar a mano.
                quedaron.Add($"{clave}: {ex.Message}");
            }
        }

        return new ResultadoDeMigracion(traidas, quedaron);
    }

    private async Task<bool> TraerUnaAsync(string clave, CancellationToken ct)
    {
        using var original = await _origen.ReadAsync(clave, ct).ConfigureAwait(false);

        if (original is null || !original.HasSecret)
        {
            return false;
        }

        // Copia propia: el secreto no puede cruzar un await como Span, y asi se puede pisar.
        var secreto = original.Secret.ToArray();

        try
        {
            await _vault.GuardarCredencialAsync(
                clave, original.UserName, original.Domain, secreto, ct).ConfigureAwait(false);

            // Verificar ANTES de borrar, y no al reves: al reves, un fallo entre las dos
            // operaciones pierde la credencial y no hay de donde recuperarla.
            var vuelta = await _vault.LeerCredencialAsync(clave, ct).ConfigureAwait(false);

            if (vuelta is not { } leida)
            {
                throw new InvalidOperationException(
                    "se guardó en el vault pero no se pudo volver a leer");
            }

            try
            {
                if (!secreto.AsSpan().SequenceEqual(leida.Secreto))
                {
                    throw new InvalidOperationException(
                        "lo que se leyó del vault no coincide con el original");
                }
            }
            finally
            {
                Array.Clear(leida.Secreto);
            }

            await _origen.DeleteAsync(clave, ct).ConfigureAwait(false);
            return true;
        }
        finally
        {
            Array.Clear(secreto);
        }
    }

    /// <summary>Lo que tiene que hacer una versión sin migrador: avisar, no borrar y no seguir en silencio.</summary>
    public static async Task<IReadOnlyList<string>> QuedaronEnElAdministradorAsync(
        ICredentialStore origen, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(origen);

        return await origen.EnumerateKeysAsync(Prefijo, ct).ConfigureAwait(false);
    }
}
