using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Tests.Connections;

/// <summary>Parseo de <c>usuario@host:puerto</c> para la conexión rápida (FR-149).</summary>
public class QuickConnectionTargetTests
{
    [Fact]
    public void Usuario_arroba_host_usa_el_puerto_por_omision()
    {
        var ok = QuickConnectionTarget.TryParse(
            "root@192.0.2.10", out var usuario, out var host, out var puerto, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("root", usuario);
        Assert.Equal("192.0.2.10", host);
        Assert.Equal(22, puerto);
    }

    [Fact]
    public void Usuario_arroba_host_dos_puntos_puerto_toma_el_puerto_escrito()
    {
        var ok = QuickConnectionTarget.TryParse(
            "deploy@app.interno:2222", out var usuario, out var host, out var puerto, out _);

        Assert.True(ok);
        Assert.Equal("deploy", usuario);
        Assert.Equal("app.interno", host);
        Assert.Equal(2222, puerto);
    }

    [Fact]
    public void Solo_host_no_trae_usuario_y_deja_que_quien_llama_decida()
    {
        var ok = QuickConnectionTarget.TryParse(
            "app.interno", out var usuario, out var host, out var puerto, out _);

        Assert.True(ok);
        Assert.Null(usuario);
        Assert.Equal("app.interno", host);
        Assert.Equal(22, puerto);
    }

    [Fact]
    public void Ipv6_entre_corchetes_sin_puerto_no_confunde_sus_propios_dos_puntos()
    {
        var ok = QuickConnectionTarget.TryParse(
            "admin@[2001:db8::1]", out var usuario, out var host, out var puerto, out _);

        Assert.True(ok);
        Assert.Equal("admin", usuario);
        Assert.Equal("2001:db8::1", host);
        Assert.Equal(22, puerto);
    }

    [Fact]
    public void Ipv6_entre_corchetes_con_puerto_separa_bien_la_direccion_del_puerto()
    {
        var ok = QuickConnectionTarget.TryParse(
            "admin@[2001:db8::1]:2222", out var usuario, out var host, out var puerto, out _);

        Assert.True(ok);
        Assert.Equal("admin", usuario);
        Assert.Equal("2001:db8::1", host);
        Assert.Equal(2222, puerto);
    }

    [Fact]
    public void Ipv6_sin_usuario_tambien_funciona()
    {
        var ok = QuickConnectionTarget.TryParse(
            "[::1]:22", out var usuario, out var host, out var puerto, out _);

        Assert.True(ok);
        Assert.Null(usuario);
        Assert.Equal("::1", host);
        Assert.Equal(22, puerto);
    }

    [Fact]
    public void Espacios_de_mas_y_mayusculas_no_impiden_parsear()
    {
        var ok = QuickConnectionTarget.TryParse(
            "  Root@Servidor-Prod.local : 2222  ",
            out var usuario, out var host, out var puerto, out _);

        Assert.True(ok);
        Assert.Equal("Root", usuario);
        Assert.Equal("Servidor-Prod.local", host);
        Assert.Equal(2222, puerto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Texto_vacio_es_invalido(string texto)
    {
        var ok = QuickConnectionTarget.TryParse(
            texto, out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.DoesNotContain("Error", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arroba_sin_usuario_antes_es_invalida_y_lo_dice()
    {
        var ok = QuickConnectionTarget.TryParse(
            "@host", out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("usuario", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arroba_sin_host_despues_es_invalida()
    {
        var ok = QuickConnectionTarget.TryParse(
            "root@", out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("host", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ipv6_sin_corchete_de_cierre_lo_dice_y_no_solo_error()
    {
        var ok = QuickConnectionTarget.TryParse(
            "root@[2001:db8::1", out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("corchete", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ipv6_sin_corchetes_pero_con_varios_dos_puntos_sugiere_los_corchetes()
    {
        var ok = QuickConnectionTarget.TryParse(
            "root@2001:db8::1:22", out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("corchetes", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Despues_del_corchete_de_ipv6_solo_se_admite_dos_puntos_puerto()
    {
        var ok = QuickConnectionTarget.TryParse(
            "root@[::1]x22", out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("IPv6", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("root@host:abc")]
    [InlineData("root@host:")]
    public void Puerto_no_numerico_dice_cual_es_el_problema(string texto)
    {
        var ok = QuickConnectionTarget.TryParse(
            texto, out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("puerto", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("root@host:0")]
    [InlineData("root@host:65536")]
    [InlineData("root@host:-1")]
    public void Puerto_fuera_de_rango_es_invalido(string texto)
    {
        var ok = QuickConnectionTarget.TryParse(
            texto, out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.Contains("65535", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("root@host:1")]
    [InlineData("root@host:65535")]
    public void Puerto_en_los_limites_del_rango_es_valido(string texto)
    {
        var ok = QuickConnectionTarget.TryParse(
            texto, out _, out _, out var puerto, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(puerto is 1 or 65535);
    }
}
