using System.Reflection;
using CafManagerConection.App.ViewModels;
using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Settings;
using CafManagerConection.UseCases.Connections;
using CafManagerConection.UseCases.Inheritance;

namespace CafManagerConection.App.Tests.ViewModels;

public sealed class IconoDelArbolTests
{
    private static NodoArbol Carpeta(string? icono = null, string? color = null) =>
        NodoArbol.Carpeta(new Folder(Guid.NewGuid(), "Producción")
        {
            ClaveDeIcono = icono,
            ClaveDeColor = color,
        });

    private static NodoArbol Conexion(
        Protocol protocolo = Protocol.Ssh, string? icono = null, string? color = null) =>
        NodoArbol.Conectable(new ConnectionSummary(
            Guid.NewGuid(), null, "Apps", protocolo, "192.0.2.1", 22, "root", null, 0,
            ClaveDeColor: color, ClaveDeIcono: icono));

    [Fact]
    public void Una_carpeta_sin_icono_elegido_usa_el_de_la_aplicacion()
    {
        Assert.Equal("IconoCarpeta", Carpeta().ClaveDeIcono);
    }

    [Theory]
    [InlineData(Protocol.Rdp, "IconoRdp")]
    [InlineData(Protocol.Ssh, "IconoSsh")]
    [InlineData(Protocol.Web, "IconoWeb")]
    public void Una_conexion_sin_icono_elegido_usa_el_de_su_protocolo(
        Protocol protocolo, string esperado)
    {
        Assert.Equal(esperado, Conexion(protocolo).ClaveDeIcono);
    }

    [Fact]
    public void El_icono_elegido_le_gana_al_del_protocolo()
    {
        var nodo = Conexion(Protocol.Ssh, icono: "base-de-datos");

        Assert.Equal("IconoBaseDeDatos", nodo.ClaveDeIcono);
    }

    [Fact]
    public void El_icono_elegido_le_gana_al_de_la_carpeta_por_omision()
    {
        Assert.Equal("IconoPanelDocker", Carpeta(icono: "contenedor").ClaveDeIcono);
    }

    [Theory]
    [InlineData("dinosaurio")]
    [InlineData("")]
    public void Una_clave_que_ya_no_esta_en_el_juego_cae_en_el_del_protocolo(string clave)
    {
        Assert.Equal("IconoSsh", Conexion(Protocol.Ssh, icono: clave).ClaveDeIcono);
    }

    [Fact]
    public void Una_conexion_dentro_de_una_carpeta_con_icono_no_toma_el_de_la_carpeta()
    {
        var carpeta = Carpeta(icono: "cortafuegos", color: "rojo");
        var hija = Conexion(Protocol.Rdp);

        carpeta.Agregar(hija);

        Assert.Equal("IconoRdp", hija.ClaveDeIcono);
        Assert.Equal("ProtocoloRdp", hija.ClaveDePincel);
    }

    // Escenario 6 de US3: dos hermanas con icono propio conservan cada una el suyo.
    [Fact]
    public void Dos_conexiones_hermanas_conservan_cada_una_su_icono()
    {
        var carpeta = Carpeta(icono: "carpeta");
        var una = Conexion(Protocol.Ssh, icono: "correo");
        var otra = Conexion(Protocol.Ssh, icono: "respaldo");

        carpeta.Agregar(una);
        carpeta.Agregar(otra);

        Assert.Equal("IconoCorreo", una.ClaveDeIcono);
        Assert.Equal("IconoRespaldo", otra.ClaveDeIcono);
        Assert.Equal("IconoCarpeta", carpeta.ClaveDeIcono);
    }

    [Fact]
    public void El_color_de_la_carpeta_tampoco_baja_a_sus_hijos()
    {
        var carpeta = Carpeta(color: "violeta");
        var hija = Conexion(Protocol.Web);

        carpeta.Agregar(hija);

        Assert.Equal("IconoVioleta", carpeta.ClaveDePincel);
        Assert.Equal("ProtocoloWeb", hija.ClaveDePincel);
    }

    [Fact]
    public void El_icono_y_el_color_son_independientes()
    {
        var nodo = Conexion(Protocol.Ssh, icono: "monitoreo", color: "rosa");

        Assert.Equal("IconoPanelEstado", nodo.ClaveDeIcono);
        Assert.Equal("IconoRosa", nodo.ClaveDePincel);
    }

    // FR-195b: la cascada de herencia no debe llegar nunca al icono ni al color. Si alguien los
    // agrega acá, el hijo empieza a copiar a su carpeta sin que se note en el árbol.
    [Theory]
    [InlineData(typeof(FolderSettings))]
    [InlineData(typeof(EffectiveSettings))]
    public void La_configuracion_heredable_no_conoce_el_icono_ni_el_color(Type heredable)
    {
        var nombres = heredable
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("ClaveDeIcono", nombres);
        Assert.DoesNotContain("ClaveDeColor", nombres);
    }

    [Fact]
    public void Toda_clave_del_juego_resuelve_a_su_recurso()
    {
        foreach (var icono in JuegoDeIconos.Iconos)
        {
            var nodo = Conexion(Protocol.Ssh, icono: icono.Clave);

            Assert.Equal(icono.Recurso, nodo.ClaveDeIcono);
        }
    }
}
