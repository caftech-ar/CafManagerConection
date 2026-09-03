using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

// Lo que hay que acertar es cuándo NO copiar: copiar de más llena la carpeta y empuja fuera del
// tope a copias que sí tenían algo distinto.
public sealed class PoliticaDeCopiasTests
{
    private static readonly DateTimeOffset Hoy =
        new(2026, 8, 26, 15, 0, 0, TimeSpan.FromHours(-3));

    private static CopiaDeSeguridad Copia(DateTimeOffset cuando, string huella = "aaa") =>
        new(PoliticaDeCopias.NombreDeArchivo(cuando), cuando, 1024, huella);

    [Fact]
    public void Sin_ninguna_copia_se_hace_la_primera() =>
        Assert.True(PoliticaDeCopias.HayQueCopiar([], "aaa", Hoy));

    [Fact]
    public void Con_una_copia_de_hoy_no_se_hace_otra()
    {
        var existentes = new[] { Copia(Hoy.AddHours(-4), "bbb") };

        Assert.False(PoliticaDeCopias.HayQueCopiar(existentes, "aaa", Hoy));
    }

    [Fact]
    public void Si_cambio_la_base_desde_ayer_se_copia()
    {
        var existentes = new[] { Copia(Hoy.AddDays(-1), "vieja") };

        Assert.True(PoliticaDeCopias.HayQueCopiar(existentes, "nueva", Hoy));
    }

    [Fact]
    public void Si_no_cambio_nada_no_se_copia_aunque_pasen_dias()
    {
        var existentes = new[] { Copia(Hoy.AddDays(-7), "misma") };

        Assert.False(PoliticaDeCopias.HayQueCopiar(existentes, "misma", Hoy));
    }

    // Se compara contra la última: un revert a un estado que ya existía en una copia vieja igual
    // hay que registrarlo.
    [Fact]
    public void Se_compara_contra_la_ultima_no_contra_todas()
    {
        var existentes = new[]
        {
            Copia(Hoy.AddDays(-5), "estado-a"),
            Copia(Hoy.AddDays(-2), "estado-b"),
        };

        Assert.True(PoliticaDeCopias.HayQueCopiar(existentes, "estado-a", Hoy));
    }

    [Fact]
    public void El_orden_en_que_llegan_no_importa()
    {
        var desordenadas = new[]
        {
            Copia(Hoy.AddDays(-2), "vieja"),
            Copia(Hoy, "de-hoy"),
            Copia(Hoy.AddDays(-9), "mas-vieja"),
        };

        Assert.False(PoliticaDeCopias.HayQueCopiar(desordenadas, "otra", Hoy));
    }

    [Fact]
    public void La_huella_no_distingue_mayusculas()
    {
        var existentes = new[] { Copia(Hoy.AddDays(-1), "ABCDEF") };

        Assert.False(PoliticaDeCopias.HayQueCopiar(existentes, "abcdef", Hoy));
    }

    [Fact]
    public void Por_debajo_del_tope_no_sobra_ninguna()
    {
        var existentes = Enumerable.Range(1, 5).Select(i => Copia(Hoy.AddDays(-i))).ToList();

        Assert.Empty(PoliticaDeCopias.Sobrantes(existentes, 10));
    }

    [Fact]
    public void Justo_en_el_tope_no_sobra_ninguna()
    {
        var existentes = Enumerable.Range(1, 10).Select(i => Copia(Hoy.AddDays(-i))).ToList();

        Assert.Empty(PoliticaDeCopias.Sobrantes(existentes, 10));
    }

    // Guardar diez significa diez en total, incluida la que se acaba de hacer.
    [Fact]
    public void Pasado_el_tope_sobran_las_mas_viejas()
    {
        var existentes = Enumerable.Range(1, 13).Select(i => Copia(Hoy.AddDays(-i))).ToList();

        var sobrantes = PoliticaDeCopias.Sobrantes(existentes, 10);

        Assert.Equal(3, sobrantes.Count);
        Assert.All(sobrantes, c => Assert.True(c.Momento <= Hoy.AddDays(-11)));
    }

    [Fact]
    public void Un_tope_absurdo_se_acota()
    {
        var existentes = Enumerable.Range(1, 5).Select(i => Copia(Hoy.AddDays(-i))).ToList();

        // Cero guardaría ninguna y borraría todo, incluida la que se acaba de hacer.
        Assert.Equal(4, PoliticaDeCopias.Sobrantes(existentes, 0).Count);
        Assert.Empty(PoliticaDeCopias.Sobrantes(existentes, 9999));
    }

    // El literal esperado se arma con el desplazamiento de la máquina que corre la prueba: con
    // uno fijo, esta prueba pasaba en UTC-3 y fallaba en el CI, que corre en UTC.
    [Fact]
    public void El_nombre_lleva_el_sello_ordenable()
    {
        var alMediodia = new DateTime(2026, 8, 26, 15, 0, 0, DateTimeKind.Unspecified);
        var local = new DateTimeOffset(alMediodia, TimeZoneInfo.Local.GetUtcOffset(alMediodia));

        var nombre = PoliticaDeCopias.NombreDeArchivo(local);

        Assert.Equal("cmc-20260826-150000.db", nombre);
    }

    [Fact]
    public void Los_nombres_ordenan_por_antiguedad()
    {
        var nombres = new[]
        {
            PoliticaDeCopias.NombreDeArchivo(Hoy),
            PoliticaDeCopias.NombreDeArchivo(Hoy.AddDays(-1)),
            PoliticaDeCopias.NombreDeArchivo(Hoy.AddMonths(-2)),
        };

        Assert.Equal(nombres.OrderBy(n => n, StringComparer.Ordinal), nombres.Reverse());
    }

    // No se lee de la fecha del archivo: copiar, restaurar o sincronizar la carpeta la reescribe.
    [Fact]
    public void El_momento_se_recupera_del_nombre()
    {
        var leido = PoliticaDeCopias.MomentoDe(PoliticaDeCopias.NombreDeArchivo(Hoy));

        Assert.NotNull(leido);
        Assert.Equal(Hoy.LocalDateTime, leido!.Value.LocalDateTime);
    }

    [Fact]
    public void El_momento_se_recupera_con_la_ruta_completa()
    {
        var leido = PoliticaDeCopias.MomentoDe(@"D:\OneDrive\copias\cmc-20260826-150000.db");

        Assert.NotNull(leido);
    }

    [Theory]
    [InlineData("cualquier-cosa.db")]
    [InlineData("cmc-sin-fecha.db")]
    [InlineData("cmc-20261399-999999.db")]
    [InlineData("cmc.db")]
    [InlineData("")]
    public void Un_nombre_ajeno_no_se_interpreta(string nombre) =>
        Assert.Null(PoliticaDeCopias.MomentoDe(nombre));

    [Fact]
    public void Los_ajustes_se_acotan()
    {
        Assert.Equal(1, (AjustesDeCopia.Default with { CuantasGuardar = -5 }).Normalizados().CuantasGuardar);
        Assert.Equal(100, (AjustesDeCopia.Default with { CuantasGuardar = 500 }).Normalizados().CuantasGuardar);
    }

    [Fact]
    public void La_carpeta_se_recorta()
    {
        var ajustes = (AjustesDeCopia.Default with { Carpeta = "  D:\\copias  " }).Normalizados();

        Assert.Equal(@"D:\copias", ajustes.Carpeta);
    }

    [Fact]
    public void Por_omision_se_guardan_diez_y_estan_activas()
    {
        Assert.True(AjustesDeCopia.Default.Activas);
        Assert.Equal(10, AjustesDeCopia.Default.CuantasGuardar);
    }
}
