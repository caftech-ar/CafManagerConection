using System.Runtime.Versioning;
using System.Windows;
using CafManagerConection.App.Views;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Credentials;

namespace CafManagerConection.App.Services;

/// <summary>Lo que pasa con los secretos al arrancar: sin clave maestra no pasa nada, y con clave maestra se pide.</summary>
[SupportedOSPlatform("windows")]
public sealed class AperturaDelVault
{
    private readonly Vault _vault;
    private readonly IAppLogger _logger;
    private readonly Func<Window?> _dueno;

    public AperturaDelVault(Vault vault, IAppLogger logger, Func<Window?> dueno)
    {
        _vault = vault;
        _logger = logger;
        _dueno = dueno;
    }

    /// <summary>Qué contarle al usuario cuando termina, o <c>null</c> si no hay nada que contar.</summary>
    public async Task<string?> AbrirAsync(CancellationToken ct = default)
    {
        return await _vault.ComoAbreAsync(ct).ConfigureAwait(true) switch
        {
            ComoAbre.SinClaveMaestra => null,
            _ => await PedirLaClaveAsync(ct).ConfigureAwait(true),
        };
    }

    private async Task<string?> PedirLaClaveAsync(CancellationToken ct)
    {
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var ventana = new ClaveMaestraWindow(ModoDeClaveMaestra.Desbloquear)
            {
                Owner = _dueno(),
            };

            if (ventana.ShowDialog() != true || ventana.Clave is not { } clave)
            {
                return "Las contraseñas quedaron bloqueadas. Las conexiones se ven igual; lo que "
                       + "no funciona es usarlas y guardarlas.";
            }

            try
            {
                var resultado = await _vault.AbrirConLaClaveMaestraAsync(clave, ct)
                    .ConfigureAwait(true);

                if (resultado == ResultadoDeApertura.NoTieneClaveMaestra)
                {
                    // La fila desaparecio entre el arranque y ahora. No hay clave que tipear, asi
                    // que preguntarla tres veces seria mandar a adivinar algo inexistente.
                    return null;
                }

                if (resultado == ResultadoDeApertura.Abierto)
                {
                    return null;
                }
            }
            catch (VaultDanadoException ex)
            {
                _logger.TechnicalError("abrir las contraseñas guardadas", ex);
                return ex.Message;
            }
            finally
            {
                Array.Clear(clave);
            }
        }

        return "La clave maestra no abrió las contraseñas guardadas. Las conexiones se ven igual; "
               + "lo que no funciona es usarlas y guardarlas.";
    }
}
