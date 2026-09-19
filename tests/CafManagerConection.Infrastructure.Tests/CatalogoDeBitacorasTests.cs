using CafManagerConection.Domain.Settings;
using CafManagerConection.Infrastructure.Bitacora;

namespace CafManagerConection.Infrastructure.Tests;

public sealed class CatalogoDeBitacorasTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(
        Path.GetTempPath(), $"cmc-catalogo-{Guid.NewGuid():N}");

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
            // Carpeta temporal: se la lleva la limpieza del sistema.
        }
    }

    private string Escribir(string conexion, DateTimeOffset inicio, string host, string texto = "x")
    {
        Directory.CreateDirectory(_carpeta);

        var ruta = Path.Combine(
            _carpeta, PoliticaDeBitacora.NombreDeArchivo(conexion, inicio, host));

        File.WriteAllText(ruta, texto);

        return ruta;
    }

    [Fact]
    public void Se_lee_conexion_host_y_momento_del_nombre()
    {
        Escribir("srv-020", Inicio, "192.0.2.30");

        var bitacora = Assert.Single(CatalogoDeBitacoras.Listar(_carpeta));

        Assert.Equal("srv-020", bitacora.Conexion);
        Assert.Equal("192.0.2.30", bitacora.Host);
        Assert.Equal(Inicio.ToLocalTime().DateTime, bitacora.Inicio.DateTime);
    }

    [Fact]
    public void Un_host_con_guiones_no_se_confunde_con_la_conexion()
    {
        Escribir("srv-de-produccion", Inicio, "mi-host-largo");

        var bitacora = Assert.Single(CatalogoDeBitacoras.Listar(_carpeta));

        Assert.Equal("mi-host-largo", bitacora.Host);
        Assert.Equal("srv-de-produccion", bitacora.Conexion);
    }

    [Fact]
    public void Se_listan_de_la_mas_reciente_a_la_mas_vieja()
    {
        Escribir("srv", Inicio, "192.0.2.30");
        Escribir("srv", Inicio.AddHours(2), "192.0.2.30");
        Escribir("srv", Inicio.AddHours(1), "192.0.2.30");

        var listadas = CatalogoDeBitacoras.Listar(_carpeta);

        Assert.Equal(3, listadas.Count);
        Assert.True(listadas[0].Inicio > listadas[1].Inicio);
        Assert.True(listadas[1].Inicio > listadas[2].Inicio);
    }

    [Fact]
    public void Lo_que_no_es_bitacora_no_se_lista()
    {
        Directory.CreateDirectory(_carpeta);
        File.WriteAllText(Path.Combine(_carpeta, "notas del cliente.txt"), "x");

        Assert.Empty(CatalogoDeBitacoras.Listar(_carpeta));
    }

    [Fact]
    public void Una_carpeta_que_no_existe_devuelve_vacio() =>
        Assert.Empty(CatalogoDeBitacoras.Listar(_carpeta));

    [Fact]
    public void Se_informa_el_tamano()
    {
        Escribir("srv", Inicio, "192.0.2.30", new string('x', 500));

        Assert.Equal(500, Assert.Single(CatalogoDeBitacoras.Listar(_carpeta)).Bytes);
    }

    [Fact]
    public async Task El_lector_trae_el_final_del_archivo()
    {
        var lineas = string.Join(Environment.NewLine, Enumerable.Range(1, 5000).Select(i => $"linea{i}"));
        var ruta = Escribir("srv", Inicio, "192.0.2.30", lineas);

        var (texto, _) = await LectorDeBitacora.LeerHaciaAtrasAsync(
            ruta, LectorDeBitacora.Largo(ruta), bytes: 2048);

        Assert.Contains("linea5000", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("linea1\r", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_lector_no_corta_una_linea_por_la_mitad()
    {
        var lineas = string.Join(Environment.NewLine, Enumerable.Range(1, 5000).Select(i => $"linea{i}"));
        var ruta = Escribir("srv", Inicio, "192.0.2.30", lineas);

        var (texto, desde) = await LectorDeBitacora.LeerHaciaAtrasAsync(
            ruta, LectorDeBitacora.Largo(ruta), bytes: 2048);

        Assert.True(desde > 0, "Debería quedar contenido hacia atrás.");
        Assert.StartsWith("linea", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leyendo_hacia_atras_se_llega_al_principio()
    {
        var lineas = string.Join(Environment.NewLine, Enumerable.Range(1, 500).Select(i => $"linea{i}"));
        var ruta = Escribir("srv", Inicio, "192.0.2.30", lineas);

        var desde = LectorDeBitacora.Largo(ruta);
        var vueltas = 0;

        while (desde > 0 && vueltas++ < 100)
        {
            (_, desde) = await LectorDeBitacora.LeerHaciaAtrasAsync(ruta, desde, bytes: 512);
        }

        Assert.Equal(0, desde);
    }
}
