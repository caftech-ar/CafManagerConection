using System.Runtime.Versioning;
using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.Infrastructure.Credentials;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.Infrastructure.Logging;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using CafManagerConection.UseCases.Credentials;
using CafManagerConection.Infrastructure;
using CafManagerConection.UseCases.Sessions;
using CafManagerConection.UseCases.Folders;

namespace CafManagerConection.App.Bootstrap;

/// <summary>Cableado de dependencias, hecho a mano.</summary>
[SupportedOSPlatform("windows")]
public sealed class CompositionRoot : IDisposable
{
    private readonly SerilogAppLogger _logger;

    private CompositionRoot(
        AppPaths paths,
        SerilogAppLogger logger,
        SqliteConnectionFactory factory,
        DatabaseStartupResult startup)
    {
        Paths = paths;
        _logger = logger;
        Startup = startup;

        Folders = new FolderRepository(factory);
        Connections = new ConnectionRepository(factory);
        Settings = new SettingsStore(factory);
        Tunnels = new TunnelRepository(factory);
        History = new ConnectionHistoryRepository(factory);
        Tags = new TagRepository(factory);
        // El Administrador de credenciales de Windows deja de ser el almacen: queda solo como
        // origen de la migracion de la 0.1.1. Los secretos van cifrados a la base.
        AdministradorDeWindows = new WindowsCredentialStore();

        var repositorioDelVault = new RepositorioDelVault(factory);

        Vault = new Vault(repositorioDelVault, new ProteccionDpapiDelEquipo());
        Credentials = new VaultCredentialStore(Vault, repositorioDelVault);

        AppSettings = new AppSettingsService(Settings);
        FolderService = new FolderService(Folders, Connections, Credentials);
        ConnectionService = new ConnectionService(Connections, Folders, Credentials, Tags, logger);

        CredentialProvider = new CredentialProvider(
            Connections,
            Folders,
            Credentials,
            new Views.CredentialPromptWpf(() => System.Windows.Application.Current?.MainWindow));
    }

    public AppPaths Paths { get; }

    public IAppLogger Logger => _logger;

    public DatabaseStartupResult Startup { get; }

    public IFolderRepository Folders { get; }

    public IConnectionRepository Connections { get; }

    public ISettingsStore Settings { get; }

    public ITunnelRepository Tunnels { get; }

    public IConnectionHistoryRepository History { get; }

    public ITagRepository Tags { get; }

    public ICredentialStore Credentials { get; }

    /// <summary>El vault de credenciales. Se abre al arrancar, y hasta entonces no se puede leer ni guardar un secreto.</summary>
    public Vault Vault { get; }

    /// <summary>Sólo para migrar lo que quedó de la versión anterior. No es el almacén.</summary>
    public ICredentialStore AdministradorDeWindows { get; }

    public IAppSettingsService AppSettings { get; }

    public FolderService FolderService { get; }

    public ConnectionService ConnectionService { get; }

    public SessionRegistry Sessions { get; } = new();

    public RegistroDeTrazas Trazas { get; } = new();

    public HerramientasDisponibles Herramientas { get; } =
        new(BuscadorDeHerramientas.DelSistema);

    /// <summary>Resuelve la credencial de una conexión, pidiéndola si falta (FR-039).</summary>
    public ICredentialProvider CredentialProvider { get; }

    public static async Task<CompositionRoot> CreateAsync(string? rootOverride = null)
    {
        var paths = new AppPaths(rootOverride);
        paths.EnsureCreated();

        var logger = new SerilogAppLogger(paths);
        var factory = new SqliteConnectionFactory(paths.DatabasePath);
        var initializer = new DatabaseInitializer(factory, paths, logger);

        var startup = await initializer.InitializeAsync().ConfigureAwait(false);

        return new CompositionRoot(paths, logger, factory, startup);
    }

    public void Dispose() => _logger.Dispose();
}
