using CafManagerConection.Domain.Connections;
using CafManagerConection.UseCases.Connections;

namespace CafManagerConection.UseCases.Tests.Connections;

// FR-127.
public sealed class JerarquiaDeConexionesTests
{
    private static Connection Conexion(string nombre = "Aplicaciones") =>
        new(Guid.NewGuid(), nombre, Protocol.Ssh, "192.0.2.207");

    [Fact]
    public void Sin_padre_es_valido()
    {
        var c = Conexion();

        var r = ConnectionValidator.ValidateParent(c, padre: null, conexionTieneHijas: false);

        Assert.True(r.IsValid);
    }

    [Fact]
    public void Colgar_de_una_conexion_sin_padre_es_valido()
    {
        var servidor = Conexion();
        var servicio = Conexion("Portainer");

        var r = ConnectionValidator.ValidateParent(servicio, servidor, conexionTieneHijas: false);

        Assert.True(r.IsValid);
    }

    [Fact]
    public void Una_conexion_no_puede_colgar_de_si_misma()
    {
        var c = Conexion();

        var r = ConnectionValidator.ValidateParent(c, c, conexionTieneHijas: false);

        Assert.False(r.IsValid);
    }

    [Fact]
    public void No_se_puede_colgar_de_una_conexion_que_ya_cuelga_de_otra()
    {
        var abuelo = Conexion("Servidor");
        var padre = Conexion("Portainer");
        padre.ParentConnectionId = abuelo.Id;

        var nieto = Conexion("Algo mas");

        var r = ConnectionValidator.ValidateParent(nieto, padre, conexionTieneHijas: false);

        Assert.False(r.IsValid);
        Assert.Contains("un nivel", r.ToMessage(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Una_conexion_que_ya_tiene_hijas_no_puede_pasar_a_colgar_de_otra()
    {
        var servidor = Conexion("Servidor");
        var conServicios = Conexion("Aplicaciones");

        var r = ConnectionValidator.ValidateParent(
            conServicios, servidor, conexionTieneHijas: true);

        Assert.False(r.IsValid);
    }

    [Fact]
    public void Una_conexion_con_hijas_puede_quedarse_sin_padre()
    {
        var conServicios = Conexion("Aplicaciones");

        var r = ConnectionValidator.ValidateParent(
            conServicios, padre: null, conexionTieneHijas: true);

        Assert.True(r.IsValid);
    }
}
