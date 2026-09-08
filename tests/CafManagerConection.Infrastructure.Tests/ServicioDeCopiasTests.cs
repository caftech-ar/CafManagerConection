using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.Infrastructure.Database;
using Microsoft.Data.Sqlite;

namespace CafManagerConection.Infrastructure.Tests;

public sealed class ServicioDeCopiasTests : IDisposable
{
    private readonly string _raiz;
    private readonly AppPaths _rutas;
    private readonly ServicioDeCopias _servicio;

    public ServicioDeCopiasTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "cmc-copias-" + Guid.NewGuid().ToString("N"));
        _rutas = new AppPaths(_raiz);
        _rutas.EnsureCreated();

        CrearBase(_rutas.DatabasePath, "servidor-uno");
        _servicio = new ServicioDeCopias(_rutas);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_raiz, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void CrearBase(string ruta, string valor)
    {
        using var cn = new SqliteConnection($"Data Source={ruta}");
        cn.Open();

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS prueba(v TEXT); INSERT INTO prueba VALUES($v);";
        cmd.Parameters.AddWithValue("$v", valor);
        cmd.ExecuteNonQuery();
    }

    private static IReadOnlyList<string> LeerBase(string ruta)
    {
        using var cn = new SqliteConnection($"Data Source={ruta};Mode=ReadOnly");
        cn.Open();

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT v FROM prueba ORDER BY rowid";

        var valores = new List<string>();
        using var lector = cmd.ExecuteReader();

        while (lector.Read())
        {
            valores.Add(lector.GetString(0));
        }

        return valores;
    }

    private static AjustesDeCopia Ajustes(int cuantas = 10) =>
        AjustesDeCopia.Default with { CuantasGuardar = cuantas };

    [Fact]
    public void Lo_exportado_es_una_base_que_abre_y_trae_los_datos()
    {
        var destino = Path.Combine(_raiz, "exportada.db");

        _servicio.Exportar(destino);

        Assert.True(File.Exists(destino));
        Assert.Equal(["servidor-uno"], LeerBase(destino));
    }

    [Fact]
    public void Exportar_crea_la_carpeta_que_falte()
    {
        var destino = Path.Combine(_raiz, "una", "carpeta", "nueva", "export.db");

        _servicio.Exportar(destino);

        Assert.True(File.Exists(destino));
    }

    [Fact]
    public void La_copia_es_del_momento_en_que_se_hizo()
    {
        var destino = Path.Combine(_raiz, "antes.db");
        _servicio.Exportar(destino);

        CrearBase(_rutas.DatabasePath, "servidor-dos");

        Assert.Equal(["servidor-uno"], LeerBase(destino));
        Assert.Equal(["servidor-uno", "servidor-dos"], LeerBase(_rutas.DatabasePath));
    }

    [Fact]
    public void La_primera_vez_se_copia()
    {
        var r = _servicio.CopiarSiCorresponde(Ajustes(), DateTimeOffset.Now);

        Assert.True(r.Hecha);
        Assert.NotNull(r.Ruta);
        Assert.True(File.Exists(r.Ruta));
    }

    [Fact]
    public void Desactivadas_no_copia_nada()
    {
        var r = _servicio.CopiarSiCorresponde(
            Ajustes() with { Activas = false }, DateTimeOffset.Now);

        Assert.False(r.Hecha);
        Assert.Empty(_servicio.Listar(Ajustes()));
    }

    [Fact]
    public void Dos_arranques_el_mismo_dia_dejan_una_sola()
    {
        var ahora = DateTimeOffset.Now;

        _servicio.CopiarSiCorresponde(Ajustes(), ahora);
        var segunda = _servicio.CopiarSiCorresponde(Ajustes(), ahora.AddHours(2));

        Assert.False(segunda.Hecha);
        Assert.Single(_servicio.Listar(Ajustes()));
    }

    [Fact]
    public void Al_dia_siguiente_sin_cambios_no_copia()
    {
        var ayer = DateTimeOffset.Now.AddDays(-1);

        _servicio.CopiarSiCorresponde(Ajustes(), ayer);
        var hoy = _servicio.CopiarSiCorresponde(Ajustes(), DateTimeOffset.Now);

        Assert.False(hoy.Hecha);
        Assert.Single(_servicio.Listar(Ajustes()));
    }

    [Fact]
    public void Al_dia_siguiente_con_cambios_si_copia()
    {
        _servicio.CopiarSiCorresponde(Ajustes(), DateTimeOffset.Now.AddDays(-1));

        CrearBase(_rutas.DatabasePath, "servidor-dos");

        var hoy = _servicio.CopiarSiCorresponde(Ajustes(), DateTimeOffset.Now);

        Assert.True(hoy.Hecha);
        Assert.Equal(2, _servicio.Listar(Ajustes()).Count);
    }

    [Fact]
    public void Copiar_ahora_no_pregunta_nada()
    {
        var ahora = DateTimeOffset.Now;

        _servicio.CopiarSiCorresponde(Ajustes(), ahora);
        var forzada = _servicio.CopiarAhora(Ajustes(), ahora.AddSeconds(5));

        Assert.True(forzada.Hecha);
        Assert.Equal(2, _servicio.Listar(Ajustes()).Count);
    }

    [Fact]
    public void Se_guardan_las_ultimas_y_se_borran_las_viejas()
    {
        var ajustes = Ajustes(3);

        for (var i = 10; i >= 1; i--)
        {
            CrearBase(_rutas.DatabasePath, $"cambio-{i}");
            _servicio.CopiarAhora(ajustes, DateTimeOffset.Now.AddDays(-i));
        }

        var quedan = _servicio.Listar(ajustes);

        Assert.Equal(3, quedan.Count);
        Assert.All(quedan, c => Assert.True(c.Momento >= DateTimeOffset.Now.AddDays(-4)));
    }

    [Fact]
    public void La_rotacion_no_toca_archivos_ajenos()
    {
        var ajustes = Ajustes(2);
        var carpeta = _servicio.CarpetaDe(ajustes);

        Directory.CreateDirectory(carpeta);

        var ajeno = Path.Combine(carpeta, "presupuesto-2026.xlsx");
        var pareceCopia = Path.Combine(carpeta, "cmc-de-otro-programa.db");

        File.WriteAllText(ajeno, "no me borres");
        File.WriteAllText(pareceCopia, "yo tampoco");

        for (var i = 5; i >= 1; i--)
        {
            CrearBase(_rutas.DatabasePath, $"cambio-{i}");
            _servicio.CopiarAhora(ajustes, DateTimeOffset.Now.AddDays(-i));
        }

        Assert.True(File.Exists(ajeno));
        Assert.True(File.Exists(pareceCopia));
        Assert.Equal(2, _servicio.Listar(ajustes).Count);
    }

    [Fact]
    public void Se_pueden_guardar_en_una_carpeta_elegida()
    {
        var elegida = Path.Combine(_raiz, "mi-onedrive");
        var ajustes = Ajustes() with { Carpeta = elegida };

        var r = _servicio.CopiarAhora(ajustes, DateTimeOffset.Now);

        Assert.True(r.Hecha);
        Assert.StartsWith(elegida, r.Ruta!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Una_carpeta_imposible_no_lanza()
    {
        var ajustes = Ajustes() with { Carpeta = "Z:\\no-existe\\ni-va-a-existir" };

        var r = _servicio.CopiarSiCorresponde(ajustes, DateTimeOffset.Now);

        Assert.False(r.Hecha);
        Assert.NotNull(r.Motivo);
    }

    [Fact]
    public void Listar_una_carpeta_que_no_existe_da_vacio() =>
        Assert.Empty(_servicio.Listar(Ajustes() with { Carpeta = Path.Combine(_raiz, "nada") }));

    [Fact]
    public void La_huella_cambia_cuando_cambia_la_base()
    {
        var antes = _servicio.Huella();

        CrearBase(_rutas.DatabasePath, "otro-servidor");

        Assert.NotEqual(antes, _servicio.Huella());
    }

    [Fact]
    public void La_huella_no_cambia_sola()
    {
        var a = _servicio.Huella();
        var b = _servicio.Huella();

        Assert.Equal(a, b);
    }
}
