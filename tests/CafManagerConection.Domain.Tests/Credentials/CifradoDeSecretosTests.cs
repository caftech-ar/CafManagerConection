using System.Security.Cryptography;
using CafManagerConection.Domain.Credentials;
using Xunit;

namespace CafManagerConection.Domain.Tests.Credentials;

public sealed class CifradoDeSecretosTests
{
    private const string Secreto = "contrasena-de-prueba-9f21!";

    [Fact]
    public void Lo_que_se_cifra_vuelve_igual()
    {
        var clave = CifradoDeSecretos.ClaveNueva();
        var sobre = CifradoDeSecretos.CifrarTexto(clave, Secreto);

        Assert.Equal(Secreto, new string(CifradoDeSecretos.DescifrarTexto(clave, sobre)));
    }

    [Fact]
    public void El_nonce_es_distinto_en_cada_cifrado()
    {
        var clave = CifradoDeSecretos.ClaveNueva();

        var nonces = Enumerable.Range(0, 200)
            .Select(_ => Convert.ToHexString(CifradoDeSecretos.CifrarTexto(clave, Secreto).Nonce))
            .ToList();

        // Un nonce repetido con la misma clave rompe AES-GCM entero, y es lo que pasa si alguien
        // lo cachea "para no generarlo cada vez".
        Assert.Equal(200, nonces.Distinct().Count());
    }

    [Fact]
    public void El_mismo_secreto_cifrado_dos_veces_no_da_el_mismo_texto()
    {
        var clave = CifradoDeSecretos.ClaveNueva();

        var uno = CifradoDeSecretos.CifrarTexto(clave, Secreto);
        var otro = CifradoDeSecretos.CifrarTexto(clave, Secreto);

        Assert.NotEqual(Convert.ToHexString(uno.Cifrado), Convert.ToHexString(otro.Cifrado));
    }

    [Fact]
    public void Con_otra_clave_no_descifra_y_no_devuelve_vacio()
    {
        var sobre = CifradoDeSecretos.CifrarTexto(CifradoDeSecretos.ClaveNueva(), Secreto);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => CifradoDeSecretos.DescifrarTexto(CifradoDeSecretos.ClaveNueva(), sobre));
    }

    [Fact]
    public void Un_texto_cifrado_con_un_byte_cambiado_se_rechaza()
    {
        var clave = CifradoDeSecretos.ClaveNueva();
        var sobre = CifradoDeSecretos.CifrarTexto(clave, Secreto);

        sobre.Cifrado[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(
            () => CifradoDeSecretos.DescifrarTexto(clave, sobre));
    }

    [Fact]
    public void Un_nonce_cambiado_se_rechaza()
    {
        var clave = CifradoDeSecretos.ClaveNueva();
        var sobre = CifradoDeSecretos.CifrarTexto(clave, Secreto);

        sobre.Nonce[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(
            () => CifradoDeSecretos.DescifrarTexto(clave, sobre));
    }

    [Fact]
    public void Un_sobre_con_forma_invalida_se_rechaza_antes_de_intentar_descifrar()
    {
        var sobre = new SobreCifrado([1, 2, 3], [4, 5, 6]);

        Assert.Throws<CryptographicException>(
            () => CifradoDeSecretos.Descifrar(CifradoDeSecretos.ClaveNueva(), sobre));
    }

    [Fact]
    public void La_misma_clave_maestra_y_la_misma_sal_dan_la_misma_clave()
    {
        var sal = CifradoDeSecretos.SalNueva();

        var una = CifradoDeSecretos.Derivar("Zorro-Verde-2026!", sal, 1000);
        var otra = CifradoDeSecretos.Derivar("Zorro-Verde-2026!", sal, 1000);

        Assert.Equal(Convert.ToHexString(una), Convert.ToHexString(otra));
    }

    [Fact]
    public void Otra_sal_da_otra_clave()
    {
        var una = CifradoDeSecretos.Derivar("Zorro-Verde-2026!", CifradoDeSecretos.SalNueva(), 1000);
        var otra = CifradoDeSecretos.Derivar("Zorro-Verde-2026!", CifradoDeSecretos.SalNueva(), 1000);

        Assert.NotEqual(Convert.ToHexString(una), Convert.ToHexString(otra));
    }

    [Fact]
    public void Otra_cantidad_de_iteraciones_da_otra_clave()
    {
        var sal = CifradoDeSecretos.SalNueva();

        var una = CifradoDeSecretos.Derivar("Zorro-Verde-2026!", sal, 1000);
        var otra = CifradoDeSecretos.Derivar("Zorro-Verde-2026!", sal, 2000);

        Assert.NotEqual(Convert.ToHexString(una), Convert.ToHexString(otra));
    }

    [Fact]
    public void La_clave_derivada_mide_32_bytes()
    {
        var derivada = CifradoDeSecretos.Derivar("Zorro-Verde-2026!", CifradoDeSecretos.SalNueva(), 1000);

        Assert.Equal(CifradoDeSecretos.LargoDeLaClave, derivada.Length);
    }

    [Fact]
    public void Una_clave_maestra_con_acentos_y_emoji_va_y_vuelve()
    {
        const string ConTodo = "ñandú-2026-🔑!";

        var sal = CifradoDeSecretos.SalNueva();
        var clave = CifradoDeSecretos.Derivar(ConTodo, sal, 1000);
        var sobre = CifradoDeSecretos.CifrarTexto(clave, Secreto);

        var otraVez = CifradoDeSecretos.Derivar(ConTodo, sal, 1000);

        Assert.Equal(Secreto, new string(CifradoDeSecretos.DescifrarTexto(otraVez, sobre)));
    }

    [Fact]
    public void Un_secreto_con_acentos_y_emoji_va_y_vuelve()
    {
        const string Raro = "año-señal-🔒-ñ";

        var clave = CifradoDeSecretos.ClaveNueva();
        var sobre = CifradoDeSecretos.CifrarTexto(clave, Raro);

        Assert.Equal(Raro, new string(CifradoDeSecretos.DescifrarTexto(clave, sobre)));
    }

    [Fact]
    public void Dos_claves_nuevas_seguidas_no_son_iguales()
    {
        Assert.NotEqual(
            Convert.ToHexString(CifradoDeSecretos.ClaveNueva()),
            Convert.ToHexString(CifradoDeSecretos.ClaveNueva()));
    }

    // Es la trampa mas facil de pisar: la sobrecarga que toma string es la que autocompleta
    // primero, y una clave maestra en un string queda en el monton sin poder pisarse.
    [Fact]
    public void La_derivacion_no_usa_la_sobrecarga_de_string()
    {
        var cuerpo = typeof(CifradoDeSecretos)
            .GetMethod(nameof(CifradoDeSecretos.Derivar))!;

        var primero = cuerpo.GetParameters()[0].ParameterType;

        Assert.Equal(typeof(ReadOnlySpan<char>), primero);
    }
}
