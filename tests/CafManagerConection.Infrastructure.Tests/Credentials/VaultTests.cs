using System.Security.Cryptography;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Infrastructure.Credentials;
using CafManagerConection.Infrastructure.Tests.Database;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Credentials;
using Xunit;

namespace CafManagerConection.Infrastructure.Tests.Credentials;

public sealed class VaultTests
{
    private const string Maestra = "Zorro-Verde-2026!";
    private const string OtraMaestra = "Otra-Clave-9182!";
    private const string Secreto = "contrasena-del-servidor-4c81";

    /// <summary>DPAPI de verdad no sirve acá: hay que poder simular «otro usuario de Windows».</summary>
    private sealed class EquipoFalso : IProteccionDeEquipo
    {
        public bool Falla { get; set; }

        public byte[] Proteger(ReadOnlySpan<byte> claro) =>
            [.. claro.ToArray().Select(b => (byte)(b ^ 0x5A))];

        public byte[] Desproteger(ReadOnlySpan<byte> envuelto) =>
            Falla
                ? throw new CryptographicException("otro usuario de Windows")
                : [.. envuelto.ToArray().Select(b => (byte)(b ^ 0x5A))];
    }

    private static async Task<(Vault Vault, EquipoFalso Equipo)> Armar(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();

        var equipo = new EquipoFalso();
        return (new Vault(new RepositorioDelVault(db.Factory), equipo), equipo);
    }

    [Fact]
    public async Task Un_vault_nuevo_esta_sin_crear()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        Assert.Equal(ComoAbre.SinCrear, await vault.ComoAbreAsync());
        Assert.False(vault.EstaAbierto);
    }

    [Fact]
    public async Task Sin_clave_maestra_el_vault_abre_solo()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(ReadOnlyMemory<char>.Empty, recordarEsteEquipo: true);
        vault.Bloquear();

        var otro = new Vault(new RepositorioDelVault(db.Factory), new EquipoFalso());

        Assert.Equal(ComoAbre.Solo, await otro.ComoAbreAsync());
        Assert.True(await otro.AbrirSoloAsync());
        Assert.True(otro.EstaAbierto);
    }

    [Fact]
    public async Task Con_clave_maestra_y_sin_recordar_el_equipo_la_pide()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);

        var otro = new Vault(new RepositorioDelVault(db.Factory), new EquipoFalso());

        Assert.Equal(ComoAbre.ConLaClaveMaestra, await otro.ComoAbreAsync());
        Assert.False(await otro.AbrirSoloAsync());
        Assert.True(await otro.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));
    }

    [Fact]
    public async Task Una_clave_maestra_equivocada_no_abre()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);

        var otro = new Vault(new RepositorioDelVault(db.Factory), new EquipoFalso());

        Assert.False(await otro.AbrirConLaClaveMaestraAsync(OtraMaestra.AsMemory()));
        Assert.False(otro.EstaAbierto);
    }

    [Fact]
    public async Task Sin_clave_maestra_y_sin_recordar_el_equipo_no_se_puede_crear()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => vault.CrearAsync(ReadOnlyMemory<char>.Empty, recordarEsteEquipo: false));
    }

    [Fact]
    public async Task Una_clave_maestra_que_no_cumple_la_politica_se_rechaza()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        var falla = await Assert.ThrowsAsync<ArgumentException>(
            () => vault.CrearAsync("abcdefgh".AsMemory(), recordarEsteEquipo: false));

        Assert.Contains("número", falla.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_secreto_guardado_vuelve_igual()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, Secreto.AsMemory());

        var leida = await vault.LeerCredencialAsync("cmc:ssh:uno");

        Assert.NotNull(leida);
        Assert.Equal("operaciones", leida!.Value.Usuario);
        Assert.Equal(Secreto, new string(leida.Value.Secreto));
    }

    [Fact]
    public async Task El_secreto_no_esta_en_claro_en_la_base()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, Secreto.AsMemory());

        var bytes = await File.ReadAllBytesAsync(db.Paths.DatabasePath);
        var comoTexto = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain(Secreto, comoTexto, StringComparison.Ordinal);
        Assert.DoesNotContain(Maestra, comoTexto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_el_vault_cerrado_no_se_lee_ni_se_guarda()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);
        vault.Bloquear();

        await Assert.ThrowsAsync<VaultCerradoException>(
            () => vault.LeerCredencialAsync("cmc:ssh:uno"));

        await Assert.ThrowsAsync<VaultCerradoException>(
            () => vault.GuardarCredencialAsync("cmc:ssh:uno", "u", null, Secreto.AsMemory()));
    }

    // Sin esto, bloquear no bloquea nada: la aplicacion se desbloquearia sola acto seguido.
    [Fact]
    public async Task Bloquear_a_mano_desarma_el_desbloqueo_automatico()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: true);
        Assert.Equal(ComoAbre.Solo, await vault.ComoAbreAsync());

        vault.Bloquear();

        Assert.Equal(ComoAbre.ConLaClaveMaestra, await vault.ComoAbreAsync());
        Assert.False(await vault.AbrirSoloAsync());
    }

    [Fact]
    public async Task Tras_tipear_la_clave_maestra_el_desbloqueo_automatico_vuelve()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: true);
        vault.Bloquear();

        Assert.True(await vault.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));
        Assert.Equal(ComoAbre.Solo, await vault.ComoAbreAsync());
    }

    [Fact]
    public async Task Si_el_equipo_recordado_falla_se_cae_al_pedido_de_la_clave_maestra()
    {
        using var db = new TempDatabase();
        var (vault, equipo) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: true);
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, Secreto.AsMemory());
        vault.Bloquear();

        equipo.Falla = true;
        var otro = new Vault(new RepositorioDelVault(db.Factory), equipo);

        Assert.False(await otro.AbrirSoloAsync());
        Assert.True(await otro.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));

        var leida = await otro.LeerCredencialAsync("cmc:ssh:uno");
        Assert.Equal(Secreto, new string(leida!.Value.Secreto));
    }

    [Fact]
    public async Task Poner_una_clave_maestra_despues_no_toca_las_credenciales()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(ReadOnlyMemory<char>.Empty, recordarEsteEquipo: true);
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, Secreto.AsMemory());

        await vault.DefinirClaveMaestraAsync(Maestra.AsMemory(), recordarEsteEquipo: true);

        var otro = new Vault(new RepositorioDelVault(db.Factory), new EquipoFalso());
        Assert.True(await otro.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));

        var leida = await otro.LeerCredencialAsync("cmc:ssh:uno");
        Assert.Equal(Secreto, new string(leida!.Value.Secreto));
    }

    [Fact]
    public async Task Cambiar_la_clave_maestra_deja_la_anterior_sin_servir()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, Secreto.AsMemory());

        await vault.DefinirClaveMaestraAsync(OtraMaestra.AsMemory(), recordarEsteEquipo: false);

        var otro = new Vault(new RepositorioDelVault(db.Factory), new EquipoFalso());

        Assert.False(await otro.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));
        Assert.True(await otro.AbrirConLaClaveMaestraAsync(OtraMaestra.AsMemory()));
        Assert.Equal(
            Secreto,
            new string((await otro.LeerCredencialAsync("cmc:ssh:uno"))!.Value.Secreto));
    }

    [Fact]
    public async Task Quitar_la_clave_maestra_deja_el_vault_abriendo_solo()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);
        await vault.DefinirClaveMaestraAsync(ReadOnlyMemory<char>.Empty, recordarEsteEquipo: true);

        var otro = new Vault(new RepositorioDelVault(db.Factory), new EquipoFalso());

        Assert.Equal(ComoAbre.Solo, await otro.ComoAbreAsync());
        Assert.True(await otro.AbrirSoloAsync());
    }

    [Fact]
    public async Task Quitar_la_clave_maestra_sin_recordar_el_equipo_se_rechaza()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);

        await Assert.ThrowsAsync<ArgumentException>(
            () => vault.DefinirClaveMaestraAsync(
                ReadOnlyMemory<char>.Empty, recordarEsteEquipo: false));
    }

    [Fact]
    public async Task Olvidar_el_equipo_sin_clave_maestra_se_rechaza()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(ReadOnlyMemory<char>.Empty, recordarEsteEquipo: true);

        var falla = await Assert.ThrowsAsync<InvalidOperationException>(
            () => vault.OlvidarEsteEquipoAsync());

        Assert.Contains("clave maestra", falla.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Olvidar_el_equipo_con_clave_maestra_hace_que_la_pida()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: true);
        await vault.OlvidarEsteEquipoAsync();

        var otro = new Vault(new RepositorioDelVault(db.Factory), new EquipoFalso());

        Assert.Equal(ComoAbre.ConLaClaveMaestra, await otro.ComoAbreAsync());
        Assert.False(await otro.AbrirSoloAsync());
    }

    [Fact]
    public async Task El_vault_se_abre_en_otra_maquina_con_solo_la_clave_maestra()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: true);
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, Secreto.AsMemory());

        // Otra maquina: el DPAPI de acá no sirve allá, y la base viaja tal cual.
        var otraMaquina = new EquipoFalso { Falla = true };
        var otro = new Vault(new RepositorioDelVault(db.Factory), otraMaquina);

        Assert.False(await otro.AbrirSoloAsync());
        Assert.True(await otro.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));
        Assert.Equal(
            Secreto,
            new string((await otro.LeerCredencialAsync("cmc:ssh:uno"))!.Value.Secreto));
    }

    [Fact]
    public async Task Guardar_dos_veces_la_misma_clave_sobrescribe_y_no_duplica()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, "vieja1!".AsMemory());
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, "nueva2!".AsMemory());

        var leida = await vault.LeerCredencialAsync("cmc:ssh:uno");

        Assert.Equal("nueva2!", new string(leida!.Value.Secreto));

        var claves = await new RepositorioDelVault(db.Factory).ClavesAsync("cmc:");
        Assert.Single(claves);
    }

    [Fact]
    public async Task Una_credencial_que_no_existe_devuelve_null_y_no_es_un_error()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);

        Assert.Null(await vault.LeerCredencialAsync("cmc:ssh:no-existe"));
    }

    [Fact]
    public async Task Existir_y_listar_funcionan_con_el_vault_cerrado()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.CrearAsync(Maestra.AsMemory(), recordarEsteEquipo: false);
        await vault.GuardarCredencialAsync("cmc:ssh:uno", "operaciones", null, Secreto.AsMemory());
        vault.Bloquear();

        var repositorio = new RepositorioDelVault(db.Factory);

        // Saber que hay una credencial guardada no es leerla, y con el vault cerrado la
        // aplicacion tiene que poder mostrar el arbol y decir cuales tienen credencial.
        Assert.True(await repositorio.ExisteCredencialAsync("cmc:ssh:uno"));
        Assert.Single(await repositorio.ClavesAsync("cmc:"));
    }

    [Fact]
    public async Task Un_vault_sin_ninguna_envoltura_no_se_puede_guardar()
    {
        using var db = new TempDatabase();
        await db.CreateInitializer().InitializeAsync();

        var repositorio = new RepositorioDelVault(db.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repositorio.GuardarAsync(
                new FilaDelVault(FilaDelVault.FormatoActual, null, null, null, null, null)));
    }
}
