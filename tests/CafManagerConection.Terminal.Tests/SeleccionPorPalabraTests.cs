using CafManagerConection.Terminal;

namespace CafManagerConection.Terminal.Tests;

public sealed class SeleccionPorPalabraTests
{
    private static TerminalCell[] Linea(string texto)
    {
        var celdas = new TerminalCell[texto.Length];

        for (var i = 0; i < texto.Length; i++)
        {
            celdas[i] = TerminalCell.Empty;
            celdas[i].Char = texto[i];
        }

        return celdas;
    }

    private static string Palabra(string texto, int columna)
    {
        var linea = Linea(texto);
        var (inicio, fin) = TerminalControl.LimitesDePalabra(linea, columna);

        return texto[inicio..(fin + 1)];
    }

    [Fact]
    public void Doble_clic_en_una_palabra_la_toma_entera() =>
        Assert.Equal("supervisorctl", Palabra("sudo supervisorctl status", 8));

    [Fact]
    public void Sirve_igual_desde_el_primer_caracter() =>
        Assert.Equal("sudo", Palabra("sudo supervisorctl status", 0));

    [Fact]
    public void Sirve_igual_desde_el_ultimo_caracter() =>
        Assert.Equal("status", Palabra("sudo supervisorctl status", 24));

    [Fact]
    public void Sobre_un_separador_no_se_estira() =>
        Assert.Equal(" ", Palabra("sudo supervisorctl", 4));

    [Fact]
    public void Una_ruta_entera_es_una_palabra() =>
        Assert.Equal(
            "/etc/nginx/sites-enabled/default.conf",
            Palabra("cat /etc/nginx/sites-enabled/default.conf", 10));

    [Fact]
    public void Una_direccion_ip_es_una_palabra() =>
        Assert.Equal("192.0.2.31", Palabra("ping 192.0.2.31 -c 4", 8));

    [Fact]
    public void Un_usuario_arroba_servidor_es_una_palabra() =>
        Assert.Equal("operador@servidor-uno", Palabra("ssh operador@servidor-uno", 10));

    [Fact]
    public void Un_nombre_de_proceso_con_guion_es_una_palabra() =>
        Assert.Equal("operador-inventario-to", Palabra("restart operador-inventario-to", 12));

    [Fact]
    public void Un_host_con_puerto_es_una_palabra() =>
        Assert.Equal("registry.example:5000", Palabra("docker pull registry.example:5000", 15));

    [Fact]
    public void Las_comillas_cortan() =>
        Assert.Equal("hola", Palabra("echo \"hola\"", 6));

    [Fact]
    public void Los_parentesis_cortan() =>
        Assert.Equal("whoami", Palabra("$(whoami)", 3));

    [Theory]
    [InlineData('a', true)]
    [InlineData('9', true)]
    [InlineData('/', true)]
    [InlineData('.', true)]
    [InlineData('-', true)]
    [InlineData('_', true)]
    [InlineData(':', true)]
    [InlineData('@', true)]
    [InlineData('~', true)]
    [InlineData('=', true)]
    [InlineData(' ', false)]
    [InlineData('"', false)]
    [InlineData('\'', false)]
    [InlineData('(', false)]
    [InlineData(';', false)]
    [InlineData('|', false)]
    public void La_lista_de_separadores_es_la_esperada(char c, bool esDePalabra) =>
        Assert.Equal(esDePalabra, TerminalControl.EsDePalabra(c));

    [Fact]
    public void Una_linea_vacia_no_rompe()
    {
        var (inicio, fin) = TerminalControl.LimitesDePalabra([], 0);

        Assert.Equal(0, inicio);
        Assert.Equal(0, fin);
    }

    [Fact]
    public void Una_columna_fuera_de_rango_no_rompe()
    {
        var linea = Linea("hola");
        var (inicio, fin) = TerminalControl.LimitesDePalabra(linea, 99);

        Assert.Equal(0, inicio);
        Assert.Equal(3, fin);
    }

    [Fact]
    public void La_linea_termina_donde_termina_el_texto()
    {
        var linea = Linea("df -h /                                 ");

        Assert.Equal(6, TerminalControl.UltimaColumnaConTexto(linea));
    }

    [Fact]
    public void Una_linea_toda_en_blanco_da_la_primera_columna()
    {
        var linea = Linea("        ");

        Assert.Equal(0, TerminalControl.UltimaColumnaConTexto(linea));
    }

    [Fact]
    public void Una_linea_sin_relleno_termina_en_su_ultimo_caracter()
    {
        var linea = Linea("uname -a");

        Assert.Equal(7, TerminalControl.UltimaColumnaConTexto(linea));
    }
}
