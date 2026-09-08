using CafManagerConection.Domain.Connections;
using Xunit;

namespace CafManagerConection.Domain.Tests.Connections;

public sealed class FiltroDelArbolTests
{
    [Fact]
    public void Sin_filtro_pasa_todo()
    {
        Assert.False(FiltroDelArbol.Ninguno.Activo());
        Assert.True(FiltroDelArbol.Ninguno.Admite(Protocol.Ssh, esFavorita: false));
        Assert.True(FiltroDelArbol.Ninguno.Admite(Protocol.Rdp, esFavorita: false));
    }

    [Fact]
    public void Favoritas_deja_afuera_las_que_no_lo_son()
    {
        Assert.True(FiltroDelArbol.Favoritas.Admite(Protocol.Rdp, esFavorita: true));
        Assert.False(FiltroDelArbol.Favoritas.Admite(Protocol.Ssh, esFavorita: false));
    }

    [Fact]
    public void Ssh_deja_afuera_rdp_sea_o_no_favorita()
    {
        Assert.True(FiltroDelArbol.Ssh.Admite(Protocol.Ssh, esFavorita: false));
        Assert.False(FiltroDelArbol.Ssh.Admite(Protocol.Rdp, esFavorita: true));
    }

    [Fact]
    public void Rdp_deja_afuera_ssh_sea_o_no_favorita()
    {
        Assert.True(FiltroDelArbol.Rdp.Admite(Protocol.Rdp, esFavorita: false));
        Assert.False(FiltroDelArbol.Rdp.Admite(Protocol.Ssh, esFavorita: true));
    }

    [Fact]
    public void Apretar_otro_apaga_el_anterior()
    {
        var filtro = FiltroDelArbol.Ninguno.Alternar(FiltroDelArbol.Ssh);
        Assert.Equal(FiltroDelArbol.Ssh, filtro);

        filtro = filtro.Alternar(FiltroDelArbol.Rdp);
        Assert.Equal(FiltroDelArbol.Rdp, filtro);

        filtro = filtro.Alternar(FiltroDelArbol.Favoritas);
        Assert.Equal(FiltroDelArbol.Favoritas, filtro);
    }

    // Sin esto habria que buscar otro control para volver a ver todo.
    [Fact]
    public void Apretar_el_que_ya_esta_prendido_lo_apaga()
    {
        Assert.Equal(
            FiltroDelArbol.Ninguno,
            FiltroDelArbol.Ssh.Alternar(FiltroDelArbol.Ssh));
    }

    [Theory]
    [InlineData(FiltroDelArbol.Ninguno, "")]
    [InlineData(FiltroDelArbol.Favoritas, "favoritas")]
    [InlineData(FiltroDelArbol.Ssh, "sólo SSH")]
    [InlineData(FiltroDelArbol.Rdp, "sólo RDP")]
    public void La_descripcion_dice_que_esta_filtrando(FiltroDelArbol filtro, string esperada) =>
        Assert.Equal(esperada, filtro.Descripcion());

    [Fact]
    public void Todo_filtro_activo_tiene_descripcion_y_todo_inactivo_no()
    {
        foreach (var filtro in Enum.GetValues<FiltroDelArbol>())
        {
            Assert.Equal(filtro.Activo(), filtro.Descripcion().Length > 0);
        }
    }
}
