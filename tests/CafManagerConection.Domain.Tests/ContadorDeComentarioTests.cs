namespace CafManagerConection.Domain.Tests;

public sealed class ContadorDeComentarioTests
{
    [Fact]
    public void Una_linea_en_blanco_no_cuenta_para_ningun_lado()
    {
        var cuenta = DensidadDeComentarioTests.Contar("var x = 1;\n\n\nvar y = 2;\n");

        Assert.Equal(0, cuenta.Comentario);
        Assert.Equal(2, cuenta.Codigo);
    }

    [Fact]
    public void Una_linea_con_codigo_y_comentario_cuenta_como_codigo()
    {
        var cuenta = DensidadDeComentarioTests.Contar("var x = 1; // esto no suma comentario\n");

        Assert.Equal(0, cuenta.Comentario);
        Assert.Equal(1, cuenta.Codigo);
    }

    [Fact]
    public void Las_barras_dentro_de_una_cadena_no_abren_un_comentario()
    {
        var cuenta = DensidadDeComentarioTests.Contar("""
            var url = "https://ejemplo/x";
            var otra = "/* tampoco */";
            """);

        Assert.Equal(0, cuenta.Comentario);
        Assert.Equal(2, cuenta.Codigo);
    }

    [Fact]
    public void Una_ruta_de_Windows_en_una_cadena_literal_no_escapa_la_comilla()
    {
        var cuenta = DensidadDeComentarioTests.Contar(
            "var ruta = @\"C:\\bin\\\";\nvar sigue = 1;\n// uno\n");

        Assert.Equal(1, cuenta.Comentario);
        Assert.Equal(2, cuenta.Codigo);
    }

    [Fact]
    public void Las_comillas_dobles_de_una_cadena_literal_no_la_cierran()
    {
        var cuenta = DensidadDeComentarioTests.Contar(
            "var s = @\"dijo \"\"hola\"\" y se fue\";\n// uno\n");

        Assert.Equal(1, cuenta.Comentario);
        Assert.Equal(1, cuenta.Codigo);
    }

    [Fact]
    public void Una_cadena_cruda_de_varias_lineas_cuenta_como_codigo_entera()
    {
        var fuente = "var muestra = \"\"\"\n// esto es dato, no comentario\nsegunda\n\"\"\";\n";
        var cuenta = DensidadDeComentarioTests.Contar(fuente);

        Assert.Equal(0, cuenta.Comentario);
        Assert.Equal(4, cuenta.Codigo);
    }

    [Fact]
    public void Un_bloque_de_varias_lineas_cuenta_cada_una()
    {
        var cuenta = DensidadDeComentarioTests.Contar("/* uno\n   dos\n   tres */\nvar x = 1;\n");

        Assert.Equal(3, cuenta.Comentario);
        Assert.Equal(1, cuenta.Codigo);
    }

    [Fact]
    public void El_codigo_que_sigue_a_un_bloque_en_la_misma_linea_cuenta_como_codigo()
    {
        var cuenta = DensidadDeComentarioTests.Contar("/* nota */ var x = 1;\n");

        Assert.Equal(0, cuenta.Comentario);
        Assert.Equal(1, cuenta.Codigo);
    }

    [Fact]
    public void La_documentacion_de_tres_barras_es_comentario()
    {
        var cuenta = DensidadDeComentarioTests.Contar(
            "/// <summary>Algo.</summary>\npublic int X { get; }\n");

        Assert.Equal(1, cuenta.Comentario);
        Assert.Equal(1, cuenta.Codigo);
    }

    [Fact]
    public void Una_barra_en_un_literal_de_caracter_no_abre_un_comentario()
    {
        var cuenta = DensidadDeComentarioTests.Contar("var c = '/';\nvar d = '\\'';\n");

        Assert.Equal(0, cuenta.Comentario);
        Assert.Equal(2, cuenta.Codigo);
    }
}
