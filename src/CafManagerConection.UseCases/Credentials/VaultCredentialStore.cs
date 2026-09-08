using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Credentials;

/// <summary>El puerto de secretos sobre el vault. No decide nada: cifrar o no lo decide el vault según haya clave maestra.</summary>
public sealed class VaultCredentialStore : ICredentialStore
{
    private readonly Vault _vault;

    public VaultCredentialStore(Vault vault) => _vault = vault;

    public Task<char[]?> ReadAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default) =>
        _vault.LeerSecretoAsync(referencia, ct);

    public Task WriteAsync(
        ReferenciaDeSecreto referencia,
        ReadOnlyMemory<char> secreto,
        CancellationToken ct = default) =>
        _vault.GuardarSecretoAsync(referencia, secreto, ct);

    public Task DeleteAsync(ReferenciaDeSecreto referencia, CancellationToken ct = default) =>
        _vault.BorrarSecretoAsync(referencia, ct);

    public Task<bool> ExistsAsync(ReferenciaDeSecreto referencia, CancellationToken ct = default) =>
        _vault.HaySecretoAsync(referencia, ct);

    public Task<IReadOnlyList<ReferenciaDeSecreto>> EnumerateAsync(
        CancellationToken ct = default) =>
        _vault.ReferenciasConSecretoAsync(ct);
}
