using CafManagerConection.Domain.Ssh;

namespace CafManagerConection.Domain.Tests.Ssh;

// Cubre los cinco formatos que el diálogo de "Pegar clave privada" tiene que distinguir. Todo el
// material es sintético, generado con ssh-keygen y puttygen fuera del repositorio; las huellas
// esperadas se verificaron contra la salida de "ssh-keygen -lf" sobre esas mismas claves.
public sealed class ReconocedorDeClavePegadaTests
{
    private const string PpkSinCifrar =
        """
        PuTTY-User-Key-File-2: ssh-rsa
        Encryption: none
        Comment: prueba-ppk
        Public-Lines: 6
        AAAAB3NzaC1yc2EAAAADAQABAAABAQDZjfu556WVSIPBajpFBbs7b8t+KB+lqey7
        hGIoO9VyiOQXEXGpbjAjp5PKgn5vcOeColMV1VtTzC5VxgE3DsmnM1gTaficOJv0
        q6EUP7JMt35Xt6cGxzVrw4KoMUWZ0RFBpjyNnljsCjKJqzVl1YJj4Rb9SIr7EkGA
        ICaDEG7wAOf//j80VAF09ogS7cf3OobffKeexGbjOcIwH/i4seYBwIwWYIYD3E6x
        Foc7uX12c1P37Ph/4JzHJ33GfV4EBguKgrHLcmKJhKTMEQkTWqQqUoDH2Af8AuGL
        9HyD6Bv9swTMi1TWrnTnrQP1hV8SKBUrOgbNHsnQrd98UvjrBJX7
        Private-Lines: 1
        AA==
        Private-MAC: 0000000000000000000000000000000000000000
        """;

    private const string PpkCifradoV3 =
        """
        PuTTY-User-Key-File-3: ssh-rsa
        Encryption: aes256-cbc
        Comment: prueba-ppk-cifrada
        Public-Lines: 6
        AAAAB3NzaC1yc2EAAAADAQABAAABAQDZjfu556WVSIPBajpFBbs7b8t+KB+lqey7
        hGIoO9VyiOQXEXGpbjAjp5PKgn5vcOeColMV1VtTzC5VxgE3DsmnM1gTaficOJv0
        q6EUP7JMt35Xt6cGxzVrw4KoMUWZ0RFBpjyNnljsCjKJqzVl1YJj4Rb9SIr7EkGA
        ICaDEG7wAOf//j80VAF09ogS7cf3OobffKeexGbjOcIwH/i4seYBwIwWYIYD3E6x
        Foc7uX12c1P37Ph/4JzHJ33GfV4EBguKgrHLcmKJhKTMEQkTWqQqUoDH2Af8AuGL
        9HyD6Bv9swTMi1TWrnTnrQP1hV8SKBUrOgbNHsnQrd98UvjrBJX7
        Key-Derivation: Argon2id
        Argon2-Memory: 8192
        Argon2-Passes: 21
        Argon2-Parallelism: 1
        Argon2-Salt: 0000000000000000
        Private-Lines: 1
        AA==
        Private-MAC: 0000000000000000000000000000000000000000
        """;

    private const string HuellaEsperadaPpk = "SHA256:9G01FtNGYy1oYTeG/NlyE6RlJWV3MiXz+6McKJiQx5A";

    [Fact]
    public void Reconoce_un_ppk_sin_cifrar_y_calcula_su_huella()
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(PpkSinCifrar);

        Assert.Equal(FormatoClavePegada.PpkPutty, resultado.Formato);
        Assert.True(resultado.EsReconocida);
        Assert.Equal(false, resultado.Cifrada);
        Assert.Equal("ssh-rsa", resultado.Algoritmo);
        Assert.Equal("prueba-ppk", resultado.Comentario);
        Assert.NotNull(resultado.Huella);
        Assert.Equal(HuellaEsperadaPpk, resultado.Huella!.Sha256);
        Assert.StartsWith("ssh-rsa ", resultado.Huella.LineaPublica, StringComparison.Ordinal);
        Assert.EndsWith(" prueba-ppk", resultado.Huella.LineaPublica, StringComparison.Ordinal);
    }

    [Fact]
    public void Reconoce_un_ppk_v3_cifrado_sin_descifrarlo()
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(PpkCifradoV3);

        Assert.Equal(FormatoClavePegada.PpkPutty, resultado.Formato);
        Assert.Equal(true, resultado.Cifrada);
        Assert.Equal("prueba-ppk-cifrada", resultado.Comentario);

        // La parte privada es basura a propósito: si el reconocedor la tocara para calcular la
        // huella, fallaría con una excepción en vez de devolver un resultado.
        Assert.NotNull(resultado.Huella);
        Assert.Equal(HuellaEsperadaPpk, resultado.Huella!.Sha256);
    }

    [Fact]
    public void Un_ppk_al_que_le_falta_Public_Lines_se_informa_como_no_reconocido()
    {
        const string ppkRoto =
            """
            PuTTY-User-Key-File-2: ssh-rsa
            Encryption: none
            Comment: prueba-rota
            """;

        var resultado = ReconocedorDeClavePegada.Reconocer(ppkRoto);

        Assert.Equal(FormatoClavePegada.Desconocido, resultado.Formato);
        Assert.False(resultado.EsReconocida);
        Assert.NotNull(resultado.Motivo);
    }

    private const string OpenSshSinCifrar =
        """
        -----BEGIN OPENSSH PRIVATE KEY-----
        b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
        QyNTUxOQAAACBOJj6KqMyU+b0Hlkg0/JgaIW4OVtkFfZ2nHGLEcJoHnAAAAJiw0kT6sNJE
        +gAAAAtzc2gtZWQyNTUxOQAAACBOJj6KqMyU+b0Hlkg0/JgaIW4OVtkFfZ2nHGLEcJoHnA
        AAAEAIcBGVZ2oFID1o4LL/Z4B4bGjGd9mk7ImUNq6gFHbRAU4mPoqozJT5vQeWSDT8mBoh
        bg5W2QV9naccYsRwmgecAAAADnBydWViYS1lZDI1NTE5AQIDBAUGBw==
        -----END OPENSSH PRIVATE KEY-----
        """;

    private const string OpenSshCifrada =
        """
        -----BEGIN OPENSSH PRIVATE KEY-----
        b3BlbnNzaC1rZXktdjEAAAAACmFlczI1Ni1jdHIAAAAGYmNyeXB0AAAAGAAAABBDzKpy78
        GiPlBoQF3Y3ytbAAAAGAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIMbaNdDjpM6gisXp
        bfLjLlTFjjOXWdAdrSSEWhFPmBY0AAAAoE2ycPwddS5ZqkkSMUxIyaE9SyBdTvDaX7m7xZ
        7kigh2Kj+yAr1vAXo36/boQOxF4oUVqDeZOkgCf8OVrWvNqp7whQTp50eTTvqYeZglqUp8
        9j7VlUurwDp0AyD6/1WhHYX8cISlv2r0Egem6AdoP6+v3i5MVl0nQfqT/+LihwDKYU3Kqo
        4xcYDurRzpwTBf2CjSTLx38NCsF6mfH1aKeDw=
        -----END OPENSSH PRIVATE KEY-----
        """;

    private const string HuellaEsperadaEd25519SinCifrar =
        "SHA256:Ceb3Td4YTqUXha3IiKhELCcDGrsgZpmx7F9vCvRmB/s";

    private const string HuellaEsperadaEd25519Cifrada =
        "SHA256:QmhkyHcOAJ6cTIl1iTJ/Si7r3z80bzGsamUYSIWhj+k";

    [Fact]
    public void Reconoce_una_clave_openssh_sin_cifrar_y_calcula_su_huella()
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(OpenSshSinCifrar);

        Assert.Equal(FormatoClavePegada.OpenSshPrivada, resultado.Formato);
        Assert.Equal(false, resultado.Cifrada);
        Assert.Equal("ssh-ed25519", resultado.Algoritmo);
        Assert.NotNull(resultado.Huella);
        Assert.Equal(HuellaEsperadaEd25519SinCifrar, resultado.Huella!.Sha256);
    }

    [Fact]
    public void La_clave_publica_de_una_openssh_cifrada_se_lee_sin_pedir_la_frase()
    {
        // La clave pública no está del lado cifrado del contenedor.
        var resultado = ReconocedorDeClavePegada.Reconocer(OpenSshCifrada);

        Assert.Equal(FormatoClavePegada.OpenSshPrivada, resultado.Formato);
        Assert.Equal(true, resultado.Cifrada);
        Assert.Equal("ssh-ed25519", resultado.Algoritmo);
        Assert.NotNull(resultado.Huella);
        Assert.Equal(HuellaEsperadaEd25519Cifrada, resultado.Huella!.Sha256);
    }

    [Fact]
    public void Una_openssh_sin_delimitador_de_cierre_no_calcula_huella()
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(
            "-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaA==");

        Assert.Equal(FormatoClavePegada.OpenSshPrivada, resultado.Formato);
        Assert.Null(resultado.Huella);
        Assert.NotNull(resultado.NotaHuella);
    }

    private const string PemRsaClasico =
        """
        -----BEGIN RSA PRIVATE KEY-----
        MIIEogIBAAKCAQEAyZx6kQn1v2TsFiXLQwB0Wm8T6FEGDg68wB8LqxR/BGmD9Xzc
        ooV0hglufNqk+9rVpxT4QqL2GlPhYOR0WHkJFACuxRSl8DKsK34jLraBfscT1jt0
        jfRXVib7Az7uBESAqtL4YMAjc0pLNqiRkhxW5KMlw2mNWFIvH79bpqdYbZrwj0DX
        Fmr/9VY8u/pciFWakp7M6DdHE9i93x0So7+C+GSBTF2iQkIsFkwYUQZPmDZNghnE
        BuPH2DPCiS3yr4Ktc5ccPlhCjb33cVbmAM6V9nMFjSKn5Ncu2o1rZk3Q8P9oHJdY
        kq2OgbWvuqU0WE9IFGjx3JcHRz+vSmWevXMICwIDAQABAoIBACrrpSw7cpXMZnZQ
        -----END RSA PRIVATE KEY-----
        """;

    private const string PemEcClasico =
        """
        -----BEGIN EC PRIVATE KEY-----
        MHcCAQEEILFf+D7ytAWFR8mxiBpqhRr4436IHIvtXWrCGJlge1vYoAoGCCqGSM49
        AwEHoUQDQgAEjdhdp4Frnc52I+vE/uT5uakJ/XPMtaS8cuRNGOWscjp95E7N+NQu
        7iMF4BcInuKHMrXcvR+UhGKPJC9E2gly9A==
        -----END EC PRIVATE KEY-----
        """;

    // No se generó con ssh-keygen porque las versiones recientes de OpenSSH ya no saben crear
    // claves DSA; el contenido no es una clave válida, sólo el encabezado que mira el reconocedor.
    private const string PemDsaClasico =
        """
        -----BEGIN DSA PRIVATE KEY-----
        MIIBuwIBAAKBgQDIzAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
        -----END DSA PRIVATE KEY-----
        """;

    [Theory]
    [InlineData("RSA")]
    [InlineData("EC")]
    [InlineData("DSA")]
    public void Reconoce_los_tres_algoritmos_de_pem_clasico_sin_calcular_huella(string algoritmo)
    {
        var texto = algoritmo switch
        {
            "RSA" => PemRsaClasico,
            "EC" => PemEcClasico,
            _ => PemDsaClasico,
        };

        var resultado = ReconocedorDeClavePegada.Reconocer(texto);

        Assert.Equal(FormatoClavePegada.PemClasica, resultado.Formato);
        Assert.True(resultado.EsReconocida);
        Assert.Equal(algoritmo, resultado.Algoritmo);
        Assert.Equal(false, resultado.Cifrada);
        Assert.Null(resultado.Huella);
        Assert.NotNull(resultado.NotaHuella);
    }

    [Fact]
    public void Un_pem_con_Proc_Type_encriptado_se_marca_cifrado()
    {
        var texto =
            "-----BEGIN RSA PRIVATE KEY-----\n"
            + "Proc-Type: 4,ENCRYPTED\n"
            + "DEK-Info: AES-128-CBC,0000000000000000000000000000000\n\n"
            + "MIIEogIBAAKCAQEAyZx6kQn1v2TsFiXLQwB0Wm8T6FEGDg68wB8LqxR/BGmD9Xzc\n"
            + "-----END RSA PRIVATE KEY-----";

        var resultado = ReconocedorDeClavePegada.Reconocer(texto);

        Assert.Equal(FormatoClavePegada.PemClasica, resultado.Formato);
        Assert.Equal(true, resultado.Cifrada);
        Assert.Null(resultado.Huella);
    }

    [Theory]
    [InlineData(
        "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIE4mPoqozJT5vQeWSDT8mBohbg5W2QV9naccYsRwmgec "
            + "prueba-ed25519",
        "ssh-ed25519",
        "prueba-ed25519")]
    [InlineData(
        "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDZjfu556WVSIPBajpFBbs7b8t+KB+lqey7hGIoO9VyiOQXEXGp"
            + "bjAjp5PKgn5vcOeColMV1VtTzC5VxgE3DsmnM1gTaficOJv0q6EUP7JMt35Xt6cGxzVrw4KoMUWZ0RFBpjyN"
            + "nljsCjKJqzVl1YJj4Rb9SIr7EkGAICaDEG7wAOf//j80VAF09ogS7cf3OobffKeexGbjOcIwH/i4seYBwIwW"
            + "YIYD3E6xFoc7uX12c1P37Ph/4JzHJ33GfV4EBguKgrHLcmKJhKTMEQkTWqQqUoDH2Af8AuGL9HyD6Bv9swTM"
            + "i1TWrnTnrQP1hV8SKBUrOgbNHsnQrd98UvjrBJX7 prueba-rsa",
        "ssh-rsa",
        "prueba-rsa")]
    public void Reconoce_una_clave_publica_pegada_por_error(
        string lineaPublica, string algoritmoEsperado, string comentarioEsperado)
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(lineaPublica);

        Assert.Equal(FormatoClavePegada.ClavePublica, resultado.Formato);
        Assert.True(resultado.EsReconocida);
        Assert.Equal(algoritmoEsperado, resultado.Algoritmo);
        Assert.Equal(comentarioEsperado, resultado.Comentario);
        Assert.NotNull(resultado.Huella);
        Assert.Equal(lineaPublica, resultado.Huella!.LineaPublica);
    }

    [Fact]
    public void La_huella_de_la_clave_publica_ed25519_coincide_con_la_de_su_privada()
    {
        var deLaPublica = ReconocedorDeClavePegada.Reconocer(
            "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIE4mPoqozJT5vQeWSDT8mBohbg5W2QV9naccYsRwmgec "
                + "prueba-ed25519");

        Assert.Equal(HuellaEsperadaEd25519SinCifrar, deLaPublica.Huella!.Sha256);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    public void Un_texto_vacio_no_se_reconoce(string textoPegado)
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(textoPegado);

        Assert.Equal(FormatoClavePegada.Desconocido, resultado.Formato);
        Assert.False(resultado.EsReconocida);
        Assert.Equal("No se pegó ningún texto.", resultado.Motivo);
    }

    [Fact]
    public void Un_texto_nulo_no_se_reconoce()
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(null);

        Assert.False(resultado.EsReconocida);
    }

    [Theory]
    [InlineData("esto no es una clave, es una nota que alguien pegó por error")]
    [InlineData("usuario@servidor: Permission denied (publickey).")]
    [InlineData("ssh-keygen -t ed25519 -f id_ed25519")]
    public void Un_texto_que_no_coincide_con_ningun_formato_lo_dice_asi(string textoPegado)
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(textoPegado);

        Assert.Equal(FormatoClavePegada.Desconocido, resultado.Formato);
        Assert.False(resultado.EsReconocida);
        Assert.NotNull(resultado.Motivo);
        Assert.Contains("PuTTY", resultado.Motivo, StringComparison.Ordinal);
    }

    // Principio II: un cartel de "no se reconoce" no puede terminar mostrando la clave que se
    // supone que protege.
    [Theory]
    [InlineData("MARCADOR-SECRETO-1 esto no es ninguna clave conocida MARCADOR-SECRETO-1")]
    [InlineData(
        "-----BEGIN OPENSSH PRIVATE KEY-----\nMARCADOR-SECRETO-2-no-es-base64-valido")]
    [InlineData("PuTTY-User-Key-File-2: ssh-rsa\nMARCADOR-SECRETO-3-encabezado-incompleto")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nMARCADOR-SECRETO-4\n-----END RSA PRIVATE KEY-----")]
    public void Ningun_mensaje_contiene_el_texto_pegado(string textoPegado)
    {
        var resultado = ReconocedorDeClavePegada.Reconocer(textoPegado);

        void NoContieneElMarcador(string? mensaje, string etiqueta)
        {
            if (mensaje is null)
            {
                return;
            }

            Assert.False(
                mensaje.Contains("MARCADOR-SECRETO", StringComparison.Ordinal),
                $"{etiqueta} filtró el texto pegado: {mensaje}");
        }

        NoContieneElMarcador(resultado.Motivo, nameof(resultado.Motivo));
        NoContieneElMarcador(resultado.NotaHuella, nameof(resultado.NotaHuella));

        // El comentario y la línea pública sí pueden traer el texto pegado -son metadatos
        // públicos- y deliberadamente no se comprueban acá.
    }
}
