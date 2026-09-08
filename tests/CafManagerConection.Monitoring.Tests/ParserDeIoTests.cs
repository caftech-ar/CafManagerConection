using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

public sealed class ParserDeIoTests
{
    private const string IoDeVerdad = """
        rchar: 3273983
        wchar: 1244851
        syscr: 6961
        syscw: 4225
        read_bytes: 27889664
        write_bytes: 331776
        cancelled_write_bytes: 45056
        """;

    [Fact]
    public void Lee_los_bytes_leidos_y_escritos_en_el_disco()
    {
        var io = ParserDeIo.Parse(IoDeVerdad);

        Assert.Equal(27889664, io.BytesLeidos);
        Assert.Equal(331776, io.BytesEscritos);
    }

    [Fact]
    public void No_confunde_rchar_y_wchar_con_los_bytes_del_disco()
    {
        var io = ParserDeIo.Parse(IoDeVerdad);

        Assert.NotEqual(3273983, io.BytesLeidos);
        Assert.NotEqual(1244851, io.BytesEscritos);
    }

    [Fact]
    public void No_confunde_cancelled_write_bytes_con_write_bytes()
    {
        var io = ParserDeIo.Parse("cancelled_write_bytes: 45056");

        Assert.Null(io.BytesEscritos);
    }

    [Fact]
    public void Un_proceso_ajeno_sin_permiso_no_deja_bloque_y_eso_no_es_un_error()
    {
        var io = ParserDeIo.Parse(string.Empty);

        Assert.Equal(EntradaSalidaDeProceso.Desconocida, io);
        Assert.Null(io.BytesLeidos);
        Assert.Null(io.BytesEscritos);
        Assert.False(io.EsConocida);
    }

    [Fact]
    public void Un_bloque_a_medias_informa_lo_que_hay()
    {
        var io = ParserDeIo.Parse("read_bytes: 1024");

        Assert.Equal(1024, io.BytesLeidos);
        Assert.Null(io.BytesEscritos);
        Assert.True(io.EsConocida);
    }

    [Fact]
    public void Un_valor_que_no_es_numero_no_rompe_el_parseo()
    {
        var io = ParserDeIo.Parse("read_bytes: ninguno\nwrite_bytes: 8192");

        Assert.Null(io.BytesLeidos);
        Assert.Equal(8192, io.BytesEscritos);
    }

    [Fact]
    public void Tolera_CRLF_igual_que_LF()
    {
        var conCrlf = ParserDeIo.Parse(IoDeVerdad.ReplaceLineEndings("\r\n"));

        Assert.Equal(ParserDeIo.Parse(IoDeVerdad), conCrlf);
    }
}
