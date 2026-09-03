using CafManagerConection.Domain.Connections;

namespace CafManagerConection.Domain.Tests.Connections;

/// <summary>El tilde «usar mi identidad de Windows» de una conexión RDP (FR-186).</summary>
public class AjustesReservadosTests
{
    private static Connection Rdp() =>
        new(Guid.NewGuid(), "servidor", Protocol.Rdp, "srv01.interno");

    [Fact]
    public void Una_conexion_recien_creada_no_usa_la_identidad_de_Windows()
    {
        Assert.False(AjustesReservados.UsaIdentidadDeWindows(Rdp()));
    }

    [Fact]
    public void Marcar_el_tilde_lo_deja_puesto()
    {
        var conexion = Rdp();

        AjustesReservados.FijarIdentidadDeWindows(conexion, true);

        Assert.True(AjustesReservados.UsaIdentidadDeWindows(conexion));
    }

    [Fact]
    public void Destildarlo_borra_el_campo_en_lugar_de_guardar_un_false()
    {
        var conexion = Rdp();

        AjustesReservados.FijarIdentidadDeWindows(conexion, true);
        AjustesReservados.FijarIdentidadDeWindows(conexion, false);

        Assert.False(AjustesReservados.UsaIdentidadDeWindows(conexion));
        Assert.Empty(conexion.CustomFields);
    }

    [Fact]
    public void El_tilde_viaja_en_los_campos_propios_que_ya_se_serializan()
    {
        var conexion = Rdp();

        AjustesReservados.FijarIdentidadDeWindows(conexion, true);

        Assert.Equal(
            bool.TrueString,
            conexion.CustomFields[AjustesReservados.IdentidadDeWindows]);
    }

    [Fact]
    public void Una_conexion_SSH_con_el_mismo_campo_no_lo_usa()
    {
        var conexion = new Connection(Guid.NewGuid(), "servidor", Protocol.Ssh, "srv01.interno");

        conexion.SetCustomField(AjustesReservados.IdentidadDeWindows, bool.TrueString);

        Assert.False(AjustesReservados.UsaIdentidadDeWindows(conexion));
    }

    [Fact]
    public void Un_valor_que_no_es_booleano_no_enciende_el_tilde()
    {
        var conexion = Rdp();

        conexion.SetCustomField(AjustesReservados.IdentidadDeWindows, "sí");

        Assert.False(AjustesReservados.UsaIdentidadDeWindows(conexion));
    }

    [Theory]
    [InlineData("cmc:rdpIdentidadDeWindows", true)]
    [InlineData("cmc:conexionRapida", true)]
    [InlineData("Ticket", false)]
    [InlineData("", false)]
    public void Los_campos_del_prefijo_cmc_son_reservados(string nombre, bool reservado)
    {
        Assert.Equal(reservado, AjustesReservados.EsReservado(nombre));
    }
}
