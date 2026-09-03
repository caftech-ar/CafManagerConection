using System.Runtime.Versioning;
using System.Windows;
using CafManagerConection.App.Views;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Credentials;

namespace CafManagerConection.App.Services;

/// <summary>Lo que pasa con el vault al arrancar: crearlo, abrirlo solo, o pedir la clave maestra. Y traer las credenciales que hayan quedado en el Administrador de Windows.</summary>
[SupportedOSPlatform("windows")]
public sealed class AperturaDelVault
{
    private readonly Vault _vault;
    private readonly ICredentialStore _administradorDeWindows;
    private readonly IAppLogger _logger;
    private readonly Func<Window?> _dueno;

    public AperturaDelVault(
        Vault vault,
        ICredentialStore administradorDeWindows,
        IAppLogger logger,
        Func<Window?> dueno)
    {
        _vault = vault;
        _administradorDeWindows = administradorDeWindows;
        _logger = logger;
        _dueno = dueno;
    }

    /// <summary>Qué contarle al usuario cuando termina, o <c>null</c> si no hay nada que contar.</summary>
    public async Task<string?> AbrirAsync(CancellationToken ct = default)
    {
        return await ComoAbre(ct).ConfigureAwait(true) switch
        {
            UseCases.Credentials.ComoAbre.SinCrear => await CrearAsync(ct).ConfigureAwait(true),
            UseCases.Credentials.ComoAbre.Solo => await AbrirSoloAsync(ct).ConfigureAwait(true),
            _ => await PedirLaClaveAsync(ct).ConfigureAwait(true),
        };
    }

    private Task<ComoAbre> ComoAbre(CancellationToken ct) => _vault.ComoAbreAsync(ct);

    private async Task<string?> CrearAsync(CancellationToken ct)
    {
        var ventana = new ClaveMaestraWindow(ModoDeClaveMaestra.Crear) { Owner = _dueno() };

        var acepto = ventana.ShowDialog() == true;

        if (!acepto && !ventana.SinClaveMaestra)
        {
            return "Sin clave maestra no se pueden usar ni guardar contraseñas. Se vuelve a "
                   + "ofrecer en el próximo arranque.";
        }

        var clave = acepto ? ventana.Clave ?? [] : [];

        try
        {
            // Sin clave maestra hay que recordar el equipo, o el vault no se abriria nunca.
            var recordar = !acepto || ventana.RecordarEsteEquipo || clave.Length == 0;

            await _vault.CrearAsync(clave, recordar, ct).ConfigureAwait(true);
        }
        catch (ArgumentException ex)
        {
            _logger.TechnicalError("crear el vault de credenciales", ex);
            return ex.Message;
        }
        finally
        {
            Array.Clear(clave);
        }

        return await MigrarAsync(ct).ConfigureAwait(true);
    }

    private async Task<string?> AbrirSoloAsync(CancellationToken ct)
    {
        if (await _vault.AbrirSoloAsync(ct).ConfigureAwait(true))
        {
            return await MigrarAsync(ct).ConfigureAwait(true);
        }

        // DPAPI fallo: otro usuario de Windows, otra maquina o el dato tocado. Es el camino
        // normal y no un error, asi que se cae al pedido de la clave maestra sin dramatizar.
        return await PedirLaClaveAsync(ct).ConfigureAwait(true);
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
                return "El vault quedó cerrado. Las conexiones se ven igual; lo que no funciona "
                       + "es usar y guardar contraseñas.";
            }

            try
            {
                if (await _vault.AbrirConLaClaveMaestraAsync(clave, ct).ConfigureAwait(true))
                {
                    if (ventana.RecordarEsteEquipo)
                    {
                        await _vault.DefinirClaveMaestraAsync(clave, true, ct).ConfigureAwait(true);
                    }

                    return await MigrarAsync(ct).ConfigureAwait(true);
                }
            }
            finally
            {
                Array.Clear(clave);
            }
        }

        return "La clave maestra no abrió el vault. Las conexiones se ven igual; lo que no "
               + "funciona es usar y guardar contraseñas.";
    }

    /// <summary>Trae lo que haya quedado en el Administrador de credenciales de Windows. Cuando no hay nada, no dice nada: cero no es un fallo.</summary>
    private async Task<string?> MigrarAsync(CancellationToken ct)
    {
        if (!_vault.EstaAbierto)
        {
            return null;
        }

        try
        {
            var resultado = await new MigradorDeCredenciales(_administradorDeWindows, _vault)
                .MigrarAsync(ct).ConfigureAwait(true);

            return resultado.HuboAlgoQueHacer ? resultado.Resumen() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.TechnicalError("migrar las credenciales del Administrador de Windows", ex);

            return "No se pudieron traer las credenciales del Administrador de Windows. Siguen "
                   + "ahí: no se borró ninguna.";
        }
    }
}
