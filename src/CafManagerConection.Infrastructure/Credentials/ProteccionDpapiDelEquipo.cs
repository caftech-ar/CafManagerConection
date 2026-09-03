using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Credentials;

/// <summary>Ata la clave del vault a este usuario de Windows con DPAPI.</summary>
public sealed class ProteccionDpapiDelEquipo : IProteccionDeEquipo
{
    public byte[] Proteger(ReadOnlySpan<byte> claro) => ProteccionDpapi.Proteger(claro);

    public byte[] Desproteger(ReadOnlySpan<byte> envuelto) =>
        ProteccionDpapi.Desproteger(envuelto);
}
