using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Tests.Sessions;

public sealed class RegistroDeTrazasTests
{
    private static EntradaDeTraza Entrada(string enviado, string salida = "") => new(
        DateTimeOffset.UnixEpoch,
        Guid.Empty,
        "servidor",
        TipoDeTraza.Comando,
        enviado,
        0,
        TimeSpan.Zero,
        salida,
        string.Empty);

    [Fact]
    public void Lo_anotado_se_puede_leer_en_orden()
    {
        var registro = new RegistroDeTrazas();

        registro.Anotar(Entrada("uno"));
        registro.Anotar(Entrada("dos"));

        Assert.Equal(["uno", "dos"], registro.Entradas().Select(e => e.Enviado));
    }

    [Fact]
    public void Pasada_la_capacidad_se_descartan_las_mas_viejas()
    {
        var registro = new RegistroDeTrazas();

        for (var i = 0; i < RegistroDeTrazas.Capacidad + 10; i++)
        {
            registro.Anotar(Entrada($"comando {i}"));
        }

        var entradas = registro.Entradas();

        Assert.Equal(RegistroDeTrazas.Capacidad, entradas.Count);
        Assert.Equal("comando 10", entradas[0].Enviado);
        Assert.Equal($"comando {RegistroDeTrazas.Capacidad + 9}", entradas[^1].Enviado);
    }

    [Fact]
    public void El_total_anotado_cuenta_tambien_lo_descartado()
    {
        var registro = new RegistroDeTrazas();

        for (var i = 0; i < RegistroDeTrazas.Capacidad + 7; i++)
        {
            registro.Anotar(Entrada("x"));
        }

        Assert.Equal(RegistroDeTrazas.Capacidad + 7, registro.Anotadas);
    }

    [Fact]
    public void Una_salida_larga_se_recorta_y_se_avisa()
    {
        var registro = new RegistroDeTrazas();
        var larga = new string('a', RegistroDeTrazas.LargoMaximo + 500);

        registro.Anotar(Entrada("docker ps", larga));

        var guardada = registro.Entradas()[0].Salida;

        Assert.StartsWith(new string('a', RegistroDeTrazas.LargoMaximo), guardada, StringComparison.Ordinal);
        Assert.Contains("500 caracteres más", guardada, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_salida_corta_no_se_toca()
    {
        var registro = new RegistroDeTrazas();

        registro.Anotar(Entrada("uname -a", "Linux servidor"));

        Assert.Equal("Linux servidor", registro.Entradas()[0].Salida);
    }

    [Fact]
    public void Las_cuentas_de_bytes_miden_lo_recibido_no_lo_guardado()
    {
        var registro = new RegistroDeTrazas();
        var larga = new string('a', RegistroDeTrazas.LargoMaximo + 500);

        registro.Anotar(Entrada("ps", larga));

        Assert.Equal(RegistroDeTrazas.LargoMaximo + 500, registro.BytesRecibidos);
        Assert.Equal(2, registro.BytesEnviados);
    }

    [Fact]
    public void En_pausa_no_se_anota_nada()
    {
        var registro = new RegistroDeTrazas { Activo = false };

        registro.Anotar(Entrada("docker ps"));

        Assert.Empty(registro.Entradas());
        Assert.Equal(0, registro.Anotadas);
        Assert.Equal(0, registro.BytesRecibidos);
    }

    [Fact]
    public void Limpiar_vacia_el_buffer_y_deja_los_totales()
    {
        var registro = new RegistroDeTrazas();

        registro.Anotar(Entrada("uname", "Linux"));
        registro.Limpiar();

        Assert.Empty(registro.Entradas());
        Assert.Equal(1, registro.Anotadas);
        Assert.Equal(5, registro.BytesRecibidos);
    }

    [Fact]
    public void Cada_anotacion_avisa()
    {
        var registro = new RegistroDeTrazas();
        var avisos = new List<string>();

        registro.Anotada += (_, e) => avisos.Add(e.Enviado);

        registro.Anotar(Entrada("uno"));
        registro.Anotar(Entrada("dos"));

        Assert.Equal(["uno", "dos"], avisos);
    }

    // supervisorctl status devuelve 3 con la tabla completa, y eso también merece resaltarse.
    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(-1, true)]
    public void El_fallo_sale_del_estado_de_salida(int? codigo, bool esperado) =>
        Assert.Equal(esperado, (Entrada("x") with { Codigo = codigo }).Fallo);

    [Fact]
    public async Task Escribir_desde_varios_hilos_no_lo_rompe()
    {
        var registro = new RegistroDeTrazas();

        var escritores = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                registro.Anotar(Entrada("x", "salida"));
            }
        }));

        var lector = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                _ = registro.Entradas().Count;
            }
        });

        await Task.WhenAll([.. escritores, lector]);

        Assert.Equal(1600, registro.Anotadas);
        Assert.Equal(RegistroDeTrazas.Capacidad, registro.Entradas().Count);
    }
}
