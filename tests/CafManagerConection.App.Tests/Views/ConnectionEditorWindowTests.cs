using System.Linq;
using CafManagerConection.App.Views;
using CafManagerConection.Domain.Connections;

namespace CafManagerConection.App.Tests.Views;

public sealed class ConnectionEditorWindowTests
{
    [Fact]
    public void El_indice_0_es_automatico_y_no_un_metodo_propio()
    {
        Assert.Null(ConnectionEditorWindow.MetodoAuthDeIndice(0));
    }

    [Fact]
    public void El_indice_1_es_contrasena()
    {
        Assert.Equal(SshAuthMethod.Password, ConnectionEditorWindow.MetodoAuthDeIndice(1));
    }

    [Fact]
    public void El_indice_2_es_clave_privada()
    {
        Assert.Equal(SshAuthMethod.PrivateKey, ConnectionEditorWindow.MetodoAuthDeIndice(2));
    }

    [Fact]
    public void Un_indice_fuera_de_rango_tambien_es_automatico()
    {
        Assert.Null(ConnectionEditorWindow.MetodoAuthDeIndice(-1));
        Assert.Null(ConnectionEditorWindow.MetodoAuthDeIndice(99));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(SshAuthMethod.Password, 1)]
    [InlineData(SshAuthMethod.PrivateKey, 2)]
    public void IndiceDeMetodoAuth_es_el_camino_inverso_de_MetodoAuthDeIndice(
        SshAuthMethod? metodo, int indiceEsperado)
    {
        var indice = ConnectionEditorWindow.IndiceDeMetodoAuth(metodo);

        Assert.Equal(indiceEsperado, indice);

        Assert.Equal(metodo, ConnectionEditorWindow.MetodoAuthDeIndice(indice));
    }

    [Fact]
    public void Un_campo_vacio_de_keep_alive_es_valido_y_significa_heredar()
    {
        var valido = ConnectionEditorWindow.ValidarKeepAliveSegundos(
            string.Empty, out var valor, out var error);

        Assert.True(valido);
        Assert.Null(valor);
        Assert.Null(error);
    }

    [Fact]
    public void Solo_espacios_en_keep_alive_tambien_significa_heredar()
    {
        var valido = ConnectionEditorWindow.ValidarKeepAliveSegundos(
            "   ", out var valor, out _);

        Assert.True(valido);
        Assert.Null(valor);
    }

    [Fact]
    public void Cero_es_un_valor_propio_valido_y_no_se_confunde_con_vacio()
    {
        var valido = ConnectionEditorWindow.ValidarKeepAliveSegundos(
            "0", out var valor, out var error);

        Assert.True(valido);
        Assert.Equal(0, valor);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("60")]
    [InlineData("  60  ")]
    [InlineData("86400")]
    public void Un_numero_razonable_de_segundos_se_acepta(string texto)
    {
        Assert.True(ConnectionEditorWindow.ValidarKeepAliveSegundos(texto, out var valor, out _));
        Assert.NotNull(valor);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("86401")]
    [InlineData("no es un número")]
    [InlineData("3.5")]
    public void Un_valor_fuera_de_rango_o_no_numerico_se_rechaza_con_mensaje(string texto)
    {
        var valido = ConnectionEditorWindow.ValidarKeepAliveSegundos(
            texto, out var valor, out var error);

        Assert.False(valido);
        Assert.Null(valor);
        Assert.False(string.IsNullOrWhiteSpace(error));

        Assert.Contains("0 desactiva", error);
    }

    [Fact]
    public void Una_fila_sin_nombre_se_descarta_sin_bloquear_el_guardado()
    {
        var filas = new[]
        {
            new ConnectionEditorWindow.CampoPropio { Nombre = "", Valor = "algo" },
            new ConnectionEditorWindow.CampoPropio { Nombre = "   ", Valor = "otro" },
        };

        Assert.Empty(ConnectionEditorWindow.CamposPropiosValidos(filas));
    }

    [Fact]
    public void Nombre_y_valor_se_recortan_de_espacios()
    {
        var filas = new[]
        {
            new ConnectionEditorWindow.CampoPropio { Nombre = "  clave  ", Valor = "  valor  " },
        };

        var resultado = Assert.Single(ConnectionEditorWindow.CamposPropiosValidos(filas));

        Assert.Equal("clave", resultado.Nombre);
        Assert.Equal("valor", resultado.Valor);
    }

    [Fact]
    public void Un_valor_vacio_es_valido_mientras_el_nombre_este_puesto()
    {
        var filas = new[]
        {
            new ConnectionEditorWindow.CampoPropio { Nombre = "clave", Valor = "" },
        };

        var resultado = Assert.Single(ConnectionEditorWindow.CamposPropiosValidos(filas));

        Assert.Equal("clave", resultado.Nombre);
        Assert.Equal(string.Empty, resultado.Valor);
    }

    [Fact]
    public void Se_conserva_el_orden_y_se_saltean_solo_las_filas_sin_nombre()
    {
        var filas = new[]
        {
            new ConnectionEditorWindow.CampoPropio { Nombre = "primero", Valor = "1" },
            new ConnectionEditorWindow.CampoPropio { Nombre = "", Valor = "se descarta" },
            new ConnectionEditorWindow.CampoPropio { Nombre = "segundo", Valor = "2" },
        };

        var resultado = ConnectionEditorWindow.CamposPropiosValidos(filas).ToList();

        Assert.Equal(2, resultado.Count);
        Assert.Equal(("primero", "1"), resultado[0]);
        Assert.Equal(("segundo", "2"), resultado[1]);
    }
}
