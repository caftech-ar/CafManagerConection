using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

public sealed class PoliticaDeBitacoraTests
{
    private static readonly DateTimeOffset Inicio =
        new(2026, 9, 18, 14, 30, 5, TimeSpan.Zero);

    [Fact]
    public void El_nombre_lleva_la_conexion_y_el_momento()
    {
        var nombre = PoliticaDeBitacora.NombreDeArchivo("prod-db-01", Inicio);

        Assert.StartsWith("prod-db-01-", nombre, StringComparison.Ordinal);
        Assert.EndsWith(".txt", nombre, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_nombre_con_caracteres_invalidos_se_sanea()
    {
        var nombre = PoliticaDeBitacora.NombreDeArchivo("prod/db:01", Inicio);

        Assert.DoesNotContain('/', nombre);
        Assert.DoesNotContain(':', nombre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_nombre_vacio_cae_en_uno_por_omision(string nombre) =>
        Assert.Equal("sesion", PoliticaDeBitacora.Sanear(nombre));

    [Fact]
    public void Dos_sesiones_en_momentos_distintos_dan_archivos_distintos()
    {
        var primera = PoliticaDeBitacora.NombreDeArchivo("srv", Inicio);
        var segunda = PoliticaDeBitacora.NombreDeArchivo("srv", Inicio.AddMinutes(1));

        Assert.NotEqual(primera, segunda);
    }

    [Fact]
    public void Lo_viejo_se_borra()
    {
        var ahora = Inicio.AddDays(40);

        Assert.True(PoliticaDeBitacora.HayQueBorrar(Inicio, ahora, dias: 30));
    }

    [Fact]
    public void Lo_reciente_se_queda()
    {
        var ahora = Inicio.AddDays(10);

        Assert.False(PoliticaDeBitacora.HayQueBorrar(Inicio, ahora, dias: 30));
    }

    [Fact]
    public void Sin_retencion_no_se_borra_nada()
    {
        var ahora = Inicio.AddDays(4000);

        Assert.False(PoliticaDeBitacora.HayQueBorrar(Inicio, ahora, dias: 0));
    }

    [Fact]
    public void La_bitacora_arranca_activa() => Assert.True(AjustesDeBitacora.Default.Activa);

    [Fact]
    public void Los_dias_se_acotan()
    {
        Assert.Equal(
            AjustesDeBitacora.MaximoDeDias,
            new AjustesDeBitacora(DiasQueSeGuardan: 99_999).Normalizados().DiasQueSeGuardan);

        Assert.Equal(
            AjustesDeBitacora.MinimoDeDias,
            new AjustesDeBitacora(DiasQueSeGuardan: -5).Normalizados().DiasQueSeGuardan);
    }

    private static Connection Conexion() =>
        new(Guid.NewGuid(), "prueba", Protocol.Ssh, "servidor");

    [Fact]
    public void Sin_decision_propia_la_conexion_hereda_lo_global()
    {
        var c = Conexion();

        Assert.Null(AjustesReservados.Decision(c, AjustesReservados.BitacoraDeSesion));
        Assert.True(AjustesReservados.Activo(c, AjustesReservados.BitacoraDeSesion, global: true));
        Assert.False(AjustesReservados.Activo(c, AjustesReservados.BitacoraDeSesion, global: false));
    }

    [Fact]
    public void La_conexion_puede_apagar_lo_global()
    {
        var c = Conexion();
        AjustesReservados.FijarDecision(c, AjustesReservados.BitacoraDeSesion, false);

        Assert.False(AjustesReservados.Activo(c, AjustesReservados.BitacoraDeSesion, global: true));
    }

    [Fact]
    public void La_conexion_puede_encender_sobre_lo_global_apagado()
    {
        var c = Conexion();
        AjustesReservados.FijarDecision(c, AjustesReservados.BitacoraDeSesion, true);

        Assert.True(AjustesReservados.Activo(c, AjustesReservados.BitacoraDeSesion, global: false));
    }

    [Fact]
    public void Volver_a_heredar_borra_la_decision_propia()
    {
        var c = Conexion();
        AjustesReservados.FijarDecision(c, AjustesReservados.BitacoraDeSesion, true);
        AjustesReservados.FijarDecision(c, AjustesReservados.BitacoraDeSesion, null);

        Assert.Null(AjustesReservados.Decision(c, AjustesReservados.BitacoraDeSesion));
    }

    [Fact]
    public void El_campo_de_la_bitacora_es_reservado() =>
        Assert.True(AjustesReservados.EsReservado(AjustesReservados.BitacoraDeSesion));

    [Fact]
    public void La_franja_arranca_activa() => Assert.True(AjustesDeFranja.Default.Activa);

    [Fact]
    public void El_intervalo_de_la_franja_se_acota()
    {
        Assert.Equal(
            AjustesDeFranja.MinimoDeSegundos,
            new AjustesDeFranja(Segundos: 0).Normalizados().Segundos);

        Assert.Equal(
            AjustesDeFranja.MaximoDeSegundos,
            new AjustesDeFranja(Segundos: 9_999).Normalizados().Segundos);
    }

    [Fact]
    public void La_franja_hereda_lo_global_mientras_la_conexion_no_decida()
    {
        var c = Conexion();

        Assert.Null(AjustesReservados.Decision(c, AjustesReservados.BarraDeMetricas));
        Assert.True(AjustesReservados.Activo(c, AjustesReservados.BarraDeMetricas, global: true));
        Assert.False(AjustesReservados.Activo(c, AjustesReservados.BarraDeMetricas, global: false));
    }

    [Fact]
    public void La_conexion_puede_apagar_la_franja_global()
    {
        var c = Conexion();
        AjustesReservados.FijarDecision(c, AjustesReservados.BarraDeMetricas, false);

        Assert.False(AjustesReservados.Activo(c, AjustesReservados.BarraDeMetricas, global: true));
    }

    [Fact]
    public void La_conexion_puede_encender_la_franja_sobre_lo_global_apagado()
    {
        var c = Conexion();
        AjustesReservados.FijarDecision(c, AjustesReservados.BarraDeMetricas, true);

        Assert.True(AjustesReservados.Activo(c, AjustesReservados.BarraDeMetricas, global: false));
    }

    [Fact]
    public void Los_dos_ajustes_de_sesion_son_independientes()
    {
        var c = Conexion();
        AjustesReservados.FijarDecision(c, AjustesReservados.BarraDeMetricas, true);
        AjustesReservados.FijarDecision(c, AjustesReservados.BitacoraDeSesion, false);

        Assert.True(AjustesReservados.Activo(c, AjustesReservados.BarraDeMetricas, global: false));
        Assert.False(AjustesReservados.Activo(c, AjustesReservados.BitacoraDeSesion, global: true));
    }

    [Fact]
    public void El_campo_de_la_franja_es_reservado() =>
        Assert.True(AjustesReservados.EsReservado(AjustesReservados.BarraDeMetricas));

    [Fact]
    public void Lo_que_escribe_la_aplicacion_se_reconoce_como_bitacora() =>
        Assert.True(PoliticaDeBitacora.EsBitacora(
            PoliticaDeBitacora.NombreDeArchivo("prod-db-01", Inicio)));

    [Theory]
    [InlineData("notas.txt")]
    [InlineData("presupuesto 2026.txt")]
    [InlineData("srv.log")]
    [InlineData("cmc-20260101-000000.db")]
    [InlineData("srv-sin-sello.txt")]
    [InlineData("")]
    public void Un_archivo_ajeno_no_se_toca(string nombre) =>
        Assert.False(PoliticaDeBitacora.EsBitacora(nombre));

    [Fact]
    public void Una_conexion_con_guiones_y_numeros_sigue_reconociendose() =>
        Assert.True(PoliticaDeBitacora.EsBitacora(
            PoliticaDeBitacora.NombreDeArchivo("srv-020", Inicio)));

    [Fact]
    public void El_host_va_adelante_para_que_agrupe()
    {
        var nombre = PoliticaDeBitacora.NombreDeArchivo("srv-020", Inicio, "192.0.2.30");

        Assert.StartsWith("192.0.2.30@srv-020-", nombre, StringComparison.Ordinal);
    }

    [Fact]
    public void Dos_conexiones_al_mismo_equipo_quedan_juntas()
    {
        var una = PoliticaDeBitacora.NombreDeArchivo("srv-020", Inicio, "192.0.2.30");
        var otra = PoliticaDeBitacora.NombreDeArchivo("srv-020-root", Inicio, "192.0.2.30");

        Assert.StartsWith("192.0.2.30@", una, StringComparison.Ordinal);
        Assert.StartsWith("192.0.2.30@", otra, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_host_el_nombre_arranca_por_la_conexion(string? host)
    {
        var nombre = PoliticaDeBitacora.NombreDeArchivo("srv-020", Inicio, host);

        Assert.StartsWith("srv-020-", nombre, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_host_delante_sigue_reconociendose_como_bitacora() =>
        Assert.True(PoliticaDeBitacora.EsBitacora(
            PoliticaDeBitacora.NombreDeArchivo("srv-020", Inicio, "192.0.2.30")));
    [Fact]
    public void Del_nombre_se_recuperan_host_conexion_y_momento()
    {
        var nombre = PoliticaDeBitacora.NombreDeArchivo("srv-020", Inicio, "192.0.2.30");

        var datos = PoliticaDeBitacora.Leer(nombre);

        Assert.NotNull(datos);
        Assert.Equal("192.0.2.30", datos.Host);
        Assert.Equal("srv-020", datos.Conexion);
        Assert.Equal(Inicio.ToLocalTime().DateTime, datos.Inicio.DateTime);
    }

    [Fact]
    public void Un_host_y_una_conexion_con_guiones_se_separan_igual()
    {
        var nombre = PoliticaDeBitacora.NombreDeArchivo(
            "srv-de-produccion", Inicio, "mi-host-largo");

        var datos = PoliticaDeBitacora.Leer(nombre);

        Assert.NotNull(datos);
        Assert.Equal("mi-host-largo", datos.Host);
        Assert.Equal("srv-de-produccion", datos.Conexion);
    }

    [Fact]
    public void Un_nombre_sin_host_se_lee_igual()
    {
        var datos = PoliticaDeBitacora.Leer(
            PoliticaDeBitacora.NombreDeArchivo("srv-020", Inicio));

        Assert.NotNull(datos);
        Assert.Equal(string.Empty, datos.Host);
        Assert.Equal("srv-020", datos.Conexion);
    }

    [Fact]
    public void De_un_archivo_ajeno_no_se_lee_nada() =>
        Assert.Null(PoliticaDeBitacora.Leer("notas del cliente.txt"));

    [Fact]
    public void El_separador_de_host_no_sobrevive_al_saneado() =>
        Assert.DoesNotContain(
            PoliticaDeBitacora.SeparadorDeHost, PoliticaDeBitacora.Sanear("cuenta@dominio"));
}
