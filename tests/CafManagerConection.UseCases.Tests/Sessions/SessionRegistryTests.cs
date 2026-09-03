using CafManagerConection.Domain.Sessions;
using CafManagerConection.UseCases.Sessions;

namespace CafManagerConection.UseCases.Tests.Sessions;

// Antes esta información se deducía recorriendo la tira de pestañas de la ventana principal, y
// no había forma de probarla sin abrir una ventana.
public sealed class SessionRegistryTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static SessionRegistry Registro() => new();

    [Fact]
    public void Un_registro_nuevo_esta_vacio()
    {
        var r = Registro();

        Assert.Empty(r.ActiveSessions);
        Assert.Equal(0, r.Count);
        Assert.Equal("Sin sesiones abiertas", r.Resumen);
    }

    [Fact]
    public void Una_sesion_registrada_aparece_en_la_cuenta()
    {
        var r = Registro();
        var conexion = Guid.NewGuid();

        r.Register(Guid.NewGuid(), conexion, "Aplicaciones", T0);

        Assert.Equal(1, r.Count);
        Assert.Equal(1, r.CountForConnection(conexion));
    }

    [Fact]
    public void Dos_sesiones_de_la_misma_conexion_se_cuentan_las_dos()
    {
        var r = Registro();
        var conexion = Guid.NewGuid();

        r.Register(Guid.NewGuid(), conexion, "Aplicaciones", T0);
        r.Register(Guid.NewGuid(), conexion, "Aplicaciones", T0.AddSeconds(1));

        Assert.Equal(2, r.CountForConnection(conexion));
    }

    [Fact]
    public void Las_sesiones_de_otra_conexion_no_se_cuentan()
    {
        var r = Registro();
        var conexion = Guid.NewGuid();

        r.Register(Guid.NewGuid(), conexion, "Aplicaciones", T0);
        r.Register(Guid.NewGuid(), Guid.NewGuid(), "Otro", T0);

        Assert.Equal(1, r.CountForConnection(conexion));
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void La_primera_sesion_de_una_conexion_es_la_mas_vieja()
    {
        var r = Registro();
        var conexion = Guid.NewGuid();
        var vieja = Guid.NewGuid();

        r.Register(vieja, conexion, "Aplicaciones", T0);
        r.Register(Guid.NewGuid(), conexion, "Aplicaciones", T0.AddMinutes(5));

        Assert.Equal(vieja, r.FirstForConnection(conexion)!.SessionId);
    }

    [Fact]
    public void Sin_sesiones_de_esa_conexion_no_hay_primera()
    {
        Assert.Null(Registro().FirstForConnection(Guid.NewGuid()));
    }

    [Fact]
    public void Cerrar_una_sesion_la_saca_de_la_cuenta()
    {
        var r = Registro();
        var sesion = Guid.NewGuid();
        r.Register(sesion, Guid.NewGuid(), "Aplicaciones", T0);

        r.Unregister(sesion);

        Assert.Equal(0, r.Count);
    }

    [Fact]
    public void Cerrar_una_sesion_que_no_existe_no_es_un_error()
    {
        var r = Registro();

        r.Unregister(Guid.NewGuid());

        Assert.Equal(0, r.Count);
    }

    [Fact]
    public void El_estado_de_una_sesion_se_actualiza()
    {
        var r = Registro();
        var sesion = Guid.NewGuid();
        r.Register(sesion, Guid.NewGuid(), "Aplicaciones", T0);

        r.UpdateState(sesion, SessionState.Connected);

        Assert.Equal(SessionState.Connected, r.ActiveSessions[0].State);
    }

    [Fact]
    public void Un_estado_que_llega_despues_de_cerrar_no_resucita_la_sesion()
    {
        // Volver a crear la sesión dejaría una sesión fantasma en la cuenta que nadie podría
        // cerrar: el estado viene del hilo de red y puede cruzarse con el cierre.
        var r = Registro();
        var sesion = Guid.NewGuid();
        r.Register(sesion, Guid.NewGuid(), "Aplicaciones", T0);
        r.Unregister(sesion);

        r.UpdateState(sesion, SessionState.Connected);

        Assert.Equal(0, r.Count);
    }

    [Fact]
    public void Las_sesiones_salen_en_el_orden_en_que_se_abrieron()
    {
        var r = Registro();
        var primera = Guid.NewGuid();
        var segunda = Guid.NewGuid();

        r.Register(segunda, Guid.NewGuid(), "Segunda", T0.AddMinutes(1));
        r.Register(primera, Guid.NewGuid(), "Primera", T0);

        Assert.Equal([primera, segunda], r.ActiveSessions.Select(s => s.SessionId));
    }

    [Theory]
    [InlineData(0, "Sin sesiones abiertas")]
    [InlineData(1, "1 sesión abierta")]
    [InlineData(2, "2 sesiones abiertas")]
    public void El_resumen_concuerda_en_numero(int cuantas, string esperado)
    {
        var r = Registro();

        for (var i = 0; i < cuantas; i++)
        {
            r.Register(Guid.NewGuid(), Guid.NewGuid(), "S", T0.AddSeconds(i));
        }

        Assert.Equal(esperado, r.Resumen);
    }

    [Fact]
    public void Cada_cambio_avisa_una_sola_vez()
    {
        // La barra de estado se redibuja con cada aviso: avisar cuando no cambió nada es ruido.
        var r = Registro();
        var avisos = 0;
        r.Changed += (_, _) => avisos++;

        var sesion = Guid.NewGuid();
        r.Register(sesion, Guid.NewGuid(), "Aplicaciones", T0);
        r.UpdateState(sesion, SessionState.Connected);
        r.Unregister(sesion);
        r.Unregister(sesion);

        Assert.Equal(3, avisos);
    }

    [Fact]
    public void Vaciar_el_registro_deja_la_cuenta_en_cero()
    {
        var r = Registro();
        r.Register(Guid.NewGuid(), Guid.NewGuid(), "A", T0);
        r.Register(Guid.NewGuid(), Guid.NewGuid(), "B", T0);

        r.Clear();

        Assert.Equal(0, r.Count);
    }
}
