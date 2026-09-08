using CafManagerConection.App.ViewModels;
using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.App.Tests.ViewModels;

public sealed class NodoArbolTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Un_instante_reciente_es_hace_un_momento()
    {
        var cuando = Ahora.AddSeconds(-30);

        Assert.Equal("hace un momento", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }

    [Fact]
    public void Un_solo_minuto_va_en_singular()
    {
        var cuando = Ahora.AddMinutes(-1);

        Assert.Equal("hace 1 minuto", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }

    [Fact]
    public void Varios_minutos_van_en_plural()
    {
        var cuando = Ahora.AddMinutes(-5);

        Assert.Equal("hace 5 minutos", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }

    [Fact]
    public void Varias_horas_del_mismo_dia_se_cuentan_en_horas()
    {
        var cuando = Ahora.AddHours(-3);

        Assert.Equal("hace 3 horas", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }

    [Fact]
    public void Entre_24_y_48_horas_se_dice_ayer()
    {
        var cuando = Ahora.AddHours(-30);

        Assert.Equal("ayer", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }

    [Fact]
    public void Cerca_de_la_medianoche_no_confunde_dos_horas_con_ayer()
    {
        var medianoche = new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);
        var cuando = medianoche.AddHours(-1);
        var ahora = medianoche.AddHours(1);

        Assert.Equal("hace 2 horas", NodoArbol.FormatearUltimaConexion(cuando, ahora));
    }

    [Fact]
    public void Varios_dias_se_cuentan_en_dias()
    {
        var cuando = Ahora.AddDays(-3);

        Assert.Equal("hace 3 días", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }

    [Fact]
    public void Mas_de_una_semana_muestra_la_fecha_sin_anio_si_es_el_mismo()
    {
        var cuando = Ahora.AddDays(-20);

        Assert.Equal("el 08/08", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }

    [Fact]
    public void Mas_de_un_anio_muestra_la_fecha_con_anio()
    {
        var cuando = Ahora.AddYears(-1);

        Assert.Equal("el 28/08/2025", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }

    [Fact]
    public void Un_reloj_desincronizado_hacia_atras_no_muestra_tiempo_negativo()
    {
        var cuando = Ahora.AddMinutes(5);

        Assert.Equal("hace un momento", NodoArbol.FormatearUltimaConexion(cuando, Ahora));
    }
}

public sealed class AjustesDeArbolEnNodoTests : IDisposable
{
    public AjustesDeArbolEnNodoTests()
    {
        NodoArbol.AjusteDeTamano = 0;
        NodoArbol.MuestraServidor = false;
    }

    public void Dispose()
    {
        NodoArbol.AjusteDeTamano = 0;
        NodoArbol.MuestraServidor = false;
    }

    private static ConnectionSummary Conexion(string host) => new(
        Guid.NewGuid(),
        FolderId: null,
        Name: "Servidor de prueba",
        Protocol: Protocol.Ssh,
        Host: host,
        EffectivePort: 22,
        EffectiveUserName: null,
        LastConnectedAt: null,
        SortOrder: 0);

    [Fact]
    public void Sin_ajuste_el_tamano_es_el_de_siempre()
    {
        var nodo = NodoArbol.Conectable(Conexion("192.0.2.5"));

        Assert.Equal(13, nodo.TamanoDeFuente);
        Assert.Equal(12, nodo.TamanoDeFuenteSecundario);
    }

    [Fact]
    public void El_ajuste_positivo_agranda_el_nombre_y_el_texto_secundario_por_igual()
    {
        NodoArbol.AjusteDeTamano = 4;

        var nodo = NodoArbol.Conectable(Conexion("192.0.2.5"));

        Assert.Equal(17, nodo.TamanoDeFuente);
        Assert.Equal(16, nodo.TamanoDeFuenteSecundario);
    }

    [Fact]
    public void El_ajuste_negativo_achica_el_nombre_y_el_texto_secundario_por_igual()
    {
        NodoArbol.AjusteDeTamano = -2;

        var nodo = NodoArbol.Conectable(Conexion("192.0.2.5"));

        Assert.Equal(11, nodo.TamanoDeFuente);
        Assert.Equal(10, nodo.TamanoDeFuenteSecundario);
    }

    [Fact]
    public void Con_el_ajuste_apagado_una_conexion_no_muestra_servidor()
    {
        NodoArbol.MuestraServidor = false;

        var nodo = NodoArbol.Conectable(Conexion("192.0.2.5"));

        Assert.False(nodo.TieneServidor);
        Assert.Null(nodo.Servidor);
    }

    [Fact]
    public void Con_el_ajuste_prendido_una_conexion_muestra_el_host_tal_cual_esta_cargado()
    {
        NodoArbol.MuestraServidor = true;

        var nodo = NodoArbol.Conectable(Conexion("srv-produccion.dominio.local"));

        Assert.True(nodo.TieneServidor);
        Assert.Equal("srv-produccion.dominio.local", nodo.Servidor);
    }

    [Fact]
    public void El_servidor_se_muestra_entre_parentesis_para_ir_pegado_al_nombre()
    {
        NodoArbol.MuestraServidor = true;

        var nodo = NodoArbol.Conectable(Conexion("192.0.2.5"));

        Assert.Equal("(192.0.2.5)", nodo.ServidorEntreParentesis);
    }

    [Fact]
    public void Con_el_ajuste_apagado_no_quedan_parentesis_vacios()
    {
        NodoArbol.MuestraServidor = false;

        var nodo = NodoArbol.Conectable(Conexion("192.0.2.5"));

        Assert.Null(nodo.ServidorEntreParentesis);
    }

    [Fact]
    public void Con_el_ajuste_prendido_una_ip_se_muestra_tal_cual_sin_resolverla()
    {
        NodoArbol.MuestraServidor = true;

        var nodo = NodoArbol.Conectable(Conexion("192.0.2.50"));

        Assert.Equal("192.0.2.50", nodo.Servidor);
    }

    [Fact]
    public void Una_carpeta_nunca_muestra_servidor_aunque_el_ajuste_este_prendido()
    {
        NodoArbol.MuestraServidor = true;

        var carpeta = NodoArbol.Carpeta(new Folder(Guid.NewGuid(), "Producción"));

        Assert.False(carpeta.TieneServidor);
        Assert.Null(carpeta.Servidor);
    }
}
