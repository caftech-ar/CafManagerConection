using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

// Interpretación y comparación de números de versión (FR-163): comparar como texto ordenaría
// 0.0.10 antes que 0.0.9.
public sealed class VersionDeAplicacionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("V1.2.3", 1, 2, 3)]
    [InlineData("0.0.1", 0, 0, 1)]
    public void Interpreta_los_componentes_con_o_sin_v_inicial(string texto, params int[] esperados)
    {
        Assert.True(VersionDeAplicacion.TryParse(texto, out var version));
        Assert.Equal(esperados, version!.Componentes);
    }

    [Theory]
    [InlineData("  1.2.3  ")]
    [InlineData(" v1.2.3")]
    [InlineData("1.2.3 ")]
    public void Los_espacios_de_mas_no_impiden_interpretarla(string texto)
    {
        Assert.True(VersionDeAplicacion.TryParse(texto, out var version));
        Assert.Equal([1, 2, 3], version!.Componentes);
    }

    [Fact]
    public void Distinta_cantidad_de_componentes_se_interpreta_igual()
    {
        Assert.True(VersionDeAplicacion.TryParse("1.0", out var corta));
        Assert.True(VersionDeAplicacion.TryParse("1.0.0", out var larga));

        Assert.Equal([1, 0], corta!.Componentes);
        Assert.Equal([1, 0, 0], larga!.Componentes);
    }

    [Theory]
    [InlineData("no-es-una-version")]
    [InlineData("uno.dos.tres")]
    [InlineData("1.2.3.")]
    [InlineData("1..3")]
    [InlineData("1.2.-3")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("1.2.3-")]
    public void Un_texto_que_no_es_version_no_se_interpreta(string? texto) =>
        Assert.False(VersionDeAplicacion.TryParse(texto, out _));

    [Fact]
    public void Parse_lanza_si_el_texto_no_es_una_version()
    {
        Assert.Throws<FormatException>(() => VersionDeAplicacion.Parse("no-es-una-version"));
    }

    [Fact]
    public void Parse_devuelve_la_version_si_el_texto_es_valido()
    {
        Assert.Equal([1, 2, 3], VersionDeAplicacion.Parse("1.2.3").Componentes);
    }

    [Fact]
    public void El_sufijo_de_prerelease_se_separa_del_nucleo()
    {
        Assert.True(VersionDeAplicacion.TryParse("1.2.0-beta", out var version));

        Assert.Equal([1, 2, 0], version!.Componentes);
        Assert.Equal("beta", version.Prerelease);
    }

    [Fact]
    public void El_sufijo_puede_traer_guiones_propios()
    {
        Assert.True(VersionDeAplicacion.TryParse("1.2.0-rc-1", out var version));

        Assert.Equal("rc-1", version!.Prerelease);
    }

    [Fact]
    public void _0_0_10_es_posterior_a_0_0_9()
    {
        var v10 = VersionDeAplicacion.Parse("0.0.10");
        var v9 = VersionDeAplicacion.Parse("0.0.9");

        Assert.True(v10 > v9);
        Assert.True(v9 < v10);
        Assert.Equal(1, v10.CompareTo(v9));
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("1.0.0", "1.1.0")]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.0.9", "1.0.10")]
    public void La_primera_es_anterior_a_la_segunda(string anterior, string posterior)
    {
        var v1 = VersionDeAplicacion.Parse(anterior);
        var v2 = VersionDeAplicacion.Parse(posterior);

        Assert.True(v1 < v2);
        Assert.True(v2 > v1);
    }

    [Fact]
    public void Componentes_de_mas_ceros_no_cambian_la_version()
    {
        var corta = VersionDeAplicacion.Parse("1.0");
        var larga = VersionDeAplicacion.Parse("1.0.0");

        Assert.Equal(corta, larga);
        Assert.True(corta == larga);
        Assert.Equal(0, corta.CompareTo(larga));
        Assert.Equal(corta.GetHashCode(), larga.GetHashCode());
    }

    [Fact]
    public void Componentes_de_mas_que_no_son_cero_si_la_hacen_posterior()
    {
        var v100 = VersionDeAplicacion.Parse("1.0.0");
        var v1001 = VersionDeAplicacion.Parse("1.0.0.1");

        Assert.True(v1001 > v100);
    }

    [Fact]
    public void Las_iguales_comparan_cero_y_son_iguales()
    {
        var a = VersionDeAplicacion.Parse("1.2.3");
        var b = VersionDeAplicacion.Parse("1.2.3");

        Assert.Equal(0, a.CompareTo(b));
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Una_prerelease_es_anterior_a_la_version_final_del_mismo_nucleo()
    {
        var beta = VersionDeAplicacion.Parse("1.2.0-beta");
        var final = VersionDeAplicacion.Parse("1.2.0");

        Assert.True(beta < final);
        Assert.True(final > beta);
    }

    [Fact]
    public void Entre_dos_prerelease_del_mismo_nucleo_decide_el_sufijo()
    {
        var beta = VersionDeAplicacion.Parse("1.2.0-beta");
        var rc = VersionDeAplicacion.Parse("1.2.0-rc");

        // No se persigue el orden completo de semver, sólo no confundir prerelease con final.
        Assert.True(beta < rc);
    }

    [Fact]
    public void El_nucleo_numerico_manda_sobre_el_sufijo()
    {
        var v121Beta = VersionDeAplicacion.Parse("1.2.1-beta");
        var v120 = VersionDeAplicacion.Parse("1.2.0");

        Assert.True(v121Beta > v120);
    }

    [Fact]
    public void ToString_reconstruye_el_texto_normalizado()
    {
        Assert.Equal("1.2.3", VersionDeAplicacion.Parse("v1.2.3").ToString());
        Assert.Equal("1.2.0-beta", VersionDeAplicacion.Parse("v1.2.0-beta").ToString());
    }

    [Fact]
    public void Comparada_contra_null_cualquier_version_es_posterior()
    {
        var v = VersionDeAplicacion.Parse("0.0.1");

        Assert.True(v.CompareTo(null) > 0);
    }

    [Fact]
    public void Igualdad_contra_null_es_falsa_y_no_lanza()
    {
        var v = VersionDeAplicacion.Parse("0.0.1");

        Assert.False(v.Equals(null));
        Assert.False(v == null);
        Assert.False(null == v);
        Assert.True(v != null);
    }
}

/// <summary>
/// Cuándo corresponde consultar si hay una versión nueva y cuándo corresponde avisarla
/// (FR-159, FR-159b, FR-160a).
/// </summary>
public sealed class PoliticaDeActualizacionesTests
{
    private static readonly DateTimeOffset Hoy =
        new(2026, 8, 28, 10, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Sin_posposicion_previa_corresponde_avisar()
    {
        var version = VersionDeAplicacion.Parse("1.0.0");

        Assert.True(PoliticaDeActualizaciones.CorrespondeAvisar(version, null, null, Hoy));
    }

    [Fact]
    public void Pospuesta_hoy_no_se_vuelve_a_avisar_hoy()
    {
        var version = VersionDeAplicacion.Parse("1.0.0");

        Assert.False(PoliticaDeActualizaciones.CorrespondeAvisar(
            version, version, Hoy.AddHours(-1), Hoy));
    }

    [Fact]
    public void Pospuesta_ayer_se_vuelve_a_avisar_hoy()
    {
        var version = VersionDeAplicacion.Parse("1.0.0");

        Assert.True(PoliticaDeActualizaciones.CorrespondeAvisar(
            version, version, Hoy.AddDays(-1), Hoy));
    }

    // Lo que se pospone es una versión concreta, no "cualquier aviso".
    [Fact]
    public void Pospuesta_una_version_no_silencia_el_aviso_de_otra_distinta()
    {
        var pospuesta = VersionDeAplicacion.Parse("1.0.0");
        var nueva = VersionDeAplicacion.Parse("1.0.1");

        Assert.True(PoliticaDeActualizaciones.CorrespondeAvisar(
            nueva, pospuesta, Hoy.AddHours(-1), Hoy));
    }

    [Fact]
    public void CorrespondeAvisar_lanza_si_la_version_disponible_es_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PoliticaDeActualizaciones.CorrespondeAvisar(null!, null, null, Hoy));
    }
}
