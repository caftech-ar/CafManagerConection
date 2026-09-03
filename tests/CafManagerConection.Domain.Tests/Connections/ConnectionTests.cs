using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Tests.Connections;

public class ConnectionTests
{
    private static Connection Nueva(Protocol protocol = Protocol.Ssh) =>
        new(Guid.NewGuid(), "Linux Web", protocol, "192.0.2.20");

    [Fact]
    public void Crear_recorta_nombre_y_host()
    {
        var c = new Connection(Guid.NewGuid(), "  Linux Web  ", Protocol.Ssh, "  192.0.2.1  ");

        Assert.Equal("Linux Web", c.Name);
        Assert.Equal("192.0.2.1", c.Host);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Crear_rechaza_un_host_vacio(string host)
    {
        Assert.Throws<ArgumentException>(
            () => new Connection(Guid.NewGuid(), "Servidor", Protocol.Ssh, host));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-1)]
    public void SetPort_rechaza_puertos_fuera_de_rango(int port)
    {
        var c = Nueva();

        Assert.Throws<ArgumentOutOfRangeException>(() => c.SetPort(port));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(22)]
    [InlineData(65535)]
    public void SetPort_acepta_los_limites_del_rango(int port)
    {
        var c = Nueva();

        c.SetPort(port);

        Assert.Equal(port, c.Port);
    }

    [Fact]
    public void Un_puerto_nulo_significa_heredar()
    {
        var c = Nueva();

        c.SetPort(null);

        Assert.Null(c.Port);
    }

    [Theory]
    [InlineData(Protocol.Rdp, 3389)]
    [InlineData(Protocol.Ssh, 22)]
    [InlineData(Protocol.Web, 443)]
    public void Cada_protocolo_tiene_su_puerto_predeterminado(Protocol protocol, int esperado)
    {
        Assert.Equal(esperado, Connection.DefaultPortFor(protocol));
    }

    [Fact]
    public void Una_conexion_nueva_no_fija_puerto_usuario_ni_credencial()
    {
        var c = Nueva();

        // Los tres nulos: se resuelven por herencia, no son un estado invalido.
        Assert.Null(c.Port);
        Assert.Null(c.UserName);
        Assert.Null(c.CredentialKey);
    }

    [Fact]
    public void Notes_rechaza_un_texto_demasiado_largo()
    {
        var c = Nueva();
        var largo = new string('x', Connection.MaxNotesLength + 1);

        Assert.Throws<ArgumentException>(() => c.Notes = largo);
    }

    [Fact]
    public void Notes_acepta_la_longitud_maxima()
    {
        var c = Nueva();
        var limite = new string('x', Connection.MaxNotesLength);

        c.Notes = limite;

        Assert.Equal(Connection.MaxNotesLength, c.Notes!.Length);
    }

    [Fact]
    public void El_protocolo_no_se_puede_cambiar()
    {
        var propiedad = typeof(Connection).GetProperty(nameof(Connection.Protocol))!;

        Assert.Null(propiedad.SetMethod);
    }
}
