using CafManagerConection.Domain.Connections;
using CafManagerConection.Import;
using CafManagerConection.Infrastructure.Configuration;
using CafManagerConection.Infrastructure.Database;
using CafManagerConection.UseCases.Abstractions;

// Herramienta de migración puntual: lee un export de Remote Desktop Manager y vuelca las
// carpetas y conexiones en la base de CMC. Por omisión NO escribe nada: hay que pedirlo
// con --apply, después de revisar la vista previa.

var archivo = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
var aplicar = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Modo estadísticas: informa el estado de la base local, sin tocar el export ni escribir.
if (args.Contains("--stats", StringComparer.OrdinalIgnoreCase))
{
    await MostrarEstadoAsync();
    return 0;
}

if (string.IsNullOrWhiteSpace(archivo))
{
    Console.Error.WriteLine("Falta la ruta del XML exportado desde Remote Desktop Manager.");
    Console.Error.WriteLine("Uso: CafManagerConection.Import <ruta-del-export.xml> [--apply]");
    return 1;
}

if (!File.Exists(archivo))
{
    Console.Error.WriteLine($"No se encontró el archivo: {archivo}");
    return 1;
}

var plan = RdmXmlParser.Parse(await File.ReadAllTextAsync(archivo));

// ------------------------------------------------------------------- vista previa

Console.WriteLine();
Console.WriteLine($"Origen: {Path.GetFullPath(archivo)}");
Console.WriteLine();
Console.WriteLine($"  Carpetas a crear ....... {plan.FolderPaths.Count}");
Console.WriteLine($"  Conexiones a importar .. {plan.Connections.Count}");

foreach (var grupo in plan.Connections.GroupBy(c => c.Protocol).OrderByDescending(g => g.Count()))
{
    Console.WriteLine($"      {grupo.Key,-5} {grupo.Count()}");
}

var tuneles = plan.Connections.Sum(c => c.Tunnels.Count);
if (tuneles > 0)
{
    Console.WriteLine($"  Túneles a importar ..... {tuneles}");
}

Console.WriteLine($"  Entradas omitidas ...... {plan.Skipped.Count}");
foreach (var motivo in plan.Skipped.GroupBy(s => s.Reason).OrderByDescending(g => g.Count()))
{
    Console.WriteLine($"      {motivo.Count(),3}  {motivo.Key}");
}

if (plan.NonLocalTunnelCount > 0)
{
    Console.WriteLine();
    Console.WriteLine($"  {plan.NonLocalTunnelCount} reenvío(s) de puerto NO se migran: son de tipo");
    Console.WriteLine("  remoto o dinámico, y CMC sólo hace reenvío de puerto local.");
}

Console.WriteLine();
Console.WriteLine($"  Contraseñas en el export: {plan.EncryptedPasswordCount}, todas cifradas.");
Console.WriteLine("  NO se migran: Remote Desktop Manager las cifra con la clave de su");
Console.WriteLine("  data source y no se pueden descifrar desde afuera. Hay que volver a");
Console.WriteLine("  cargarlas. La herencia por carpeta de CMC hace que alcance con");
Console.WriteLine("  cargarlas una vez por grupo de servidores que comparta credencial.");
Console.WriteLine();

if (!aplicar)
{
    Console.WriteLine("Vista previa: no se escribió nada.");
    Console.WriteLine("Para importar de verdad:  task import -- --apply");
    return 0;
}

// ------------------------------------------------------------------------ importar

var paths = new AppPaths();
paths.EnsureCreated();

var factory = new SqliteConnectionFactory(paths.DatabasePath);
await new DatabaseInitializer(factory, paths).InitializeAsync();

var folderRepo = new FolderRepository(factory);
var connectionRepo = new ConnectionRepository(factory);
var tunnelRepo = new TunnelRepository(factory);

var existentes = await folderRepo.GetAllAsync();
var existentesPorRuta = BuildPathMap(existentes);
var creadas = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

// Las rutas vienen ordenadas alfabeticamente, y por construccion una ruta padre siempre
// ordena antes que sus hijas, asi que el padre ya existe cuando toca crear la hija.
foreach (var ruta in plan.FolderPaths)
{
    if (existentesPorRuta.TryGetValue(ruta, out var yaExiste))
    {
        creadas[ruta] = yaExiste;
        continue;
    }

    var corte = ruta.LastIndexOf('\\');
    var nombre = corte < 0 ? ruta : ruta[(corte + 1)..];
    var rutaPadre = corte < 0 ? null : ruta[..corte];

    Guid? padreId = rutaPadre is not null && creadas.TryGetValue(rutaPadre, out var p) ? p : null;

    var folder = new Folder(Guid.NewGuid(), nombre, padreId);
    await folderRepo.AddAsync(folder);
    creadas[ruta] = folder.Id;
}

var importadas = 0;
var tunelesImportados = 0;
var fallidas = new List<string>();

foreach (var c in plan.Connections)
{
    try
    {
        Guid? folderId = !string.IsNullOrWhiteSpace(c.FolderPath) &&
                         creadas.TryGetValue(c.FolderPath, out var f) ? f : null;

        var conexion = new Connection(Guid.NewGuid(), c.Name, c.Protocol, c.Host)
        {
            FolderId = folderId,
            UserName = c.UserName,
        };
        conexion.SetPort(c.Port);

        var record = c.Protocol switch
        {
            Protocol.Rdp => new ConnectionRecord(
                conexion, Rdp: new RdpSettings { ConnectionId = conexion.Id }),
            Protocol.Web => new ConnectionRecord(
                conexion,
                Web: new WebSettings
                {
                    ConnectionId = conexion.Id,
                    Url = c.Url ?? string.Empty,
                    Browser = c.Browser,
                }),
            _ => new ConnectionRecord(
                conexion, Ssh: new SshSettings { ConnectionId = conexion.Id }),
        };

        await connectionRepo.AddAsync(record);
        importadas++;

        foreach (var t in c.Tunnels)
        {
            await tunnelRepo.AddAsync(new SshTunnel(
                Guid.NewGuid(), conexion.Id, t.Name, t.LocalPort, t.RemoteHost, t.RemotePort));
            tunelesImportados++;
        }
    }
    catch (Exception ex)
    {
        fallidas.Add($"{c.Name}: {ex.Message}");
    }
}

Console.WriteLine($"Importadas {importadas} conexiones y {creadas.Count} carpetas.");

if (tunelesImportados > 0)
{
    Console.WriteLine($"Importados {tunelesImportados} túneles.");
}

if (fallidas.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"No se pudieron importar {fallidas.Count}:");
    foreach (var f in fallidas)
    {
        Console.WriteLine($"  - {f}");
    }
}

Console.WriteLine();
Console.WriteLine($"Base: {paths.DatabasePath}");
return 0;

// Estado de la base local. Sólo lectura: cuenta y agrupa, no modifica nada.
static async Task MostrarEstadoAsync()
{
    var paths = new AppPaths();

    Console.WriteLine();
    Console.WriteLine("Datos locales de CafManagerConection");
    Console.WriteLine($"  Carpeta    {paths.Root}");

    if (!File.Exists(paths.DatabasePath))
    {
        Console.WriteLine("  Base       todavía no existe. Ejecutá la aplicación una vez.");
        return;
    }

    var tamano = new FileInfo(paths.DatabasePath).Length / 1024.0;
    Console.WriteLine($"  Base       {paths.DatabasePath} ({tamano:F1} KB)");

    if (Directory.Exists(paths.LogsDirectory))
    {
        var n = Directory.GetFiles(paths.LogsDirectory, "*.log").Length;
        Console.WriteLine($"  Registros  {paths.LogsDirectory} ({n} archivo(s))");
    }

    var factory = new SqliteConnectionFactory(paths.DatabasePath);
    var folders = await new FolderRepository(factory).GetAllAsync();
    var connections = await new ConnectionRepository(factory).GetAllAsync();
    var tunnels = await new TunnelRepository(factory).GetAllAsync();

    Console.WriteLine();
    Console.WriteLine($"  Carpetas ............ {folders.Count}");
    Console.WriteLine($"  Conexiones .......... {connections.Count}");

    foreach (var g in connections.GroupBy(c => c.Protocol).OrderByDescending(g => g.Count()))
    {
        Console.WriteLine($"      {g.Key,-4} {g.Count()}");
    }

    Console.WriteLine($"  Túneles ............. {tunnels.Count}");

    var conCredencial = connections.Count(c => c.CredentialKey is not null);
    var conCarpeta = folders.Count(f => !f.Settings.IsEmpty);

    Console.WriteLine($"  Con credencial ...... {conCredencial} de {connections.Count}");
    Console.WriteLine($"  Carpetas con config . {conCarpeta} de {folders.Count}");

    if (conCredencial < connections.Count)
    {
        Console.WriteLine();
        Console.WriteLine($"  Faltan {connections.Count - conCredencial} credenciales por cargar.");
        Console.WriteLine("  Conviene cargarlas en la carpeta y dejar que se hereden, en lugar");
        Console.WriteLine("  de repetirlas en cada conexión.");
    }

    Console.WriteLine();
}

// Reconstruye la ruta completa de cada carpeta ya existente, para no duplicarlas al
// reimportar.
static Dictionary<string, Guid> BuildPathMap(IReadOnlyList<Folder> folders)
{
    var byId = folders.ToDictionary(f => f.Id);
    var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

    foreach (var folder in folders)
    {
        var partes = new List<string>();
        var actual = folder;
        var visitados = new HashSet<Guid>();

        while (actual is not null && visitados.Add(actual.Id))
        {
            partes.Insert(0, actual.Name);
            actual = actual.ParentId is { } p && byId.TryGetValue(p, out var padre) ? padre : null;
        }

        result[string.Join('\\', partes)] = folder.Id;
    }

    return result;
}
