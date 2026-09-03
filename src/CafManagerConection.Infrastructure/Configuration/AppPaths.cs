namespace CafManagerConection.Infrastructure.Configuration;

/// <summary>Ubicaciones de los datos del usuario, todas bajo <c>%LocalAppData%</c>: es lo que permite funcionar sin privilegios de administrador.</summary>
public sealed class AppPaths
{
    public const string ProductFolderName = "CafManagerConection";
    public const string DatabaseFileName = "cmc.db";

    public AppPaths(string? rootOverride = null)
    {
        Root = rootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductFolderName);

        DatabasePath = Path.Combine(Root, DatabaseFileName);
        LogsDirectory = Path.Combine(Root, "logs");
    }

    public string Root { get; }

    public string DatabasePath { get; }

    public string LogsDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>Ruta a la que se mueve una base corrupta; lleva sello de tiempo para no pisar una preservada antes (FR-052).</summary>
    public string CorruptedDatabasePath(DateTimeOffset now) =>
        $"{DatabasePath}.corrupta-{now:yyyyMMdd-HHmmss}";
}
