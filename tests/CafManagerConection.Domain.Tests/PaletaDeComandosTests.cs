using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

// Equivocar qué comandos aplican a la sesión abierta no rompe nada -no hay excepción- y deja
// una paleta que ofrece comandos de otro servidor, que es peor que no tenerla.
public sealed class PaletaDeComandosTests
{
    private static readonly Guid Servidor = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Otro = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static PaletaDeComandos Con(params ComandoGuardado[] comandos) => new(comandos);

    private static ComandoGuardado Cmd(string nombre, string comando, Guid? conexion = null) =>
        new(Guid.NewGuid(), nombre, comando, conexion);

    [Theory]
    [InlineData("Discos", "df -h", true)]
    [InlineData("", "df -h", false)]
    [InlineData("   ", "df -h", false)]
    [InlineData("Discos", "", false)]
    [InlineData("Discos", "   ", false)]
    public void Hacen_falta_nombre_y_comando(string nombre, string comando, bool esperado) =>
        Assert.Equal(esperado, Cmd(nombre, comando).EsValido);

    [Fact]
    public void Los_invalidos_no_entran_en_la_paleta()
    {
        var paleta = Con(Cmd("Discos", "df -h"), Cmd("", "uname -a"));

        Assert.Equal(1, paleta.Cantidad);
    }

    [Fact]
    public void Se_recortan_los_espacios_al_agregar()
    {
        var paleta = new PaletaDeComandos();
        var nuevo = paleta.Agregar("  Discos  ", "  df -h  ");

        Assert.NotNull(nuevo);
        Assert.Equal("Discos", nuevo.Nombre);
        Assert.Equal("df -h", nuevo.Comando);
    }

    [Fact]
    public void No_se_agrega_uno_invalido()
    {
        var paleta = new PaletaDeComandos();

        Assert.Null(paleta.Agregar("   ", "df -h"));
        Assert.Equal(0, paleta.Cantidad);
    }

    [Fact]
    public void Los_globales_se_ven_en_cualquier_conexion()
    {
        var paleta = Con(Cmd("Discos", "df -h"));

        Assert.Single(paleta.Visibles(Servidor));
        Assert.Single(paleta.Visibles(Otro));
    }

    // El accidente que hay que impedir: supervisorctl restart apuntando al proceso equivocado.
    [Fact]
    public void El_de_una_conexion_no_se_ve_en_otra()
    {
        var paleta = Con(Cmd("Reiniciar api", "supervisorctl restart api", Servidor));

        Assert.Single(paleta.Visibles(Servidor));
        Assert.Empty(paleta.Visibles(Otro));
    }

    // Sin conexión -la paleta abierta desde la configuración- no hay contra qué contrastar los
    // comandos atados a un servidor.
    [Fact]
    public void Sin_conexion_se_ven_solo_los_globales()
    {
        var paleta = Con(
            Cmd("Discos", "df -h"),
            Cmd("Reiniciar api", "supervisorctl restart api", Servidor));

        var visibles = paleta.Visibles(null);

        Assert.Single(visibles);
        Assert.Equal("Discos", visibles[0].Nombre);
    }

    [Fact]
    public void Los_de_la_conexion_van_antes_que_los_globales()
    {
        var paleta = Con(
            Cmd("Alfa global", "echo a"),
            Cmd("Zeta del servidor", "echo z", Servidor));

        var visibles = paleta.Visibles(Servidor);

        Assert.Equal("Zeta del servidor", visibles[0].Nombre);
        Assert.Equal("Alfa global", visibles[1].Nombre);
    }

    [Fact]
    public void Dentro_de_cada_grupo_van_alfabeticos()
    {
        var paleta = Con(
            Cmd("Zeta", "echo z"),
            Cmd("alfa", "echo a"),
            Cmd("Beta", "echo b"));

        Assert.Equal(
            ["alfa", "Beta", "Zeta"],
            paleta.Visibles(Servidor).Select(c => c.Nombre));
    }

    [Fact]
    public void El_filtro_busca_en_el_nombre()
    {
        var paleta = Con(Cmd("Discos", "df -h"), Cmd("Memoria", "free -m"));

        Assert.Single(paleta.Visibles(Servidor, "disc"));
    }

    [Fact]
    public void El_filtro_tambien_busca_en_el_comando()
    {
        var paleta = Con(Cmd("Discos", "df -h"), Cmd("Memoria", "free -m"));

        var visibles = paleta.Visibles(Servidor, "free");

        Assert.Single(visibles);
        Assert.Equal("Memoria", visibles[0].Nombre);
    }

    [Fact]
    public void El_filtro_no_distingue_mayusculas() =>
        Assert.Single(Con(Cmd("Discos", "df -h")).Visibles(Servidor, "DISCOS"));

    [Fact]
    public void Un_filtro_en_blanco_no_filtra() =>
        Assert.Single(Con(Cmd("Discos", "df -h")).Visibles(Servidor, "   "));

    [Fact]
    public void Un_filtro_sin_coincidencias_no_devuelve_nada() =>
        Assert.Empty(Con(Cmd("Discos", "df -h")).Visibles(Servidor, "nginx"));

    [Fact]
    public void Actualizar_cambia_nombre_y_comando()
    {
        var paleta = new PaletaDeComandos();
        var nuevo = paleta.Agregar("Discos", "df -h")!;

        Assert.True(paleta.Actualizar(nuevo with { Nombre = "Discos libres", Comando = "df -hT" }));

        var guardado = paleta.Todos[0];

        Assert.Equal("Discos libres", guardado.Nombre);
        Assert.Equal("df -hT", guardado.Comando);
    }

    // La identidad es propia y no el nombre, justamente para que renombrar no cree un duplicado.
    [Fact]
    public void Renombrar_no_duplica()
    {
        var paleta = new PaletaDeComandos();
        var nuevo = paleta.Agregar("Discos", "df -h")!;

        paleta.Actualizar(nuevo with { Nombre = "Otro nombre" });

        Assert.Equal(1, paleta.Cantidad);
    }

    [Fact]
    public void No_se_puede_actualizar_a_algo_invalido()
    {
        var paleta = new PaletaDeComandos();
        var nuevo = paleta.Agregar("Discos", "df -h")!;

        Assert.False(paleta.Actualizar(nuevo with { Comando = "  " }));
        Assert.Equal("df -h", paleta.Todos[0].Comando);
    }

    [Fact]
    public void Actualizar_algo_que_no_esta_no_lo_agrega()
    {
        var paleta = new PaletaDeComandos();

        Assert.False(paleta.Actualizar(Cmd("Fantasma", "echo x")));
        Assert.Equal(0, paleta.Cantidad);
    }

    [Fact]
    public void Quitar_saca_solo_ese()
    {
        var paleta = new PaletaDeComandos();
        var uno = paleta.Agregar("Discos", "df -h")!;
        paleta.Agregar("Memoria", "free -m");

        Assert.True(paleta.Quitar(uno.Id));
        Assert.Equal(1, paleta.Cantidad);
        Assert.Equal("Memoria", paleta.Todos[0].Nombre);
    }

    [Fact]
    public void Quitar_algo_que_no_esta_no_es_un_error() =>
        Assert.False(new PaletaDeComandos().Quitar(Guid.NewGuid()));
}
