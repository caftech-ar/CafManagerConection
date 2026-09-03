using CafManagerConection.Domain.Settings;
using Xunit;

namespace CafManagerConection.Domain.Tests.Settings;

public sealed class TipografiasDisponiblesTests
{
    private static readonly string[] Instaladas =
    [
        "Arial", "Consolas", "Courier New", "Segoe UI", "Wingdings", "Cascadia Mono",
        "Una Fuente Rara",
    ];

    [Fact]
    public void Las_preferidas_instaladas_van_primero_y_en_su_orden()
    {
        var orden = TipografiasDisponibles.Ordenar(
            Instaladas, TipografiasDisponibles.PreferidasParaTerminal);

        Assert.Equal("Cascadia Mono", orden[0]);
        Assert.Equal("Consolas", orden[1]);
        Assert.Equal("Courier New", orden[2]);
    }

    [Fact]
    public void Una_preferida_que_no_esta_instalada_no_se_ofrece()
    {
        var orden = TipografiasDisponibles.Ordenar(
            Instaladas, TipografiasDisponibles.PreferidasParaTerminal);

        // JetBrains Mono esta en la lista de preferidas pero no en las instaladas.
        Assert.DoesNotContain("JetBrains Mono", orden);
    }

    [Fact]
    public void El_resto_aparece_igual_y_en_orden_alfabetico()
    {
        var orden = TipografiasDisponibles.Ordenar(
            Instaladas, TipografiasDisponibles.PreferidasParaTerminal);

        // Quien tiene la que quiere y no esta en ninguna lista tambien la tiene que poder elegir.
        Assert.Contains("Una Fuente Rara", orden);
        Assert.Contains("Wingdings", orden);

        var resto = orden.Skip(3).ToList();
        Assert.Equal(resto.OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase), resto);
    }

    [Fact]
    public void No_se_repite_ninguna()
    {
        var orden = TipografiasDisponibles.Ordenar(
            ["Consolas", "consolas", "CONSOLAS", "Arial"],
            TipografiasDisponibles.PreferidasParaTerminal);

        Assert.Single(orden, f => f.Equals("Consolas", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Los_nombres_vacios_se_descartan()
    {
        var orden = TipografiasDisponibles.Ordenar(
            ["Arial", "", "   ", null!], TipografiasDisponibles.PreferidasParaInterfaz);

        Assert.Equal(["Arial"], orden);
    }

    [Fact]
    public void Sin_ninguna_instalada_la_lista_queda_vacia_y_no_falla()
    {
        Assert.Empty(TipografiasDisponibles.Ordenar([], TipografiasDisponibles.PreferidasParaTerminal));
    }

    [Fact]
    public void Buscar_sin_texto_devuelve_todo()
    {
        Assert.Equal(7, TipografiasDisponibles.Buscar(Instaladas, null).Count);
        Assert.Equal(7, TipografiasDisponibles.Buscar(Instaladas, "  ").Count);
    }

    // Quien busca «mono» quiere ver «Cascadia Mono», no solo lo que empiece con mono.
    [Fact]
    public void Buscar_coincide_en_cualquier_parte_del_nombre()
    {
        Assert.Equal(["Cascadia Mono"], TipografiasDisponibles.Buscar(Instaladas, "mono"));
    }

    [Fact]
    public void Buscar_no_distingue_mayusculas()
    {
        Assert.Equal(["Consolas"], TipografiasDisponibles.Buscar(Instaladas, "CONSO"));
    }

    [Fact]
    public void Buscar_algo_que_no_esta_devuelve_vacio()
    {
        Assert.Empty(TipografiasDisponibles.Buscar(Instaladas, "comic"));
    }

    [Fact]
    public void Las_dos_listas_de_preferidas_no_estan_vacias_ni_repiten()
    {
        foreach (var lista in new[]
                 {
                     TipografiasDisponibles.PreferidasParaTerminal,
                     TipografiasDisponibles.PreferidasParaInterfaz,
                 })
        {
            Assert.NotEmpty(lista);
            Assert.Equal(lista.Length, lista.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
