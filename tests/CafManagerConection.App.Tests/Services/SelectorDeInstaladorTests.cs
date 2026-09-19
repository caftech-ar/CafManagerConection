using CafManagerConection.App.Services;
using CafManagerConection.Infrastructure.Actualizaciones;

namespace CafManagerConection.App.Tests.Services;

public sealed class SelectorDeInstaladorTests
{
    private static readonly ActivoDeRelease Liviano =
        new("CafManagerConection-setup.exe", "https://x/liviano");

    private static readonly ActivoDeRelease Completo =
        new("CafManagerConection-setup-completo.exe", "https://x/completo");

    private static readonly ActivoDeRelease Hash =
        new("CafManagerConection-setup.exe.sha256", "https://x/hash");

    private static readonly ActivoDeRelease Notas =
        new("Notas.txt", "https://x/notas");

    [Fact]
    public void Elige_el_exe_que_contiene_setup()
    {
        var elegido = SelectorDeInstalador.Elegir([Hash, Liviano, Notas], preferido: null);

        Assert.Equal(Liviano, elegido);
    }

    [Fact]
    public void No_confunde_el_archivo_de_hash_con_el_instalador()
    {
        Assert.Null(SelectorDeInstalador.Elegir([Hash], preferido: null));
    }

    [Fact]
    public void Sin_ningun_instalador_no_hay_nada_que_elegir()
    {
        Assert.Null(SelectorDeInstalador.Elegir([Notas], preferido: null));
    }

    [Fact]
    public void Lista_vacia_no_elige_nada()
    {
        Assert.Null(SelectorDeInstalador.Elegir([], preferido: null));
    }

    [Fact]
    public void Con_la_marca_completo_ofrece_el_completo()
    {
        var elegido = SelectorDeInstalador.Elegir(
            [Hash, Liviano, Completo], TipoDeInstalador.Completo);

        Assert.Equal(Completo, elegido);
    }

    [Fact]
    public void Con_la_marca_liviano_ofrece_el_liviano_aunque_el_completo_venga_antes()
    {
        var elegido = SelectorDeInstalador.Elegir(
            [Completo, Liviano], TipoDeInstalador.Liviano);

        Assert.Equal(Liviano, elegido);
    }

    [Fact]
    public void Sin_marca_ofrece_el_liviano_aunque_el_completo_venga_antes()
    {
        var elegido = SelectorDeInstalador.Elegir([Completo, Liviano], preferido: null);

        Assert.Equal(Liviano, elegido);
    }

    [Fact]
    public void Sin_marca_y_sin_liviano_ofrece_el_unico_instalador_publicado()
    {
        var elegido = SelectorDeInstalador.Elegir([Completo], preferido: null);

        Assert.Equal(Completo, elegido);
    }

    [Fact]
    public void Si_la_release_no_trae_el_tipo_preferido_cae_al_otro()
    {
        var elegido = SelectorDeInstalador.Elegir([Liviano], TipoDeInstalador.Completo);

        Assert.Equal(Liviano, elegido);
    }

    [Theory]
    [InlineData("liviano", TipoDeInstalador.Liviano)]
    [InlineData("LIVIANO", TipoDeInstalador.Liviano)]
    [InlineData("completo", TipoDeInstalador.Completo)]
    [InlineData(" Completo ", TipoDeInstalador.Completo)]
    public void La_marca_del_registro_se_interpreta_sin_mirar_mayusculas_ni_espacios(
        string marca, TipoDeInstalador esperado)
    {
        Assert.Equal(esperado, SelectorDeInstalador.InterpretarMarca(marca));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("portable")]
    public void Una_marca_que_no_se_reconoce_se_comporta_como_ausente(string? marca)
    {
        var preferido = SelectorDeInstalador.InterpretarMarca(marca);

        Assert.Null(preferido);
        Assert.Equal(Liviano, SelectorDeInstalador.Elegir([Completo, Liviano], preferido));
    }
    [Fact]
    public void Se_enumeran_los_dos_instaladores_con_su_tipo()
    {
        var disponibles = SelectorDeInstalador.Disponibles(
            [Hash, Liviano, Notas, Completo], instalado: null);

        Assert.Equal(2, disponibles.Count);
        Assert.Contains(disponibles, d => d.Tipo == TipoDeInstalador.Liviano);
        Assert.Contains(disponibles, d => d.Tipo == TipoDeInstalador.Completo);
    }

    [Fact]
    public void Lo_que_no_es_instalador_no_se_enumera()
    {
        var disponibles = SelectorDeInstalador.Disponibles([Hash, Notas], instalado: null);

        Assert.Empty(disponibles);
    }

    [Fact]
    public void Se_marca_el_que_coincide_con_el_instalado()
    {
        var disponibles = SelectorDeInstalador.Disponibles(
            [Liviano, Completo], TipoDeInstalador.Completo);

        Assert.True(disponibles.Single(d => d.Tipo == TipoDeInstalador.Completo).EsElInstalado);
        Assert.False(disponibles.Single(d => d.Tipo == TipoDeInstalador.Liviano).EsElInstalado);
    }

    [Fact]
    public void Sin_marca_del_registro_no_se_senala_ninguno()
    {
        var disponibles = SelectorDeInstalador.Disponibles([Liviano, Completo], instalado: null);

        Assert.All(disponibles, d => Assert.False(d.EsElInstalado));
    }

    [Fact]
    public void El_tamano_se_muestra_en_megabytes()
    {
        var disponibles = SelectorDeInstalador.Disponibles(
            [Liviano with { Bytes = 12 * 1024 * 1024 }], instalado: null);

        Assert.Equal("12 MB", disponibles[0].Tamano);
    }

    [Fact]
    public void Sin_tamano_publicado_no_se_inventa_ninguno()
    {
        var disponibles = SelectorDeInstalador.Disponibles([Liviano], instalado: null);

        Assert.Null(disponibles[0].Tamano);
    }

    [Fact]
    public void Cada_tipo_dice_que_precisa()
    {
        var disponibles = SelectorDeInstalador.Disponibles([Liviano, Completo], instalado: null);

        Assert.Contains(
            "precisa .NET",
            disponibles.Single(d => d.Tipo == TipoDeInstalador.Liviano).Condicion,
            StringComparison.Ordinal);

        Assert.Contains(
            "incluye todo",
            disponibles.Single(d => d.Tipo == TipoDeInstalador.Completo).Condicion,
            StringComparison.Ordinal);
    }
}
