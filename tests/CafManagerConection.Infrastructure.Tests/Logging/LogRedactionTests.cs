using System.Reflection;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Sessions;
using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.Infrastructure.Logging;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.Infrastructure.Tests.Logging;

/// <summary>Principio II: ningún secreto ni contenido de sesión puede llegar al log.</summary>
public class LogRedactionTests
{
    private const string Secreto = "ClaveSuperSecreta-2026";

    [Fact]
    public async Task Ejercitar_todos_los_metodos_no_deja_ningun_secreto_en_el_archivo()
    {
        var root = Path.Combine(Path.GetTempPath(), "cmc-log-tests", Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(root);

        try
        {
            using (var logger = new SerilogAppLogger(paths))
            {
                var id = Guid.NewGuid();
                using var credencial = new StoredCredential("admin", "CORP", Secreto);

                logger.ApplicationStarted("1.0.0");
                logger.ConnectionOpening(id, "Ssh", "192.0.2.1", 22);
                logger.ConnectionSucceeded(id, TimeSpan.FromMilliseconds(320));
                logger.ConnectionFailed(id, SessionFailureReason.AuthenticationRejected, "auth failed");
                logger.ConnectionClosed(id, TimeSpan.FromMinutes(5));
                logger.TunnelStarted(Guid.NewGuid(), 8080);
                logger.TunnelStopped(Guid.NewGuid(), 8080);
                logger.DatabaseMigrated(0, 1);
                logger.DatabaseCorruptionRecovered(Path.Combine(root, "cmc.db.corrupta"));
                logger.TechnicalError("probar", new InvalidOperationException("algo falló"));
                logger.ApplicationStopping(2);
            }

            var contenido = await LeerLogsAsync(paths);

            Assert.NotEmpty(contenido);
            Assert.DoesNotContain(Secreto, contenido, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Limpiar(root);
        }
    }

    [Fact]
    public async Task Registrar_una_credencial_por_error_no_filtra_el_secreto()
    {
        var root = Path.Combine(Path.GetTempPath(), "cmc-log-tests", Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(root);

        try
        {
            using (var logger = new SerilogAppLogger(paths))
            {
                using var credencial = new StoredCredential("admin", null, Secreto);
                logger.TechnicalError($"usar {credencial}", new InvalidOperationException("x"));
            }

            var contenido = await LeerLogsAsync(paths);

            Assert.DoesNotContain(Secreto, contenido, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("redactada", contenido, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Limpiar(root);
        }
    }

    [Fact]
    public void La_interfaz_de_registro_no_acepta_objetos_arbitrarios()
    {
        var metodos = typeof(IAppLogger).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        var permisivos = metodos
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType == typeof(object) ||
                p.ParameterType == typeof(object[]) ||
                p.ParameterType == typeof(IDictionary<string, object>)))
            .Select(m => m.Name)
            .ToArray();

        Assert.True(
            permisivos.Length == 0,
            $"Estos métodos aceptan objetos arbitrarios y permiten filtrar secretos: " +
            $"{string.Join(", ", permisivos)}");
    }

    [Fact]
    public void No_existe_ningun_metodo_para_registrar_contenido_de_sesion()
    {
        var prohibidos = new[]
        {
            "Keystroke", "Terminal", "Screen", "Clipboard", "FileTransfer",
            "CommandOutput", "Metrics", "Inventory",
        };

        var nombres = typeof(IAppLogger)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToArray();

        foreach (var prohibido in prohibidos)
        {
            Assert.DoesNotContain(nombres, n => n.Contains(prohibido, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static async Task<string> LeerLogsAsync(AppPaths paths)
    {
        var archivos = Directory.GetFiles(paths.LogsDirectory, "*.log");
        var sb = new System.Text.StringBuilder();

        foreach (var archivo in archivos)
        {
            using var stream = new FileStream(
                archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            sb.Append(await reader.ReadToEndAsync());
        }

        return sb.ToString();
    }

    private static void Limpiar(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
