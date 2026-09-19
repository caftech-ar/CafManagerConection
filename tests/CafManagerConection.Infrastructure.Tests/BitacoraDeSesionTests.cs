using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Bitacora;

namespace CafManagerConection.Infrastructure.Tests;

public sealed class BitacoraDeSesionTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(
        Path.GetTempPath(), $"cmc-bitacora-{Guid.NewGuid():N}");

    private static readonly DateTimeOffset Inicio =
        new(2026, 9, 18, 14, 30, 5, TimeSpan.Zero);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_carpeta))
            {
                Directory.Delete(_carpeta, recursive: true);
            }
        }
        catch (IOException)
        {
            // Carpeta temporal: si Windows la tiene tomada, se la lleva la limpieza del sistema.
        }
    }

    [Fact]
    public void Se_escribe_lo_anotado()
    {
        using (var bitacora = BitacoraDeSesion.Abrir(_carpeta, "srv", Inicio))
        {
            Assert.NotNull(bitacora);
            bitacora.Anotar("root@srv:~# ls");
            bitacora.Anotar("archivo.txt");
        }

        var texto = File.ReadAllText(Path.Combine(
            _carpeta, PoliticaDeBitacora.NombreDeArchivo("srv", Inicio)));

        Assert.Contains("root@srv:~# ls", texto, StringComparison.Ordinal);
        Assert.Contains("archivo.txt", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Volcar_deja_lo_escrito_en_disco_sin_cerrar()
    {
        using var bitacora = BitacoraDeSesion.Abrir(_carpeta, "srv", Inicio);

        Assert.NotNull(bitacora);
        bitacora.Anotar("una linea");
        bitacora.Volcar();

        using var lector = new FileStream(
            bitacora.Ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        Assert.True(lector.Length > 0);
    }

    [Fact]
    public void La_carpeta_se_crea_sola()
    {
        Assert.False(Directory.Exists(_carpeta));

        using var bitacora = BitacoraDeSesion.Abrir(_carpeta, "srv", Inicio);

        Assert.NotNull(bitacora);
        Assert.True(Directory.Exists(_carpeta));
    }

    [Fact]
    public void Una_ruta_imposible_no_lanza()
    {
        var bitacora = BitacoraDeSesion.Abrir("Z:\\no\\existe\\|", "srv", Inicio);

        Assert.Null(bitacora);
    }

    [Fact]
    public void Despues_de_cerrarla_anotar_no_lanza()
    {
        var bitacora = BitacoraDeSesion.Abrir(_carpeta, "srv", Inicio);

        Assert.NotNull(bitacora);
        bitacora.Dispose();

        bitacora.Anotar("esto ya no se escribe");

        Assert.False(bitacora.Activa);
    }

    [Fact]
    public void La_purga_borra_lo_vencido_y_deja_lo_reciente()
    {
        Directory.CreateDirectory(_carpeta);

        var vieja = Path.Combine(
            _carpeta, PoliticaDeBitacora.NombreDeArchivo("vieja", Inicio));

        var nueva = Path.Combine(
            _carpeta, PoliticaDeBitacora.NombreDeArchivo("nueva", Inicio));

        File.WriteAllText(vieja, "x");
        File.WriteAllText(nueva, "x");

        var ahora = DateTimeOffset.Now;
        File.SetLastWriteTimeUtc(vieja, ahora.AddDays(-40).UtcDateTime);
        File.SetLastWriteTimeUtc(nueva, ahora.AddDays(-2).UtcDateTime);

        var borrados = PurgaDeBitacoras.Purgar(_carpeta, dias: 30, ahora);

        Assert.Equal(1, borrados);
        Assert.False(File.Exists(vieja));
        Assert.True(File.Exists(nueva));
    }

    [Fact]
    public void Sin_retencion_la_purga_no_borra_nada()
    {
        Directory.CreateDirectory(_carpeta);

        var archivo = Path.Combine(
            _carpeta, PoliticaDeBitacora.NombreDeArchivo("vieja", Inicio));

        File.WriteAllText(archivo, "x");
        File.SetLastWriteTimeUtc(archivo, DateTime.UtcNow.AddDays(-4000));

        Assert.Equal(0, PurgaDeBitacoras.Purgar(_carpeta, dias: 0, DateTimeOffset.Now));
        Assert.True(File.Exists(archivo));
    }

    [Fact]
    public void La_purga_no_toca_las_copias_de_la_base()
    {
        Directory.CreateDirectory(_carpeta);

        var copia = Path.Combine(_carpeta, "cmc-20260101-000000.db");
        File.WriteAllText(copia, "x");
        File.SetLastWriteTimeUtc(copia, DateTime.UtcNow.AddDays(-400));

        PurgaDeBitacoras.Purgar(_carpeta, dias: 30, DateTimeOffset.Now);

        Assert.True(File.Exists(copia));
    }

    [Fact]
    public void La_purga_no_toca_un_txt_ajeno()
    {
        Directory.CreateDirectory(_carpeta);

        var ajeno = Path.Combine(_carpeta, "notas del cliente.txt");
        File.WriteAllText(ajeno, "x");
        File.SetLastWriteTimeUtc(ajeno, DateTime.UtcNow.AddDays(-400));

        Assert.Equal(0, PurgaDeBitacoras.Purgar(_carpeta, dias: 30, DateTimeOffset.Now));
        Assert.True(File.Exists(ajeno));
    }

    [Fact]
    public void Purgar_una_carpeta_que_no_existe_no_lanza() =>
        Assert.Equal(0, PurgaDeBitacoras.Purgar(_carpeta, dias: 30, DateTimeOffset.Now));
}
