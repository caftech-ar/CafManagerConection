using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace CafManagerConection.Infrastructure.Credentials;

/// <summary>Escribe la clave privada pegada en <c>%USERPROFILE%\.ssh</c> con los permisos que exige OpenSSH; la base guarda la ruta y nunca el contenido (Principio II).</summary>
[SupportedOSPlatform("windows")]
public sealed class SshKeyFileWriter
{
    public SshKeyFileWriter(string? carpetaSsh = null)
    {
        CarpetaSsh = carpetaSsh ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
    }

    public string CarpetaSsh { get; }

    /// <exception cref="IOException">Ya existe un archivo con ese nombre. Nunca se pisa: podría dejar sin acceso a un servidor que todavía usa el viejo.</exception>
    public string Guardar(string nombreArchivo, string contenido)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreArchivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(contenido);

        if (nombreArchivo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "El nombre de archivo tiene caracteres que Windows no admite.",
                nameof(nombreArchivo));
        }

        Directory.CreateDirectory(CarpetaSsh);

        var ruta = Path.Combine(CarpetaSsh, nombreArchivo);

        if (File.Exists(ruta))
        {
            throw new IOException(
                $"Ya existe un archivo llamado «{nombreArchivo}» en {CarpetaSsh}. " +
                "Elegí otro nombre: no se reemplaza uno existente.");
        }

        var bytes = Encoding.UTF8.GetBytes(contenido);

        try
        {
            // FileMode.CreateNew además del Exists de arriba: cierra la condición de carrera entre el chequeo y la escritura.
            using (var flujo = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write))
            {
                flujo.Write(bytes, 0, bytes.Length);
            }

            AplicarPermisosCerrados(ruta);

            return ruta;
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    // OpenSSH rechaza una clave con permisos abiertos («UNPROTECTED PRIVATE KEY FILE»), así que sin este paso el archivo queda inservible.
    private static void AplicarPermisosCerrados(string ruta)
    {
        var usuarioActual = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException(
                "No se pudo determinar el identificador del usuario actual.");

        // Una FileSecurity nueva y no la de GetAccessControl: SetAccessRuleProtection sólo corta la herencia hacia adelante y las reglas ya puestas seguirían ahí.
        var seguridad = new FileSecurity();
        seguridad.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        seguridad.AddAccessRule(new FileSystemAccessRule(
            usuarioActual,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        new FileInfo(ruta).SetAccessControl(seguridad);
    }
}
