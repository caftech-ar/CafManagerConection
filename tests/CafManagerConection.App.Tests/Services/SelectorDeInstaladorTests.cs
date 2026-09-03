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
}
