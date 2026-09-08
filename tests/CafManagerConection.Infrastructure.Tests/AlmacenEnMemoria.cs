using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Tests;

/// <summary>Almacén de secretos en memoria para las pruebas que necesitan uno pero no lo están probando.</summary>
public sealed class AlmacenEnMemoria : ICredentialStore
{
    private readonly Dictionary<ReferenciaDeSecreto, string> _filas = [];

    public Task<char[]?> ReadAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default) =>
        Task.FromResult(_filas.TryGetValue(referencia, out var secreto)
            ? secreto.ToCharArray()
            : null);

    public Task WriteAsync(
        ReferenciaDeSecreto referencia,
        ReadOnlyMemory<char> secreto,
        CancellationToken ct = default)
    {
        _filas[referencia] = new string(secreto.Span);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ReferenciaDeSecreto referencia, CancellationToken ct = default)
    {
        _filas.Remove(referencia);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default) =>
        Task.FromResult(_filas.ContainsKey(referencia));

    public Task<IReadOnlyList<ReferenciaDeSecreto>> EnumerateAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ReferenciaDeSecreto>>([.. _filas.Keys]);
}
