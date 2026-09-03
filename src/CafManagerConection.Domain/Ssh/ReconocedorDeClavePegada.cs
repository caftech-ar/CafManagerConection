using System.Text;

namespace CafManagerConection.Domain.Ssh;

public static class ReconocedorDeClavePegada
{
    private const string MotivoVacio = "No se pegó ningún texto.";

    private const string MotivoNoReconocido =
        "El texto no coincide con ninguno de los formatos reconocidos: un archivo .ppk de " +
        "PuTTY (empieza con «PuTTY-User-Key-File-2:» o «-3:»), una clave privada OpenSSH " +
        "(delimitada por «-----BEGIN OPENSSH PRIVATE KEY-----»), un PEM clásico RSA, EC o DSA " +
        "(delimitado por «-----BEGIN RSA/EC/DSA PRIVATE KEY-----»), o una clave pública " +
        "(empieza con «ssh-rsa», «ssh-ed25519» y similares).";

    private const string MotivoPpkIncompleto =
        "Se reconoce el encabezado de un archivo .ppk de PuTTY, pero faltan los campos " +
        "(Encryption, Comment o Public-Lines) que hacen falta para leerlo: el archivo parece " +
        "estar incompleto o dañado.";

    private const string NotaOpenSshSinCierre =
        "Falta el delimitador de cierre «-----END OPENSSH PRIVATE KEY-----»: el texto pegado " +
        "parece estar incompleto.";

    private const string NotaOpenSshBase64Invalido =
        "El bloque de texto entre los delimitadores no es base64 válido: el archivo parece " +
        "estar dañado.";

    private const string NotaOpenSshEncabezadoInvalido =
        "No se pudo interpretar el encabezado del contenedor openssh-key-v1.";

    private const string NotaOpenSshSinClaves =
        "El contenedor no trae ninguna clave.";

    private const string NotaPemSinPublica =
        "El PEM clásico no guarda la clave pública por separado de la privada: haría falta " +
        "descifrarla para derivarla, y esta pantalla no descifra nada.";

    private const string NotaPublicaBase64Invalido =
        "La clave pública no se pudo decodificar para calcular la huella.";

    private static readonly (string Marcador, string Algoritmo)[] MarcadoresPem =
    [
        ("-----BEGIN RSA PRIVATE KEY-----", "RSA"),
        ("-----BEGIN EC PRIVATE KEY-----", "EC"),
        ("-----BEGIN DSA PRIVATE KEY-----", "DSA"),
    ];

    private static readonly string[] AlgoritmosPublicos =
    [
        "ssh-rsa", "ssh-ed25519", "ssh-dss",
        "ecdsa-sha2-nistp256", "ecdsa-sha2-nistp384", "ecdsa-sha2-nistp521",
        "sk-ssh-ed25519@openssh.com", "sk-ecdsa-sha2-nistp256@openssh.com",
    ];

    public static ReconocimientoClavePegada Reconocer(string? textoPegado)
    {
        if (string.IsNullOrWhiteSpace(textoPegado))
        {
            return new ReconocimientoClavePegada
            {
                Formato = FormatoClavePegada.Desconocido,
                Motivo = MotivoVacio,
            };
        }

        var texto = textoPegado.Trim();

        if (texto.StartsWith("PuTTY-User-Key-File-2:", StringComparison.Ordinal)
            || texto.StartsWith("PuTTY-User-Key-File-3:", StringComparison.Ordinal))
        {
            return ReconocerPpk(texto);
        }

        if (texto.Contains("-----BEGIN OPENSSH PRIVATE KEY-----", StringComparison.Ordinal))
        {
            return ReconocerOpenSshPrivada(texto);
        }

        foreach (var (marcador, algoritmo) in MarcadoresPem)
        {
            if (texto.Contains(marcador, StringComparison.Ordinal))
            {
                return ReconocerPemClasico(texto, algoritmo);
            }
        }

        if (ReconocerClavePublica(texto) is { } publica)
        {
            return publica;
        }

        return new ReconocimientoClavePegada
        {
            Formato = FormatoClavePegada.Desconocido,
            Motivo = MotivoNoReconocido,
        };
    }

    private static ReconocimientoClavePegada ReconocerPpk(string texto)
    {
        var lineas = texto.Replace("\r\n", "\n").Split('\n');

        var dosPuntos = lineas[0].IndexOf(':');
        var algoritmo = dosPuntos >= 0 ? lineas[0][(dosPuntos + 1)..].Trim() : string.Empty;

        string? cifrado = null;
        string? comentario = null;
        int? cantidadLineas = null;
        var indicePublicLines = -1;

        for (var i = 1; i < lineas.Length; i++)
        {
            var linea = lineas[i];

            if (linea.StartsWith("Encryption:", StringComparison.Ordinal))
            {
                cifrado = linea["Encryption:".Length..].Trim();
            }
            else if (linea.StartsWith("Comment:", StringComparison.Ordinal))
            {
                comentario = linea["Comment:".Length..].Trim();
            }
            else if (linea.StartsWith("Public-Lines:", StringComparison.Ordinal))
            {
                if (int.TryParse(linea["Public-Lines:".Length..].Trim(), out var n))
                {
                    cantidadLineas = n;
                    indicePublicLines = i;
                }

                break;
            }
        }

        if (algoritmo.Length == 0 || cifrado is null || comentario is null
            || cantidadLineas is null || indicePublicLines < 0)
        {
            return new ReconocimientoClavePegada
            {
                Formato = FormatoClavePegada.Desconocido,
                Motivo = MotivoPpkIncompleto,
            };
        }

        var esCifrada = !string.Equals(cifrado, "none", StringComparison.OrdinalIgnoreCase);

        var bloque = new StringBuilder();
        var ultimaLinea = Math.Min(indicePublicLines + cantidadLineas.Value, lineas.Length - 1);

        for (var i = indicePublicLines + 1; i <= ultimaLinea; i++)
        {
            bloque.Append(lineas[i].Trim());
        }

        HuellaClavePublica? huella = null;
        string? notaHuella = null;

        try
        {
            var blob = Convert.FromBase64String(bloque.ToString());

            var lineaPublica = comentario.Length > 0
                ? $"{algoritmo} {bloque} {comentario}"
                : $"{algoritmo} {bloque}";

            huella = new HuellaClavePublica(HuellaSsh.CalcularSha256(blob), lineaPublica);
        }
        catch (FormatException)
        {
            notaHuella = MotivoPpkIncompleto;
        }

        return new ReconocimientoClavePegada
        {
            Formato = FormatoClavePegada.PpkPutty,
            Cifrada = esCifrada,
            Algoritmo = algoritmo,
            Comentario = comentario.Length > 0 ? comentario : null,
            Huella = huella,
            NotaHuella = notaHuella,
        };
    }

    private static ReconocimientoClavePegada ReconocerOpenSshPrivada(string texto)
    {
        const string inicioMarcador = "-----BEGIN OPENSSH PRIVATE KEY-----";
        const string finMarcador = "-----END OPENSSH PRIVATE KEY-----";

        var inicio = texto.IndexOf(inicioMarcador, StringComparison.Ordinal);
        var fin = texto.IndexOf(finMarcador, StringComparison.Ordinal);

        if (fin < 0 || fin < inicio)
        {
            return new ReconocimientoClavePegada
            {
                Formato = FormatoClavePegada.OpenSshPrivada,
                NotaHuella = NotaOpenSshSinCierre,
            };
        }

        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(texto[(inicio + inicioMarcador.Length)..fin]);
        }
        catch (FormatException)
        {
            return new ReconocimientoClavePegada
            {
                Formato = FormatoClavePegada.OpenSshPrivada,
                NotaHuella = NotaOpenSshBase64Invalido,
            };
        }

        if (LeerEncabezadoOpenSsh(bytes) is not var (cifrada, algoritmo, blobPublico))
        {
            return new ReconocimientoClavePegada
            {
                Formato = FormatoClavePegada.OpenSshPrivada,
                NotaHuella = NotaOpenSshEncabezadoInvalido,
            };
        }

        if (blobPublico is null)
        {
            return new ReconocimientoClavePegada
            {
                Formato = FormatoClavePegada.OpenSshPrivada,
                Cifrada = cifrada,
                NotaHuella = NotaOpenSshSinClaves,
            };
        }

        var lineaPublica = $"{algoritmo} {Convert.ToBase64String(blobPublico)}";
        var huella = new HuellaClavePublica(HuellaSsh.CalcularSha256(blobPublico), lineaPublica);

        return new ReconocimientoClavePegada
        {
            Formato = FormatoClavePegada.OpenSshPrivada,
            Cifrada = cifrada,
            Algoritmo = algoritmo,
            Huella = huella,
        };
    }

    private static (bool Cifrada, string? Algoritmo, byte[]? BlobPublico)? LeerEncabezadoOpenSsh(
        byte[] bytes)
    {
        var magico = Encoding.ASCII.GetBytes("openssh-key-v1\0");

        if (bytes.Length < magico.Length || !bytes.AsSpan(0, magico.Length).SequenceEqual(magico))
        {
            return null;
        }

        var posicion = magico.Length;

        if (LeerCadena(bytes, ref posicion) is not { } cifrado)
        {
            return null;
        }

        if (LeerCadena(bytes, ref posicion) is null)
        {
            return null;
        }

        if (LeerBytes(bytes, ref posicion) is null)
        {
            return null;
        }

        var estaCifrada = !string.Equals(cifrado, "none", StringComparison.Ordinal);

        if (LeerEntero(bytes, ref posicion) is not { } cantidadClaves || cantidadClaves < 1)
        {
            return (estaCifrada, null, null);
        }

        if (LeerBytes(bytes, ref posicion) is not { } blobPublico)
        {
            return (estaCifrada, null, null);
        }

        var posicionAlgoritmo = 0;
        var algoritmo = LeerCadena(blobPublico, ref posicionAlgoritmo);

        return (estaCifrada, algoritmo, blobPublico);
    }

    private static int? LeerEntero(byte[] bytes, ref int posicion)
    {
        if (posicion + 4 > bytes.Length)
        {
            return null;
        }

        var valor = (bytes[posicion] << 24) | (bytes[posicion + 1] << 16)
            | (bytes[posicion + 2] << 8) | bytes[posicion + 3];

        posicion += 4;
        return valor;
    }

    private static byte[]? LeerBytes(byte[] bytes, ref int posicion)
    {
        if (LeerEntero(bytes, ref posicion) is not { } longitud
            || longitud < 0 || posicion + longitud > bytes.Length)
        {
            return null;
        }

        var valor = bytes[posicion..(posicion + longitud)];
        posicion += longitud;
        return valor;
    }

    private static string? LeerCadena(byte[] bytes, ref int posicion) =>
        LeerBytes(bytes, ref posicion) is { } valor ? Encoding.ASCII.GetString(valor) : null;

    private static ReconocimientoClavePegada ReconocerPemClasico(string texto, string algoritmo) =>
        new()
        {
            Formato = FormatoClavePegada.PemClasica,
            Algoritmo = algoritmo,
            Cifrada = texto.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal),
            NotaHuella = NotaPemSinPublica,
        };

    private static ReconocimientoClavePegada? ReconocerClavePublica(string texto)
    {
        var primeraLinea = texto
            .Split('\n')
            .Select(l => l.Trim('\r', ' ', '\t'))
            .FirstOrDefault(l => l.Length > 0);

        if (primeraLinea is null)
        {
            return null;
        }

        var partes = primeraLinea.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        if (partes.Length < 2 || !AlgoritmosPublicos.Contains(partes[0], StringComparer.Ordinal))
        {
            return null;
        }

        var algoritmo = partes[0];
        var comentario = partes.Length > 2 ? partes[2] : null;

        HuellaClavePublica? huella = null;
        string? notaHuella = NotaPublicaBase64Invalido;

        try
        {
            var blob = Convert.FromBase64String(partes[1]);
            huella = new HuellaClavePublica(HuellaSsh.CalcularSha256(blob), primeraLinea);
            notaHuella = null;
        }
        catch (FormatException)
        {
        }

        return new ReconocimientoClavePegada
        {
            Formato = FormatoClavePegada.ClavePublica,
            Algoritmo = algoritmo,
            Comentario = comentario,
            Huella = huella,
            NotaHuella = notaHuella,
        };
    }
}
