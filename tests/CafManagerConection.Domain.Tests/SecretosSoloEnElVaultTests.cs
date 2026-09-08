namespace CafManagerConection.Domain.Tests;

// Cinco pantallas decian que la contrasena iba al almacen de Windows mientras el codigo ya
// escribia en el vault. Al sacar el almacen del proyecto, este guardian nacio comparando contra
// UNA cadena: por eso no vio «Administrador de Windows» en el resumen de credenciales de
// Preferencias, que sobrevivio a esa purga. Ahora es una lista y mira todo el repositorio, no
// solo la interfaz.
public sealed class SecretosSoloEnElVaultTests
{
    private static readonly string[] Nombres =
    [
        "Administrador de credenciales",
        "Administrador de Windows",
        "almacén de credenciales",
        "almacen de credenciales",
        "Credential Manager",
        "CredentialManager",
        "WindowsCredentialStore",
    ];

    private static readonly string[] Api =
    [
        "CredRead",
        "CredWrite",
        "CredDelete",
        "CredEnumerate",
        "CredFree",
        "advapi32",
    ];

    // La unica exencion, y por el motivo de siempre: para prohibir un termino hay que nombrarlo.
    // Este archivo lleva la lista y las cadenas con las que se prueba que la detecta.
    private static readonly string[] Exentos =
    [
        $"tests/CafManagerConection.Domain.Tests/{nameof(SecretosSoloEnElVaultTests)}.cs",
    ];

    private static readonly string[] Extensiones = ["*.cs", "*.xaml", "*.md", "*.ps1", "*.yml", "*.nsi"];

    private static readonly string[] Carpetas = ["src", "tests", "scripts", "installer"];

    [Fact]
    public void El_almacen_de_Windows_no_se_nombra_en_ninguna_parte_del_repositorio()
    {
        var hallazgos = new List<string>();

        foreach (var (relativa, lineas) in Archivos())
        {
            for (var i = 0; i < lineas.Length; i++)
            {
                var siguiente = i + 1 < lineas.Length ? lineas[i + 1] : null;

                foreach (var termino in TerminosProhibidosEn(lineas[i], siguiente))
                {
                    hallazgos.Add($"{relativa}:{i + 1}  →  «{termino}»");
                }
            }
        }

        Assert.True(
            hallazgos.Count == 0,
            $"{hallazgos.Count} hallazgo(s). El almacén de credenciales de Windows no se usa más: la "
            + "única fuente de claves es el vault cifrado de la base SQLite. Nombrarlo hace que el "
            + "usuario busque su contraseña donde no está, y que crea que hay una copia fuera de la "
            + $"base.{Environment.NewLine}{Environment.NewLine}"
            + string.Join(Environment.NewLine, hallazgos));
    }

    // Sin esto el guardian pasaria en verde con la busqueda rota o con el arbol vacio. Se ancla
    // en lo que el proyecto sigue teniendo, no en lo que tuvo: la clave logica y DPAPI se fueron
    // con el vault de dos niveles, y anclarse ahi era lo que hacia fallar este guardian.
    [Fact]
    public void El_guardian_mira_un_arbol_que_tiene_algo_adentro()
    {
        var texto = Archivos().SelectMany(a => a.Lineas).ToArray();

        Assert.Contains(texto, l => l.Contains("AES-256-GCM", StringComparison.Ordinal));
        Assert.Contains(texto, l => l.Contains("clave maestra", StringComparison.Ordinal));
    }

    [Fact]
    public void Ni_DPAPI_ni_la_clave_logica_volvieron()
    {
        var texto = Archivos()
            .SelectMany(a => a.Lineas.Select(l => (a.Relativa, Linea: l)))
            .Where(x => x.Linea.Contains("CryptProtectData", StringComparison.Ordinal)
                        || x.Linea.Contains("CryptUnprotectData", StringComparison.Ordinal))
            .Select(x => x.Relativa)
            .Distinct()
            .ToArray();

        Assert.True(
            texto.Length == 0,
            "DPAPI ata el secreto al usuario de Windows, y eso es un tercer camino de apertura "
            + "que el usuario no ve: se sacó a propósito. Está en: " + string.Join(", ", texto));
    }

    [Theory]
    [InlineData("// la clave va al Administrador de credenciales", "Administrador de credenciales")]
    [InlineData("Text=\"Guardar en el almacén de credenciales\"", "almacén de credenciales")]
    [InlineData("[DllImport(\"advapi32.dll\")]", "advapi32")]
    [InlineData("static extern bool CredWrite(...)", "CredWrite")]
    public void Un_nombre_del_almacen_se_detecta(string linea, string esperado) =>
        Assert.Contains(esperado, TerminosProhibidosEn(linea, null));

    [Fact]
    public void Un_nombre_partido_en_dos_renglones_se_detecta() =>
        Assert.Contains(
            "Administrador de credenciales",
            TerminosProhibidosEn(
                "               Text=\"La clave va al Administrador",
                "                      de credenciales de Windows.\" />"));

    [Theory]
    [InlineData("// la clave va al vault cifrado de la base")]
    [InlineData("Text=\"Están en la base, cifradas con AES-256-GCM.\"")]
    [InlineData("private const string Credencial = \"cmc:folder:1:ssh\";")]
    public void Nombrar_el_vault_de_CMC_no_se_marca(string linea) =>
        Assert.Empty(TerminosProhibidosEn(linea, null));

    // Se mira la linea pegada a la siguiente: «el Administrador / de credenciales de Windows»
    // partido en dos renglones no lo veia un guardian que compara linea por linea.
    internal static IEnumerable<string> TerminosProhibidosEn(string linea, string? siguiente)
    {
        var conLaSiguiente = siguiente is null ? linea : $"{linea} {siguiente.TrimStart()}";

        return Nombres
            .Concat(Api)
            .Where(t => conLaSiguiente.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<(string Relativa, string[] Lineas)> Archivos()
    {
        var raiz = Repositorio.Raiz();

        foreach (var carpeta in Carpetas)
        {
            var completa = Path.Combine(raiz, carpeta);

            if (!Directory.Exists(completa))
            {
                continue;
            }

            foreach (var extension in Extensiones)
            {
                foreach (var archivo in Directory.EnumerateFiles(
                             completa, extension, SearchOption.AllDirectories))
                {
                    var relativa = Path.GetRelativePath(raiz, archivo)
                        .Replace(Path.DirectorySeparatorChar, '/');

                    if (EsGenerado(relativa) || Exentos.Any(e => relativa.StartsWith(e, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    yield return (relativa, File.ReadAllLines(archivo));
                }
            }
        }
    }

    private static bool EsGenerado(string relativa) =>
        relativa.Contains("/obj/", StringComparison.Ordinal)
        || relativa.Contains("/bin/", StringComparison.Ordinal);
}
