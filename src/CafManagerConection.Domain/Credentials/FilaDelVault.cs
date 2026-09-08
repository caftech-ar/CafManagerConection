using System.Security.Cryptography;

namespace CafManagerConection.Domain.Credentials;

/// <summary>Lo que la base guarda de la clave maestra. Que esta fila exista es lo que dice que hay clave maestra; que no exista es lo que dice que los secretos están en claro. Nada de esto es secreto salvo el verificador, y sin la sal y las iteraciones los secretos no se descifran nunca más.</summary>
public sealed record FilaDelVault(
    int Formato,
    string KdfHash,
    byte[] KdfSal,
    int KdfIteraciones,
    byte[] VerificadorNonce,
    byte[] Verificador)
{
    public const int FormatoActual = 3;

    /// <summary>Lo que el verificador contiene una vez descifrado. Fijo y público: sólo sirve para saber si la clave derivada es la correcta.</summary>
    public const string TextoDelVerificador = "CafManagerConection.Vault";

    public SobreCifrado SobreDelVerificador => new(VerificadorNonce, Verificador);

    public HashAlgorithmName Hash => new(KdfHash);

    /// <summary>Devuelve el motivo por el que esta fila no se puede usar, o <c>null</c> si está sana. Se comprueba antes de derivar: una base con las iteraciones bajadas a mano abriría en microsegundos.</summary>
    public string? PorQueNoSeUsa(int formatoConocido, int iteracionesMinimas)
    {
        if (Formato > formatoConocido)
        {
            return $"El vault está en formato {Formato} y esta versión conoce hasta "
                   + $"{formatoConocido}. Actualizá CMC antes de abrirlo.";
        }

        if (KdfIteraciones < iteracionesMinimas)
        {
            return $"El vault dice tener {KdfIteraciones:N0} iteraciones de derivación y el mínimo "
                   + $"es {iteracionesMinimas:N0}. La base fue modificada fuera de la aplicación.";
        }

        return CifradoDeSecretos.HashesAdmitidos.Contains(KdfHash, StringComparer.Ordinal)
            ? null
            : $"El vault dice derivar con «{KdfHash}», que esta versión no conoce.";
    }
}
