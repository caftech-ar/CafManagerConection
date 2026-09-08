using CafManagerConection.Domain.Credentials;

namespace CafManagerConection.Domain.Importacion;

public enum OrigenDeImportacion
{
    WinScpRegistro,
    WinScpIni,
    FileZilla,
    Putty,
    Rdm,
}

public sealed record ConexionImportada(
    OrigenDeImportacion Origen,
    string Nombre,
    IReadOnlyList<string> Carpetas,
    string Host,
    int? Puerto,
    string? Usuario,
    string? RutaDeClavePrivada,
    string ProtocoloOriginal,
    StoredCredential? Credencial = null,
    IReadOnlyList<string>? Advertencias = null,
    ProtocoloImportado Protocolo = ProtocoloImportado.Ssh,
    string? Url = null,
    string? Dominio = null)
{
    public IReadOnlyList<string> AdvertenciasOVacio => Advertencias ?? [];

    public bool TieneContrasena => Credencial is { HasSecret: true };

    public string Ruta => Carpetas.Count == 0
        ? Nombre
        : string.Join(" › ", Carpetas) + " › " + Nombre;
}

/// <summary>Los tres orígenes de clientes SSH sólo traen SSH; Remote Desktop Manager trae además RDP y direcciones web, que también están en alcance.</summary>
public enum ProtocoloImportado
{
    Ssh,
    Rdp,
    Web,
}

public sealed record ImportacionOmitida(
    OrigenDeImportacion Origen, string Nombre, string Motivo);

public sealed class LecturaDeImportacion(
    IReadOnlyList<ConexionImportada> compatibles,
    IReadOnlyList<ImportacionOmitida> omitidas) : IDisposable
{
    public static LecturaDeImportacion Vacia { get; } = new([], []);

    public IReadOnlyList<ConexionImportada> Compatibles { get; } = compatibles;

    public IReadOnlyList<ImportacionOmitida> Omitidas { get; } = omitidas;

    public int ConContrasena => Compatibles.Count(c => c.TieneContrasena);

    public void Dispose()
    {
        foreach (var conexion in Compatibles)
        {
            conexion.Credencial?.Dispose();
        }
    }
}
