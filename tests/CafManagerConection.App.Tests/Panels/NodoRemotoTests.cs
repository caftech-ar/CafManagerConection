using CafManagerConection.App.Panels;

namespace CafManagerConection.App.Tests.Panels;

public sealed class NodoRemotoTests
{
    private static NodoRemoto Carpeta(string ruta = "/var") =>
        new(ruta[(ruta.LastIndexOf('/') + 1)..], ruta, esCarpeta: true);

    private static NodoRemoto Archivo(string ruta = "/var/log/syslog") =>
        new(ruta[(ruta.LastIndexOf('/') + 1)..], ruta, esCarpeta: false);

    [Fact]
    public void Una_carpeta_sin_desplegar_muestra_el_expansor_con_un_marcador()
    {
        var carpeta = Carpeta();

        Assert.True(carpeta.CargaPendiente);
        Assert.True(Assert.Single(carpeta.Hijos).EsMarcador);
    }

    [Fact]
    public void Un_archivo_no_tiene_hijos_ni_carga_pendiente()
    {
        var archivo = Archivo();

        Assert.False(archivo.CargaPendiente);
        Assert.Empty(archivo.Hijos);
    }

    [Fact]
    public void Una_carpeta_ya_cargada_no_vuelve_a_leer_el_servidor_al_replegarse()
    {
        var carpeta = Carpeta();
        var pedidos = 0;
        carpeta.SolicitaCarga += (_, _) => pedidos++;

        carpeta.Expandido = true;
        carpeta.Completar([Archivo("/var/uno")]);
        carpeta.Expandido = false;
        carpeta.Expandido = true;

        Assert.Equal(1, pedidos);
    }

    [Fact]
    public void Una_carpeta_cuyo_nivel_nunca_llego_vuelve_a_pedirlo()
    {
        var carpeta = Carpeta();
        var pedidos = 0;
        carpeta.SolicitaCarga += (_, _) => pedidos++;

        carpeta.Expandido = true;
        carpeta.Expandido = false;
        carpeta.Expandido = true;

        Assert.Equal(2, pedidos);
    }

    [Fact]
    public void Un_archivo_nunca_pide_carga_aunque_lo_marquen_expandido()
    {
        var archivo = Archivo();
        var pedidos = 0;
        archivo.SolicitaCarga += (_, _) => pedidos++;

        archivo.Expandido = true;

        Assert.Equal(0, pedidos);
    }

    [Fact]
    public void Completar_reemplaza_el_marcador_por_los_hijos_y_los_encadena_al_padre()
    {
        var carpeta = Carpeta("/var/log");
        var hijo = Archivo("/var/log/syslog");

        carpeta.Completar([hijo]);

        Assert.Same(hijo, Assert.Single(carpeta.Hijos));
        Assert.Same(carpeta, hijo.Padre);
        Assert.False(carpeta.CargaPendiente);
    }

    [Fact]
    public void Una_carpeta_vacia_queda_sin_hijos_y_sin_marcador()
    {
        var carpeta = Carpeta();

        carpeta.Completar([]);

        Assert.Empty(carpeta.Hijos);
    }

    [Fact]
    public void Recargar_una_carpeta_desplegada_vuelve_a_pedir_su_nivel()
    {
        var carpeta = Carpeta();
        var pedidos = 0;
        carpeta.SolicitaCarga += (_, _) => pedidos++;

        carpeta.Expandido = true;
        carpeta.Completar([Archivo("/var/uno")]);
        carpeta.Recargar();

        Assert.Equal(2, pedidos);
        Assert.True(carpeta.CargaPendiente);
    }

    [Fact]
    public void Recargar_una_carpeta_cerrada_no_pide_nada_hasta_que_se_despliegue()
    {
        var carpeta = Carpeta();
        var pedidos = 0;
        carpeta.SolicitaCarga += (_, _) => pedidos++;

        carpeta.Recargar();

        Assert.Equal(0, pedidos);

        carpeta.Expandido = true;

        Assert.Equal(1, pedidos);
    }

    [Fact]
    public void La_carpeta_de_destino_de_una_carpeta_es_ella_misma()
    {
        Assert.Equal("/var/log", Carpeta("/var/log").CarpetaDeDestino);
    }

    [Fact]
    public void La_carpeta_de_destino_de_un_archivo_es_la_que_lo_contiene()
    {
        Assert.Equal("/var/log", Archivo("/var/log/syslog").CarpetaDeDestino);
    }

    [Fact]
    public void Un_marcador_no_ofrece_ninguna_ruta_para_transferir()
    {
        var carpeta = Carpeta();
        var marcador = Assert.Single(carpeta.Hijos);

        Assert.True(marcador.EsMarcador);
        Assert.False(marcador.CargaPendiente);
        Assert.Empty(marcador.Hijos);
    }
}
