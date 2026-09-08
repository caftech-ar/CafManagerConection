using System.Text;

namespace CafManagerConection.Ssh.Tests;

// El valor conocido no puede aparecer en la base, en la configuración ni en ningún archivo
// bajo %LocalAppData%\CafManagerConection. El vault es el único almacén de secretos del proyecto,
// así que los tres lugares que se revisan son los tres que hay.
public sealed class ContrasenaDeSudoNoSePersisteTests
{
    private const string Conocida = "clave-de-sudo-que-no-se-guarda-1d47";

    private static readonly string[] ApisQuePersisten =
    [
        "ICredentialStore",
        "File.WriteAll",
        "File.AppendAll",
        "StreamWriter(",
        "JsonSerializer.Serialize",
        "SqliteCommand",
        "Properties.Settings",
        "Environment.SetEnvironmentVariable",
    ];

    private static readonly string[] ArchivosDeLaContrasena =
    [
        @"src\CafManagerConection.Ssh\ContrasenaDeSudoDeSesion.cs",
        @"src\CafManagerConection.Ssh\IPedidoDeContrasenaDeSudo.cs",
        @"src\CafManagerConection.App\Views\PedidoDeContrasenaDeSudoWindow.xaml.cs",
    ];

    [Fact]
    public void Ningun_archivo_bajo_LocalAppData_contiene_la_contrasena()
    {
        using var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.Guardar(Conocida);

        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CafManagerConection");

        if (!Directory.Exists(carpeta))
        {
            return;
        }

        var conElValor = Directory
            .EnumerateFiles(carpeta, "*", SearchOption.AllDirectories)
            .Where(Contiene)
            .ToList();

        Assert.Empty(conElValor);
    }

    [Fact]
    public void El_archivo_de_la_base_no_guarda_una_contrasena_de_sudo()
    {
        using var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.Guardar(Conocida);

        var baseLocal = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CafManagerConection",
            "cmc.db");

        if (!File.Exists(baseLocal))
        {
            return;
        }

        Assert.False(Contiene(baseLocal), "La contraseña de sudo llegó al archivo de la base.");
    }

    [Fact]
    public void Los_archivos_que_tocan_la_contrasena_no_llaman_a_nada_que_persista()
    {
        var problemas = new List<string>();

        foreach (var relativo in ArchivosDeLaContrasena)
        {
            var archivo = Path.Combine(Raiz(), relativo);

            Assert.True(File.Exists(archivo), $"Falta {relativo}.");

            var texto = File.ReadAllText(archivo);

            problemas.AddRange(
                ApisQuePersisten
                    .Where(api => texto.Contains(api, StringComparison.Ordinal))
                    .Select(api => $"{relativo} usa {api}"));
        }

        Assert.Empty(problemas);
    }

    private static bool Contiene(string archivo)
    {
        try
        {
            using var flujo = new FileStream(
                archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            using var memoria = new MemoryStream();
            flujo.CopyTo(memoria);

            var bytes = memoria.ToArray();

            return Contiene(bytes, Encoding.UTF8.GetBytes(Conocida))
                   || Contiene(bytes, Encoding.Unicode.GetBytes(Conocida));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool Contiene(byte[] donde, byte[] que) =>
        donde.AsSpan().IndexOf(que.AsSpan()) >= 0;

    // Por src y tests, y no por una carpeta de documentacion: buscaba «specs», que se borro, y
    // entonces esta prueba fallaba por no encontrar la raiz en lugar de por lo que vigila.
    private static string Raiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);

        while (carpeta is not null
               && !(Directory.Exists(Path.Combine(carpeta.FullName, "src"))
                    && Directory.Exists(Path.Combine(carpeta.FullName, "tests"))))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta?.FullName
               ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
