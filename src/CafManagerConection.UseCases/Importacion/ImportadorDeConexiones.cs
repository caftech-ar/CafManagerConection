using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Importacion;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using CafManagerConection.UseCases.Credentials;
using CafManagerConection.UseCases.Folders;

namespace CafManagerConection.UseCases.Importacion;

public sealed record ResultadoDeImportacion(
    int Creadas,
    int CarpetasCreadas,
    int ContrasenasGuardadas,
    IReadOnlyList<string> YaExistian,
    IReadOnlyList<string> Fallidas,
    IReadOnlyList<string> ContrasenasSinTraer)
{
    /// <summary>Las conexiones que entraron y cuya contraseña no: se cuentan y se dice por qué.</summary>
    public int ContrasenasPerdidas => ContrasenasSinTraer.Count;
}

public sealed class ImportadorDeConexiones(
    IFolderRepository carpetas,
    IConnectionRepository conexiones,
    ConnectionService servicio)
{
    public static string CarpetaRaizDe(OrigenDeImportacion origen) => origen switch
    {
        OrigenDeImportacion.Putty => "Importado de PuTTY",
        OrigenDeImportacion.FileZilla => "Importado de FileZilla",
        OrigenDeImportacion.Rdm => "Importado de Remote Desktop Manager",
        _ => "Importado de WinSCP",
    };

    public async Task<ResultadoDeImportacion> ImportarAsync(
        IReadOnlyList<ConexionImportada> elegidas,
        bool traerContrasenas,
        CancellationToken ct = default)
    {
        var existentes = await conexiones.GetAllAsync(ct).ConfigureAwait(false);
        var arbol = (await carpetas.GetAllAsync(ct).ConfigureAwait(false)).ToList();

        var creadas = 0;
        var carpetasCreadas = 0;
        var contrasenas = 0;
        var yaExistian = new List<string>();
        var fallidas = new List<string>();
        var sinTraer = new List<string>();

        foreach (var importada in elegidas)
        {
            if (YaEsta(existentes, importada))
            {
                yaExistian.Add(importada.Ruta);
                continue;
            }

            var (carpeta, nuevas) = await AsegurarCarpetasAsync(arbol, importada, ct)
                .ConfigureAwait(false);

            carpetasCreadas += nuevas;

            var conContrasena = traerContrasenas;
            OperationResult<Guid> resultado;

            try
            {
                resultado = await CrearAsync(importada, carpeta, conContrasena, ct)
                    .ConfigureAwait(false);
            }
            catch (VaultCerradoException)
            {
                // El vault cerrado no puede costarle la conexión al usuario: entra sin contraseña
                // y se cuenta lo que quedó afuera. Antes, la primera con contraseña abortaba el
                // lote entero y no entraba ninguna.
                conContrasena = false;
                sinTraer.Add($"{importada.Ruta}: el vault estaba cerrado");

                resultado = await CrearAsync(importada, carpeta, conContrasena: false, ct)
                    .ConfigureAwait(false);
            }

            if (!resultado.Success)
            {
                fallidas.Add($"{importada.Ruta}: {resultado.ErrorMessage}");
                continue;
            }

            creadas++;

            if (conContrasena && importada.TieneContrasena)
            {
                contrasenas++;
            }
        }

        return new ResultadoDeImportacion(
            creadas, carpetasCreadas, contrasenas, yaExistian, fallidas, sinTraer);
    }

    // Mismo host, usuario y puerto efectivo: es lo que hace que reimportar no duplique.
    private static bool YaEsta(
        IReadOnlyList<Connection> existentes, ConexionImportada importada) =>
        existentes.Any(c =>
            c.Protocol == Protocol.Ssh
            && string.Equals(c.Host, importada.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.UserName ?? string.Empty, importada.Usuario ?? string.Empty,
                StringComparison.OrdinalIgnoreCase)
            && (c.Port ?? 22) == (importada.Puerto ?? 22));

    private async Task<(Guid? Carpeta, int Creadas)> AsegurarCarpetasAsync(
        List<Folder> arbol, ConexionImportada importada, CancellationToken ct)
    {
        Guid? padre = null;
        var creadas = 0;

        var camino = new List<string> { CarpetaRaizDe(importada.Origen) };
        camino.AddRange(importada.Carpetas);

        foreach (var nombre in camino)
        {
            var existente = arbol.FirstOrDefault(f =>
                f.ParentId == padre
                && string.Equals(f.Name, nombre, StringComparison.OrdinalIgnoreCase));

            if (existente is not null)
            {
                padre = existente.Id;
                continue;
            }

            var nueva = await CrearCarpetaAlfabeticaAsync(arbol, nombre, padre, ct)
                .ConfigureAwait(false);

            arbol.Add(nueva);
            padre = nueva.Id;
            creadas++;
        }

        return (padre, creadas);
    }

    /// <summary>La carpeta importada entra en su lugar alfabético, no al final.</summary>
    private async Task<Folder> CrearCarpetaAlfabeticaAsync(
        List<Folder> arbol, string nombre, Guid? padre, CancellationToken ct)
    {
        var hermanas = arbol
            .Where(f => f.ParentId == padre)
            .OrderBy(f => f.SortOrder)
            .ToList();

        var lugar = OrdenAlfabetico.Posicion([.. hermanas.Select(f => f.Name)], nombre);
        var nueva = new Folder(Guid.NewGuid(), nombre, padre, lugar);

        var orden = hermanas.Select(f => f.Id).ToList();
        orden.Insert(lugar, nueva.Id);

        await carpetas.ReorderAsync(padre, orden, ct).ConfigureAwait(false);
        await carpetas.AddAsync(nueva, ct).ConfigureAwait(false);

        for (var i = 0; i < hermanas.Count; i++)
        {
            hermanas[i].SortOrder = orden.IndexOf(hermanas[i].Id);
        }

        return nueva;
    }

    private Task<OperationResult<Guid>> CrearAsync(
        ConexionImportada importada,
        Guid? carpeta,
        bool conContrasena,
        CancellationToken ct)
    {
        var protocolo = importada.Protocolo switch
        {
            ProtocoloImportado.Rdp => Protocol.Rdp,
            ProtocoloImportado.Web => Protocol.Web,
            _ => Protocol.Ssh,
        };

        var conexion = new Connection(
            Guid.NewGuid(), importada.Nombre, protocolo, importada.Host)
        {
            FolderId = carpeta,
            UserName = importada.Usuario,
            Notes = Notas(importada),
        };

        conexion.SetPort(importada.Puerto);

        var registro = protocolo switch
        {
            Protocol.Ssh => new ConnectionRecord(
                conexion,
                Ssh: new SshSettings
                {
                    ConnectionId = conexion.Id,
                    AuthMethod = importada.RutaDeClavePrivada is null
                        ? SshAuthMethod.Password
                        : SshAuthMethod.PrivateKey,
                    PrivateKeyPath = importada.RutaDeClavePrivada,
                }),
            Protocol.Rdp => new ConnectionRecord(
                conexion,
                Rdp: new RdpSettings
                {
                    ConnectionId = conexion.Id,
                    Domain = importada.Dominio,
                }),
            Protocol.Web => new ConnectionRecord(
                conexion,
                Web: new WebSettings
                {
                    ConnectionId = conexion.Id,
                    Url = importada.Url ?? string.Empty,
                }),
            _ => new ConnectionRecord(conexion),
        };

        var credencial = conContrasena && importada.Credencial is { HasSecret: true } secreto
            ? new CredentialPromptResult(
                importada.Usuario ?? string.Empty, null, secreto.RevealSecret(), Remember: true)
            : null;

        return servicio.CreateAsync(registro, credencial, ct);
    }

    private static string? Notas(ConexionImportada importada)
    {
        var lineas = new List<string>
        {
            $"Importado de {importada.Origen} como {importada.ProtocoloOriginal}.",
        };

        lineas.AddRange(importada.AdvertenciasOVacio);

        return string.Join(Environment.NewLine, lineas);
    }
}
