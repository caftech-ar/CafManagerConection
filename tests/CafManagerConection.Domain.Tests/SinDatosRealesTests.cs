using System.Text.RegularExpressions;

namespace CafManagerConection.Domain.Tests;

// El repositorio es público. Las muestras de salida de servidores reales traían la subred
// corporativa, tres nombres de host, el usuario del dominio, dos nombres de proyecto de cliente y
// hasta el nombre de la notebook dentro del base64 de una clave privada de prueba.
public sealed class SinDatosRealesTests
{
    // Un patrón por entrada y no una alternancia gigante: si mañana alguien agrega un término y lo
    // escribe mal, falla ese término solo en lugar de invalidar la comprobación entera.
    private static readonly (string Nombre, string Patron, bool DistingueMayusculas, string Reemplazo)[] Prohibidos =
    [
        ("dirección privada IPv4",
         @"\b(?:10\.\d{1,3}|172\.(?:1[6-9]|2\d|3[01])|192\.168)\.\d{1,3}\.\d{1,3}\b",
         false,
         "una de documentación: 192.0.2.x, 198.51.100.x o 203.0.113.x (RFC 5737), o 198.18.0.0/15 (RFC 2544)"),

        ("prefijo IPv6 privado",
         @"\bfd[0-9a-f]{2}:[0-9a-f:]+",
         false,
         "2001:db8::/32 (RFC 3849)"),

        ("dirección MAC de hardware",
         @"\b(?!00:00:5e:00:53)(?!00:00:00:00:00:00)(?!ff:ff:ff:ff:ff:ff)(?:[0-9a-f]{2}:){5}[0-9a-f]{2}\b",
         false,
         "una del rango 00:00:5e:00:53:xx (RFC 7042)"),

        // Sólo minúsculas, y el TLD largo antes del corto: así `SSH.NET` y `System.Net` no cuentan
        // —son nombres de biblioteca— y `caftech.com.ar` no se parte en `caftech.com`.
        ("dominio no reservado para documentación",
         @"(?<![\w.])(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?:com\.ar|com|net|org)(?![\w.])",
         true,
         "example.com, example.org o un TLD no registrado (RFC 2606)"),
    ];

    // Lista negra de lo que ya se filtró una vez. Un patrón general no lo habría atajado: un nombre
    // de host en minúsculas dentro de un mensaje de error, o el censo del parque en un comentario.
    private static readonly (string Termino, string Motivo)[] Filtrados =
    [
        // El prefijo y no el nombre completo: en la misma red hay al menos «…svmeap01» y
        // «…escapp02», así que un término por host deja pasar el próximo.
        ("arbavsp", "prefijo con el que se nombran los servidores reales"),
        ("esc-ar-002", "nombre de un servidor real"),
        ("nodo-arm", "nombre de un servidor real"),
        ("svmea", "fragmento del nombre de un servidor real"),
        ("boldt", "nombre de la empresa, y del dominio de Windows"),
        ("alacquan", "usuario de una persona"),
        ("sifed", "nombre de un proyecto de cliente"),
        ("sicam", "nombre de un proyecto de cliente"),
        ("almirante-brown", "nombre de un municipio cliente"),
        ("apps vial", "nombre de un proyecto de cliente"),
        ("coneccionesremotas", "nombre del archivo de exportación del usuario"),
        ("131 conexiones", "censo del parque de servidores"),
        ("289 contraseñas", "censo de credenciales"),
        ("91 eran distintas", "cuántas credenciales cubren el parque entero"),
    ];

    private static readonly string[] Permitidos =
    [
        // Reservados por la RFC 2606, y `ejemplo.com` que es el mismo marcador en castellano.
        "example.com", "example.org", "example.net", "ejemplo.com", "dominio.com",

        // Sitios de verdad que el proyecto cita a propósito: el suyo y los de sus dependencias.
        "caftech.com.ar", "openssh.com", "winscp.net", "microsoft.com", "github.com",
        "anthropic.com", "nuget.org", "oxyplot.org", "sourceforge.net", "nsis.sourceforge.net",
    ];

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Ningun_archivo_del_repositorio_lleva_datos_de_una_red_real(string relativa)
    {
        var hallazgos = Buscar(File.ReadAllText(Path.Combine(Repositorio.Raiz(), relativa)));

        Assert.True(
            hallazgos.Count == 0,
            $"{relativa} tiene datos que no pueden ir a un repositorio público:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, hallazgos));
    }

    // Sin esto el verde no vale nada: un patrón mal escapado da cero coincidencias igual que un
    // árbol limpio, y así se pierde media hora creyendo que está todo bien.
    [Theory]
    [InlineData("el host es 10.20.4.31 y responde")]
    [InlineData("HostName=172.20.135.207")]
    [InlineData("inet6 fd9a:12d4:1529::1/64 scope global")]
    [InlineData("link/ether 00:50:56:94:65:da brd ff:ff:ff:ff:ff:ff")]
    [InlineData("search unaempresa.com.ar")]
    [InlineData("server_name api.unaempresa.com;")]
    [InlineData("docker pull registry.otracosa.net/imagen")]
    [InlineData("sudo: unable to resolve host arbavspsvmeap01")]
    [InlineData("12:22:11.236  ArBAVSPEscAPP02  [comando]  29 ms  salida 1")]
    [InlineData("<Company>Boldt</Company>")]
    [InlineData("el contenedor sifed-frontend-1 esta arriba")]
    [InlineData("Corre sobre una base con 131 conexiones reales")]
    [InlineData("de 289 contraseñas sólo 91 eran distintas")]
    public void El_guardian_reconoce_lo_que_tiene_que_reconocer(string sembrado) =>
        Assert.NotEmpty(Buscar(sembrado));

    [Theory]
    [InlineData("el host es 192.0.2.31 y responde")]
    [InlineData("inet 198.18.0.1/16 brd 198.18.255.255 scope global docker0")]
    [InlineData("inet6 2001:db8:a::1/64 scope global")]
    [InlineData("link/ether 00:00:5e:00:53:01 brd ff:ff:ff:ff:ff:ff")]
    [InlineData("search example.com")]
    [InlineData("server_name api.example.com;")]
    [InlineData("el sitio del producto es caftech.com.ar")]
    [InlineData("panel.local y servidor.interno son inventados")]
    [InlineData("using System.Net.Sockets; // SSH.NET lo envuelve")]
    [InlineData("escuchando en 127.0.0.1:2222 y en 0.0.0.0:80")]
    public void El_guardian_no_se_queja_de_lo_que_esta_bien(string sano) =>
        Assert.Empty(Buscar(sano));

    private static List<string> Buscar(string texto)
    {
        var hallazgos = new List<string>();

        foreach (var (nombre, patron, distingue, reemplazo) in Prohibidos)
        {
            var opciones = distingue ? RegexOptions.None : RegexOptions.IgnoreCase;

            foreach (var m in Regex.Matches(texto, patron, opciones).Cast<Match>())
            {
                if (!Permitido(m.Value))
                {
                    hallazgos.Add($"  «{m.Value}»: {nombre}. Usá {reemplazo}.");
                }
            }
        }

        foreach (var (termino, motivo) in Filtrados)
        {
            if (texto.Contains(termino, StringComparison.OrdinalIgnoreCase))
            {
                hallazgos.Add($"  «{termino}»: {motivo}. No puede volver al repositorio.");
            }
        }

        return hallazgos;
    }

    /// <summary>Lo que parece un dato real y no lo es: rangos reservados y dominios conocidos.</summary>
    private static bool Permitido(string valor)
    {
        var v = valor.ToLowerInvariant();

        return v.StartsWith("127.0.0.", StringComparison.Ordinal)
               || v.StartsWith("0.0.0.0", StringComparison.Ordinal)
               || Permitidos.Any(p => v.Equals(p, StringComparison.Ordinal)
                                      || v.EndsWith($".{p}", StringComparison.Ordinal));
    }

    public static TheoryData<string> Archivos()
    {
        var datos = new TheoryData<string>();
        var raiz = Repositorio.Raiz();

        foreach (var archivo in Revisables(raiz))
        {
            datos.Add(Path.GetRelativePath(raiz, archivo));
        }

        return datos;
    }

    private static IEnumerable<string> Revisables(string raiz)
    {
        string[] carpetas =
        [
            "src", "tests", "specs", "docs", ".specify", "scripts", "build", "installer",
            "tools", ".github",
        ];

        var sueltos = new[] { "README.md", "Taskfile.yml", "Directory.Build.props" }
            .Select(a => Path.Combine(raiz, a))
            .Where(File.Exists);

        var dentro = carpetas
            .Select(c => Path.Combine(raiz, c))
            .Where(Directory.Exists)
            .SelectMany(c => Directory.EnumerateFiles(c, "*", SearchOption.AllDirectories))
            .Where(Revisable);

        return sueltos.Concat(dentro);
    }

    private static bool Revisable(string ruta)
    {
        var separador = Path.DirectorySeparatorChar;

        if (ruta.Contains($"{separador}bin{separador}", StringComparison.Ordinal)
            || ruta.Contains($"{separador}obj{separador}", StringComparison.Ordinal))
        {
            return false;
        }

        // Este archivo lleva los valores sembrados que el guardián tiene que reconocer.
        if (Path.GetFileName(ruta) == "SinDatosRealesTests.cs")
        {
            return false;
        }

        return Path.GetExtension(ruta) is ".cs" or ".xaml" or ".md" or ".yml" or ".yaml"
            or ".json" or ".ps1" or ".nsi" or ".props" or ".xml" or ".sql";
    }
}
