using CafManagerConection.App.Services;

namespace CafManagerConection.App.Tests.Services;

public sealed class PuertoLocalSugeridoTests
{
    [Fact]
    public void Si_el_puerto_del_servidor_esta_libre_se_usa_el_mismo_numero()
    {
        Assert.Equal(5432, PuertoLocalSugerido.Elegir(5432, new HashSet<int>()));
    }

    [Fact]
    public void Si_esta_tomado_se_corre_a_la_franja_alta_conservando_el_numero()
    {
        Assert.Equal(15432, PuertoLocalSugerido.Elegir(5432, new HashSet<int> { 5432 }));
    }

    [Fact]
    public void Si_el_desplazado_tambien_esta_tomado_se_busca_el_siguiente()
    {
        var tomados = new HashSet<int> { 5432, 15432, 15433 };

        Assert.Equal(15434, PuertoLocalSugerido.Elegir(5432, tomados));
    }

    [Fact]
    public void Un_puerto_reservado_por_otro_tunel_cuenta_como_tomado()
    {
        var tomados = PuertoLocalSugerido.Tomados([8080]);

        Assert.Contains(8080, tomados);
        Assert.NotEqual(8080, PuertoLocalSugerido.Elegir(8080, tomados));
    }

    [Fact]
    public void Un_puerto_de_la_franja_alta_no_se_desplaza_fuera_del_rango_valido()
    {
        // La franja efímera de Windows arranca en 49152.
        var elegido = PuertoLocalSugerido.Elegir(60000, new HashSet<int> { 60000 });

        Assert.InRange(elegido, 10000, 49151);
    }

    [Fact]
    public void Un_puerto_remoto_fuera_de_rango_no_se_propone_tal_cual()
    {
        var elegido = PuertoLocalSugerido.Elegir(70000, new HashSet<int>());

        Assert.InRange(elegido, 10000, 49151);
    }

    [Fact]
    public void El_puerto_propuesto_nunca_esta_en_la_lista_de_tomados()
    {
        var tomados = new HashSet<int>(Enumerable.Range(10000, 500)) { 3000 };

        var elegido = PuertoLocalSugerido.Elegir(3000, tomados);

        Assert.DoesNotContain(elegido, tomados);
    }
}
