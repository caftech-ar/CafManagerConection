using CafManagerConection.Infrastructure;

namespace CafManagerConection.Infrastructure.Tests;

public sealed class HerramientasExternasTests
{
    private static readonly DestinoRemoto Destino = new("192.0.2.31", 22, "operador");

    [Fact]
    public void Putty_lleva_usuario_host_y_puerto()
    {
        var linea = LineaDeComando.Para(HerramientaExterna.Putty, Destino);

        Assert.Equal("-ssh operador@192.0.2.31 -P 22", linea);
    }

    [Fact]
    public void Putty_siempre_declara_el_protocolo() =>
        Assert.StartsWith(
            "-ssh ",
            LineaDeComando.Para(HerramientaExterna.Putty, Destino),
            StringComparison.Ordinal);

    [Fact]
    public void Putty_sin_usuario_manda_solo_el_host()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.Putty, new DestinoRemoto("servidor", 2222));

        Assert.Equal("-ssh servidor -P 2222", linea);
    }

    [Fact]
    public void Putty_pasa_la_clave_privada_entre_comillas()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.Putty,
            Destino with { RutaDeClave = @"C:\Users\yo\mis llaves\id.ppk" });

        Assert.Contains(@"-i ""C:\Users\yo\mis llaves\id.ppk""", linea, StringComparison.Ordinal);
    }

    [Fact]
    public void FileZilla_recibe_una_direccion_sftp()
    {
        var linea = LineaDeComando.Para(HerramientaExterna.FileZilla, Destino);

        Assert.Equal("\"sftp://operador@192.0.2.31:22\"", linea);
    }

    [Fact]
    public void WinScp_recibe_la_direccion_con_barra_final()
    {
        var linea = LineaDeComando.Para(HerramientaExterna.WinScp, Destino);

        Assert.Equal("\"sftp://operador@192.0.2.31:22/\"", linea);
    }

    [Fact]
    public void Sin_usuario_la_direccion_no_lleva_arroba()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.FileZilla, new DestinoRemoto("servidor", 22));

        Assert.Equal("\"sftp://servidor:22\"", linea);
    }

    [Fact]
    public void El_usuario_se_escapa_dentro_de_la_direccion()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.FileZilla, Destino with { Usuario = @"DOMINIO\operador" });

        Assert.Contains("DOMINIO%5Coperador@", linea, StringComparison.Ordinal);
        Assert.DoesNotContain(@"DOMINIO\operador", linea, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HerramientaExterna.Putty)]
    [InlineData(HerramientaExterna.FileZilla)]
    [InlineData(HerramientaExterna.WinScp)]
    public void Ninguna_linea_lleva_banderas_de_contrasena(HerramientaExterna herramienta)
    {
        var linea = LineaDeComando.Para(herramienta, Destino);

        Assert.DoesNotContain("-pw", linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-pwfile", linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", linea, StringComparison.OrdinalIgnoreCase);

        var arroba = linea.IndexOf('@');

        if (arroba > 0)
        {
            var credencial = linea[..arroba];
            var esquema = credencial.IndexOf("//", StringComparison.Ordinal);

            Assert.DoesNotContain(':', credencial[(esquema + 2)..]);
        }
    }

    [Fact]
    public void El_destino_no_tiene_donde_guardar_una_contrasena()
    {
        var nombres = typeof(DestinoRemoto)
            .GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();

        Assert.DoesNotContain("password", nombres);
        Assert.DoesNotContain("contrasena", nombres);
        Assert.DoesNotContain("secret", nombres);
        Assert.DoesNotContain("secreto", nombres);
    }

    [Fact]
    public void WinScp_recibe_la_clave_privada_despues_de_la_direccion()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.WinScp,
            Destino with { RutaDeClave = @"C:\claves\id.ppk" });

        Assert.Equal(
            @"""sftp://operador@192.0.2.31:22/"" /privatekey=""C:\claves\id.ppk""", linea);
    }

    [Fact]
    public void WinScp_sin_clave_no_menciona_el_parametro()
    {
        var linea = LineaDeComando.Para(HerramientaExterna.WinScp, Destino);

        Assert.DoesNotContain("/privatekey", linea, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WinScp_conserva_entera_una_ruta_de_clave_con_espacios()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.WinScp,
            Destino with { RutaDeClave = @"C:\Users\Mi Usuario\.ssh\id_rsa" });

        Assert.EndsWith(
            @" /privatekey=""C:\Users\Mi Usuario\.ssh\id_rsa""",
            linea,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WinScp_sin_usuario_con_puerto_propio_y_clave()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.WinScp,
            new DestinoRemoto("servidor", 2222, RutaDeClave: @"D:\k\id.ppk"));

        Assert.Equal(@"""sftp://servidor:2222/"" /privatekey=""D:\k\id.ppk""", linea);
    }

    [Fact]
    public void FileZilla_no_recibe_la_clave_privada()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.FileZilla,
            Destino with { RutaDeClave = @"C:\claves\id.ppk" });

        Assert.Equal("\"sftp://operador@192.0.2.31:22\"", linea);
        Assert.DoesNotContain("id.ppk", linea, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileZilla_sin_usuario_con_puerto_propio_y_clave_solo_lleva_el_destino()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.FileZilla,
            new DestinoRemoto("servidor", 2222, RutaDeClave: @"D:\k\id.ppk"));

        Assert.Equal("\"sftp://servidor:2222\"", linea);
    }

    [Fact]
    public void Putty_con_puerto_propio_y_clave_manda_todo_en_orden()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.Putty,
            new DestinoRemoto("servidor", 2222, "operador", @"C:\Users\Mi Usuario\.ssh\id_rsa"));

        Assert.Equal(
            @"-ssh operador@servidor -P 2222 -i ""C:\Users\Mi Usuario\.ssh\id_rsa""", linea);
    }

    [Fact]
    public void Putty_sin_usuario_y_con_clave_manda_host_y_clave()
    {
        var linea = LineaDeComando.Para(
            HerramientaExterna.Putty,
            new DestinoRemoto("servidor", 22, RutaDeClave: @"D:\k\id.ppk"));

        Assert.Equal(@"-ssh servidor -P 22 -i ""D:\k\id.ppk""", linea);
    }

    [Theory]
    [InlineData(HerramientaExterna.Putty)]
    [InlineData(HerramientaExterna.FileZilla)]
    [InlineData(HerramientaExterna.WinScp)]
    public void Con_clave_ninguna_linea_lleva_contrasena_ni_frase_de_paso(
        HerramientaExterna herramienta)
    {
        var linea = LineaDeComando.Para(
            herramienta,
            Destino with { RutaDeClave = @"C:\Users\Mi Usuario\.ssh\id_rsa" });

        Assert.DoesNotContain("-pw", linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-pwfile", linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passphrase", linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretstoredinfiles", linea, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordsfromfiles", linea, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_clave_es_lo_unico_que_el_destino_agrega_a_la_linea()
    {
        var conClave = LineaDeComando.Para(
            HerramientaExterna.WinScp, Destino with { RutaDeClave = @"C:\claves\id.ppk" });

        var sinClave = LineaDeComando.Para(HerramientaExterna.WinScp, Destino);

        Assert.StartsWith(sinClave, conClave, StringComparison.Ordinal);
    }

    [Fact]
    public void Se_elige_la_primera_ruta_que_existe()
    {
        var buscadas = new List<string>();

        var buscador = new BuscadorDeHerramientas(ruta =>
        {
            buscadas.Add(ruta);
            return ruta.Contains("PuTTY", StringComparison.OrdinalIgnoreCase);
        });

        var ruta = buscador.Buscar(HerramientaExterna.Putty);

        Assert.NotNull(ruta);
        Assert.EndsWith(@"PuTTY\putty.exe", ruta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lo_que_no_esta_devuelve_nulo_y_no_lanza() =>
        Assert.Null(new BuscadorDeHerramientas(_ => false).Buscar(HerramientaExterna.FileZilla));

    [Fact]
    public void Se_buscan_las_dos_carpetas_de_programas()
    {
        var candidatas = BuscadorDeHerramientas.Candidatas(HerramientaExterna.Putty).ToList();

        Assert.True(candidatas.Count >= 2, $"Sólo {candidatas.Count} candidata(s)");
        Assert.All(candidatas, c => Assert.EndsWith("putty.exe", c, StringComparison.Ordinal));
    }

    [Fact]
    public void FileZilla_se_busca_con_sus_dos_nombres_de_carpeta()
    {
        var candidatas = BuscadorDeHerramientas
            .Candidatas(HerramientaExterna.FileZilla)
            .ToList();

        Assert.Contains(candidatas, c => c.Contains("FileZilla FTP Client", StringComparison.Ordinal));
        Assert.Contains(candidatas, c => c.EndsWith(@"FileZilla\filezilla.exe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Antes_de_detectar_no_hay_nada()
    {
        var disponibles = new HerramientasDisponibles(new BuscadorDeHerramientas(_ => true));

        Assert.False(disponibles.Listo);
        Assert.False(disponibles.Hay(HerramientaExterna.Putty));
        Assert.Null(disponibles.Ruta(HerramientaExterna.Putty));

        await disponibles.DetectarUnaVezAsync();

        Assert.True(disponibles.Listo);
        Assert.True(disponibles.Hay(HerramientaExterna.Putty));
    }

    [Fact]
    public async Task Detectar_dos_veces_no_vuelve_a_mirar_el_disco()
    {
        var consultas = 0;

        var disponibles = new HerramientasDisponibles(
            new BuscadorDeHerramientas(_ =>
            {
                consultas++;
                return true;
            }));

        await disponibles.DetectarUnaVezAsync();
        var primera = consultas;

        await disponibles.DetectarUnaVezAsync();
        await disponibles.DetectarUnaVezAsync();

        Assert.Equal(primera, consultas);
    }

    [Fact]
    public async Task Pedirla_desde_varios_hilos_la_corre_una_sola_vez()
    {
        var consultas = 0;

        var disponibles = new HerramientasDisponibles(
            new BuscadorDeHerramientas(_ =>
            {
                Interlocked.Increment(ref consultas);
                return false;
            }));

        await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => disponibles.DetectarUnaVezAsync()));

        var unaPasada = 0;
        var referencia = new BuscadorDeHerramientas(_ =>
        {
            unaPasada++;
            return false;
        });

        foreach (var herramienta in Enum.GetValues<HerramientaExterna>())
        {
            referencia.Buscar(herramienta);
        }

        Assert.Equal(unaPasada, consultas);
    }

    [Fact]
    public async Task Solo_se_listan_las_instaladas()
    {
        var disponibles = new HerramientasDisponibles(
            new BuscadorDeHerramientas(r => r.Contains("WinSCP", StringComparison.Ordinal)));

        await disponibles.DetectarUnaVezAsync();

        Assert.Equal([HerramientaExterna.WinScp], disponibles.Instaladas);
    }
}
