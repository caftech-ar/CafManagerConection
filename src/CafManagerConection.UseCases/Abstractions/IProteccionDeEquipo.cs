namespace CafManagerConection.UseCases.Abstractions;

/// <summary>Ata un dato a este usuario de Windows y a este equipo. Es lo que permite abrir el vault sin preguntar nada, y es opcional.</summary>
public interface IProteccionDeEquipo
{
    byte[] Proteger(ReadOnlySpan<byte> claro);

    /// <summary>Lanza cuando el dato es de otro usuario, de otra máquina o está tocado. Ese fallo es el camino normal para caer al pedido de la clave maestra, no un error del programa.</summary>
    byte[] Desproteger(ReadOnlySpan<byte> envuelto);
}
