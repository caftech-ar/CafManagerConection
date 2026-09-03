using System.Text;
using CafManagerConection.Ssh;

namespace CafManagerConection.Ssh.Tests;

public sealed class EntradaDeContrasenaInteractivaTests
{
    [Fact]
    public void Escribir_y_confirmar_devuelve_lo_tipeado()
    {
        var entrada = new EntradaDeContrasenaInteractiva();

        Assert.Equal(ResultadoDeEntrada.Continua, entrada.Alimentar("secreto"u8));
        Assert.Equal(ResultadoDeEntrada.Confirmada, entrada.Alimentar("\r"u8));

        Assert.Equal("secreto", entrada.TomarTexto());
    }

    [Fact]
    public void Enter_como_salto_de_linea_tambien_confirma()
    {
        var entrada = new EntradaDeContrasenaInteractiva();

        entrada.Alimentar("hola"u8);
        var resultado = entrada.Alimentar("\n"u8);

        Assert.Equal(ResultadoDeEntrada.Confirmada, resultado);
        Assert.Equal("hola", entrada.TomarTexto());
    }

    [Fact]
    public void Escape_cancela_sin_devolver_lo_tipeado_como_confirmado()
    {
        var entrada = new EntradaDeContrasenaInteractiva();

        entrada.Alimentar("algo"u8);
        var resultado = entrada.Alimentar([0x1b]);

        Assert.Equal(ResultadoDeEntrada.Cancelada, resultado);
    }

    [Fact]
    public void Retroceso_borra_el_ultimo_caracter_tipeado()
    {
        var entrada = new EntradaDeContrasenaInteractiva();

        entrada.Alimentar("clavee"u8);
        entrada.Alimentar([0x7f]);
        entrada.Alimentar("\r"u8);

        Assert.Equal("clave", entrada.TomarTexto());
    }

    [Fact]
    public void Retroceso_con_el_bufer_vacio_no_hace_nada()
    {
        var entrada = new EntradaDeContrasenaInteractiva();

        entrada.Alimentar([0x7f]);
        entrada.Alimentar([0x7f]);
        entrada.Alimentar("a"u8);
        entrada.Alimentar("\r"u8);

        Assert.Equal("a", entrada.TomarTexto());
    }

    [Fact]
    public void Tomar_el_texto_deja_el_bufer_vacio_para_el_proximo_pedido()
    {
        var entrada = new EntradaDeContrasenaInteractiva();

        entrada.Alimentar("primera"u8);
        entrada.Alimentar("\r"u8);
        entrada.TomarTexto();

        entrada.Alimentar("segunda"u8);
        entrada.Alimentar("\r"u8);

        Assert.Equal("segunda", entrada.TomarTexto());
    }

    [Fact]
    public void Sin_nada_tipeado_confirmar_devuelve_texto_vacio()
    {
        var entrada = new EntradaDeContrasenaInteractiva();

        entrada.Alimentar("\r"u8);

        Assert.Equal(string.Empty, entrada.TomarTexto());
    }

    [Fact]
    public void Un_caracter_multibyte_se_reconstruye_entero()
    {
        var entrada = new EntradaDeContrasenaInteractiva();

        entrada.Alimentar(Encoding.UTF8.GetBytes("contraseña"));
        entrada.Alimentar("\r"u8);

        Assert.Equal("contraseña", entrada.TomarTexto());
    }
}
