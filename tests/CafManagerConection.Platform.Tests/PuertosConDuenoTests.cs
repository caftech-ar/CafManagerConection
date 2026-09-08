using CafManagerConection.Platform;

namespace CafManagerConection.Platform.Tests;

// El usuario y el directorio de trabajo no los trae `ss`: salen de /proc en el mismo viaje, detras
// de la marca cmc:duenos. Sin el bloque, el parser tiene que seguir andando igual: es lo que llega
// de un servidor donde /proc de un proceso ajeno no se pudo leer.
public sealed class PuertosConDuenoTests
{
    private const string Escucha =
        "tcp   LISTEN 0      511          0.0.0.0:80        0.0.0.0:*    users:((\"nginx\",pid=1234,fd=6))\n"
        + "tcp   LISTEN 0      4096       127.0.0.1:5432      0.0.0.0:*    users:((\"postgres\",pid=987,fd=5))\n";

    [Fact]
    public void El_usuario_y_el_directorio_llegan_a_cada_puerto()
    {
        var salida = Escucha
            + "cmc:duenos\n"
            + "1234\twww-data\t/var/www\n"
            + "987\tpostgres\t/var/lib/postgresql/16/main\n";

        var puertos = PuertosParser.Parse(salida);

        var web = puertos.Single(p => p.Port == 80);
        Assert.Equal("www-data", web.Usuario);
        Assert.Equal("/var/www", web.DirectorioDeTrabajo);

        var base_ = puertos.Single(p => p.Port == 5432);
        Assert.Equal("postgres", base_.Usuario);
        Assert.Equal("/var/lib/postgresql/16/main", base_.DirectorioDeTrabajo);
    }

    [Fact]
    public void Un_directorio_con_espacios_llega_entero()
    {
        var salida = Escucha + "cmc:duenos\n" + "1234\twww-data\t/srv/mi sitio/publico\n";

        Assert.Equal(
            "/srv/mi sitio/publico",
            PuertosParser.Parse(salida).Single(p => p.Port == 80).DirectorioDeTrabajo);
    }

    [Fact]
    public void Sin_el_bloque_de_duenos_los_puertos_salen_igual()
    {
        var puertos = PuertosParser.Parse(Escucha);

        Assert.Equal(2, puertos.Count);
        Assert.All(puertos, p => Assert.Equal(string.Empty, p.Usuario));
        Assert.All(puertos, p => Assert.Equal(string.Empty, p.DirectorioDeTrabajo));
    }

    [Fact]
    public void Un_pid_sin_dueno_no_contamina_a_los_demas()
    {
        // Es el caso real: /proc del proceso ajeno no se pudo leer y sólo vino uno de los dos.
        var salida = Escucha + "cmc:duenos\n" + "1234\twww-data\t/var/www\n";

        var puertos = PuertosParser.Parse(salida);

        Assert.Equal("www-data", puertos.Single(p => p.Port == 80).Usuario);
        Assert.Equal(string.Empty, puertos.Single(p => p.Port == 5432).Usuario);
    }

    [Fact]
    public void Un_dueno_sin_directorio_legible_deja_el_directorio_vacio()
    {
        // readlink sobre el cwd de un proceso ajeno falla aunque stat del /proc sí resuelva.
        var salida = Escucha + "cmc:duenos\n" + "1234\twww-data\t\n";

        var web = PuertosParser.Parse(salida).Single(p => p.Port == 80);

        Assert.Equal("www-data", web.Usuario);
        Assert.Equal(string.Empty, web.DirectorioDeTrabajo);
    }
}
