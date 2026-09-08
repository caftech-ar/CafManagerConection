using System.Security.Cryptography;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Infrastructure.Credentials;
using CafManagerConection.Infrastructure.Tests.Database;
using CafManagerConection.UseCases.Credentials;
using Dapper;
using Xunit;

namespace CafManagerConection.Infrastructure.Tests.Credentials;

/// <summary>Los dos modos y nada más: sin clave maestra el secreto va en claro, con clave maestra va cifrado. Contra la base de verdad, porque lo que se prueba incluye la transacción.</summary>
public sealed class VaultTests
{
    private const string Maestra = "Zorro-Verde-2026!";
    private const string OtraMaestra = "Otra-Clave-2026!";
    private const string Secreto = "hunter2-no-compartir";

    private static async Task<(Vault Vault, RepositorioDelVault Repo)> Armar(TempDatabase db)
    {
        await db.CreateInitializer().InitializeAsync();

        var repo = new RepositorioDelVault(db.Factory);
        return (new Vault(repo), repo);
    }

    // Una conexion y una carpeta reales: el secreto es una columna suya, asi que sin fila no hay
    // donde guardarlo.
    private static ReferenciaDeSecreto Conexion(TempDatabase db, string nombre = "Servidor")
    {
        var id = Guid.NewGuid();

        using var cn = db.Factory.Create();
        cn.Execute(
            """
            INSERT INTO connections (id, name, protocol, host, created_at, updated_at)
            VALUES (@id, @nombre, 'Ssh', '192.0.2.1', '2026-09-07', '2026-09-07');
            """,
            new { id = id.ToString("D"), nombre });

        return ReferenciaDeSecreto.DeConexion(id, Protocol.Ssh);
    }

    private static ReferenciaDeSecreto Carpeta(TempDatabase db)
    {
        var id = Guid.NewGuid();

        using var cn = db.Factory.Create();
        cn.Execute(
            """
            INSERT INTO connection_folders (id, name, created_at, updated_at)
            VALUES (@id, 'Producción', '2026-09-07', '2026-09-07');
            INSERT INTO folder_settings (folder_id) VALUES (@id);
            """,
            new { id = id.ToString("D") });

        return ReferenciaDeSecreto.DeCarpeta(id, Protocol.Ssh);
    }

    private static byte[] BytesCrudos(TempDatabase db, ReferenciaDeSecreto referencia)
    {
        using var cn = db.Factory.Create();

        return cn.ExecuteScalar<byte[]>(
            referencia.EsDeCarpeta
                ? "SELECT ssh_secreto FROM folder_settings WHERE folder_id = @id"
                : "SELECT secreto FROM connections WHERE id = @id",
            new { id = referencia.Id.ToString("D") }) ?? [];
    }

    private static string Texto(char[]? secreto) => secreto is null ? "" : new string(secreto);


    [Fact]
    public async Task Sin_clave_maestra_el_secreto_se_lee_sin_preguntar_nada()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);
        var donde = Conexion(db);

        Assert.Equal(ComoAbre.SinClaveMaestra, await vault.ComoAbreAsync());

        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());

        Assert.Equal(Secreto, Texto(await vault.LeerSecretoAsync(donde)));
    }

    [Fact]
    public async Task Sin_clave_maestra_el_secreto_queda_en_claro_en_la_base()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);
        var donde = Conexion(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());

        Assert.Equal(Secreto, System.Text.Encoding.UTF8.GetString(BytesCrudos(db, donde)));
    }

    [Fact]
    public async Task Sin_clave_maestra_otra_instancia_lo_lee_igual()
    {
        using var db = new TempDatabase();
        var (vault, repo) = await Armar(db);
        var donde = Conexion(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());

        var otro = new Vault(repo);
        Assert.Equal(ComoAbre.SinClaveMaestra, await otro.ComoAbreAsync());
        Assert.Equal(Secreto, Texto(await otro.LeerSecretoAsync(donde)));
    }


    [Fact]
    public async Task Con_clave_maestra_el_secreto_no_queda_en_claro_en_la_base()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);
        var donde = Conexion(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        var crudo = System.Text.Encoding.UTF8.GetString(BytesCrudos(db, donde));

        Assert.DoesNotContain(Secreto, crudo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_clave_maestra_otra_instancia_no_lee_hasta_tipearla()
    {
        using var db = new TempDatabase();
        var (vault, repo) = await Armar(db);
        var donde = Conexion(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        var otro = new Vault(repo);

        Assert.Equal(ComoAbre.ConLaClaveMaestra, await otro.ComoAbreAsync());
        await Assert.ThrowsAsync<VaultCerradoException>(() => otro.LeerSecretoAsync(donde));

        Assert.Equal(
            ResultadoDeApertura.Abierto,
            await otro.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));

        Assert.Equal(Secreto, Texto(await otro.LeerSecretoAsync(donde)));
    }

    [Fact]
    public async Task Una_clave_maestra_equivocada_se_informa_como_tal()
    {
        using var db = new TempDatabase();
        var (vault, repo) = await Armar(db);

        await vault.ComoAbreAsync();
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        Assert.Equal(
            ResultadoDeApertura.ClaveIncorrecta,
            await new Vault(repo).AbrirConLaClaveMaestraAsync(OtraMaestra.AsMemory()));
    }

    [Fact]
    public async Task Tipear_una_clave_donde_no_hay_ninguna_lo_dice()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        Assert.Equal(
            ResultadoDeApertura.NoTieneClaveMaestra,
            await vault.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));
    }

    [Fact]
    public async Task Bloquear_saca_la_clave_de_memoria()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);
        var donde = Conexion(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        vault.Bloquear();

        await Assert.ThrowsAsync<VaultCerradoException>(() => vault.LeerSecretoAsync(donde));
    }


    [Fact]
    public async Task Poner_la_clave_maestra_cifra_lo_que_estaba_en_claro()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);
        var conexion = Conexion(db);
        var carpeta = Carpeta(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(conexion, Secreto.AsMemory());
        await vault.GuardarSecretoAsync(carpeta, "otra-clave".AsMemory());

        Assert.True(await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory()));

        Assert.Equal(Secreto, Texto(await vault.LeerSecretoAsync(conexion)));
        Assert.Equal("otra-clave", Texto(await vault.LeerSecretoAsync(carpeta)));

        Assert.DoesNotContain(
            Secreto,
            System.Text.Encoding.UTF8.GetString(BytesCrudos(db, conexion)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quitar_la_clave_maestra_deja_los_secretos_en_claro()
    {
        using var db = new TempDatabase();
        var (vault, repo) = await Armar(db);
        var donde = Conexion(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        Assert.True(await vault.DefinirClaveMaestraAsync(Maestra.AsMemory(), default));

        Assert.Equal(Secreto, System.Text.Encoding.UTF8.GetString(BytesCrudos(db, donde)));
        Assert.Equal(ComoAbre.SinClaveMaestra, await new Vault(repo).ComoAbreAsync());
    }

    [Fact]
    public async Task Cambiar_la_clave_maestra_deja_los_secretos_legibles_con_la_nueva()
    {
        using var db = new TempDatabase();
        var (vault, repo) = await Armar(db);
        var donde = Conexion(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        Assert.True(
            await vault.DefinirClaveMaestraAsync(Maestra.AsMemory(), OtraMaestra.AsMemory()));

        var otro = new Vault(repo);

        Assert.Equal(
            ResultadoDeApertura.ClaveIncorrecta,
            await otro.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));

        Assert.Equal(
            ResultadoDeApertura.Abierto,
            await otro.AbrirConLaClaveMaestraAsync(OtraMaestra.AsMemory()));

        Assert.Equal(Secreto, Texto(await otro.LeerSecretoAsync(donde)));
    }

    [Fact]
    public async Task Cambiar_la_clave_maestra_sin_la_actual_correcta_no_cambia_nada()
    {
        using var db = new TempDatabase();
        var (vault, repo) = await Armar(db);
        var donde = Conexion(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(donde, Secreto.AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        Assert.False(
            await vault.DefinirClaveMaestraAsync(
                "Clave-Que-No-Es-2026!".AsMemory(), OtraMaestra.AsMemory()));

        var otro = new Vault(repo);

        Assert.Equal(
            ResultadoDeApertura.Abierto,
            await otro.AbrirConLaClaveMaestraAsync(Maestra.AsMemory()));

        Assert.Equal(Secreto, Texto(await otro.LeerSecretoAsync(donde)));
    }

    [Fact]
    public async Task Una_clave_maestra_que_no_cumple_la_forma_se_rechaza()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.ComoAbreAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => vault.DefinirClaveMaestraAsync(default, "abc12345".AsMemory()));
    }


    [Fact]
    public async Task Cambiar_una_contrasena_no_toca_el_cifrado_de_las_demas()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);
        var una = Conexion(db, "Una");
        var otra = Conexion(db, "Otra");

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(una, Secreto.AsMemory());
        await vault.GuardarSecretoAsync(otra, "intacta".AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        var antes = BytesCrudos(db, otra);

        await vault.GuardarSecretoAsync(una, "cambiada".AsMemory());

        Assert.Equal(antes, BytesCrudos(db, otra));
        Assert.Equal("intacta", Texto(await vault.LeerSecretoAsync(otra)));
    }

    [Fact]
    public async Task Borrar_una_contrasena_no_toca_el_cifrado_de_las_demas()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);
        var una = Conexion(db, "Una");
        var otra = Conexion(db, "Otra");

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(una, Secreto.AsMemory());
        await vault.GuardarSecretoAsync(otra, "intacta".AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        var antes = BytesCrudos(db, otra);

        await vault.BorrarSecretoAsync(una);

        Assert.Equal(antes, BytesCrudos(db, otra));
        Assert.False(await vault.HaySecretoAsync(una));
        Assert.True(await vault.HaySecretoAsync(otra));
    }

    [Fact]
    public async Task Una_fila_sin_contrasena_devuelve_nulo_y_no_es_un_error()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);

        await vault.ComoAbreAsync();

        Assert.Null(await vault.LeerSecretoAsync(Conexion(db)));
    }

    [Fact]
    public async Task Las_referencias_con_secreto_incluyen_conexiones_y_carpetas()
    {
        using var db = new TempDatabase();
        var (vault, _) = await Armar(db);
        var conexion = Conexion(db);
        var carpeta = Carpeta(db);

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(conexion, Secreto.AsMemory());
        await vault.GuardarSecretoAsync(carpeta, "otra".AsMemory());

        var referencias = await vault.ReferenciasConSecretoAsync();

        Assert.Contains(conexion, referencias);
        Assert.Contains(carpeta, referencias);
        Assert.Equal(2, referencias.Count);
    }


    [Fact]
    public async Task Una_contrasena_cifrada_con_otra_clave_se_informa_sin_tirar_criptografia()
    {
        using var db = new TempDatabase();
        var (vault, repo) = await Armar(db);
        var sana = Conexion(db, "Sana");
        var rota = Conexion(db, "Rota");

        await vault.ComoAbreAsync();
        await vault.GuardarSecretoAsync(sana, Secreto.AsMemory());
        await vault.GuardarSecretoAsync(rota, "la-que-se-rompe".AsMemory());
        await vault.DefinirClaveMaestraAsync(default, Maestra.AsMemory());

        // Se pisa una sola fila con un cifrado hecho con otra clave, como quedaria si el recifrado
        // se hubiera salteado esa fila.
        var ajena = new byte[CifradoDeSecretos.LargoDeLaClave];
        RandomNumberGenerator.Fill(ajena);
        var sobre = CifradoDeSecretos.CifrarTexto(ajena, "lo-que-sea");

        await repo.GuardarSecretoAsync(rota, SecretoGuardado.Cifrado(sobre));

        var ex = await Assert.ThrowsAsync<CredencialIlegibleException>(
            () => vault.LeerSecretoAsync(rota));

        Assert.Equal(rota, ex.Referencia);
        Assert.Contains("volver a cargarla", ex.Message, StringComparison.Ordinal);

        // El vault esta sano: las demas se siguen leyendo.
        Assert.Equal(Secreto, Texto(await vault.LeerSecretoAsync(sana)));
    }
}
