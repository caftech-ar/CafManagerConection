using System.Runtime.InteropServices;
using System.Text;

namespace CafManagerConection.Ssh.Tests;

// SC-052a: el valor conocido no puede aparecer en la base, en la configuración, en el Administrador
// de credenciales ni en ningún archivo bajo %LocalAppData%\CafManagerConection.
public sealed class ContrasenaDeSudoNoSePersisteTests
{
    private const string Conocida = "clave-de-sudo-que-no-se-guarda-1d47";

    private static readonly string[] ApisQuePersisten =
    [
        "CredWrite",
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
    public void Ninguna_credencial_de_la_aplicacion_guarda_una_contrasena_de_sudo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var contrasena = new ContrasenaDeSudoDeSesion();
        contrasena.Guardar(Conocida);

        var guardadas = CredencialesDeLaAplicacion();

        Assert.DoesNotContain(
            guardadas,
            c => c.Contains("sudo", StringComparison.OrdinalIgnoreCase)
                 || c.Contains(Conocida, StringComparison.Ordinal));
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

    private static string Raiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);

        while (carpeta is not null && !Directory.Exists(Path.Combine(carpeta.FullName, "specs")))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta?.FullName
               ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private static IReadOnlyList<string> CredencialesDeLaAplicacion()
    {
        if (!CredEnumerateW("cmc:*", 0, out var cuantas, out var arreglo))
        {
            return [];
        }

        var leidas = new List<string>((int)cuantas);

        try
        {
            for (var i = 0; i < cuantas; i++)
            {
                var puntero = Marshal.ReadIntPtr(arreglo, i * IntPtr.Size);
                var credencial = Marshal.PtrToStructure<CREDENTIAL>(puntero);

                var nombre = Marshal.PtrToStringUni(credencial.TargetName) ?? string.Empty;
                var blob = credencial.CredentialBlobSize == 0
                    ? string.Empty
                    : Marshal.PtrToStringUni(
                        credencial.CredentialBlob, (int)credencial.CredentialBlobSize / 2);

                leidas.Add($"{nombre}\n{blob}");
            }
        }
        finally
        {
            CredFree(arreglo);
        }

        return leidas;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredEnumerateW(
        string? filtro, uint banderas, out uint cuantas, out IntPtr credenciales);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr arreglo);

    [StructLayout(LayoutKind.Sequential)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
