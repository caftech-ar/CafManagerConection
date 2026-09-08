using System.Runtime.Versioning;
using System.Windows;
using CafManagerConection.App.Bootstrap;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Credentials;

namespace CafManagerConection.App.Services;

/// <summary>Lee un secreto guardado descifrándolo del vault, ofreciendo desbloquear si está cerrado. Es el mismo camino que «Copiar contraseña».</summary>
[SupportedOSPlatform("windows")]
public static class LectorDeSecreto
{
    /// <summary>El secreto en claro, o <c>null</c> si no hay ninguno guardado o el usuario no desbloqueó el vault.</summary>
    public static async Task<string?> LeerAsync(
        CompositionRoot root, Window dueno, ReferenciaDeSecreto referencia)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(dueno);

        for (var intento = 0; intento < 2; intento++)
        {
            try
            {
                var secreto = await root.Credentials.ReadAsync(referencia).ConfigureAwait(true);

                if (secreto is null)
                {
                    return null;
                }

                try
                {
                    return new string(secreto);
                }
                finally
                {
                    Array.Clear(secreto);
                }
            }
            catch (VaultCerradoException)
            {
                if (intento > 0)
                {
                    return null;
                }

                await new AperturaDelVault(root.Vault, root.Logger, () => dueno)
                    .AbrirAsync().ConfigureAwait(true);

                if (!root.Vault.EstaAbierto)
                {
                    return null;
                }
            }
        }

        return null;
    }
}
