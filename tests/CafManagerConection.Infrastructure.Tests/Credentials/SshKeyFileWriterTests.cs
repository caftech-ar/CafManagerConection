using System.Security.AccessControl;
using System.Security.Principal;
using CafManagerConection.Infrastructure.Credentials;

namespace CafManagerConection.Infrastructure.Tests.Credentials;

public sealed class SshKeyFileWriterTests : IDisposable
{
    private readonly string _carpeta;

    public SshKeyFileWriterTests()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "cmc-ssh-writer-tests-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact]
    public void Guardar_crea_la_carpeta_si_no_existe()
    {
        var escritor = new SshKeyFileWriter(_carpeta);

        Assert.False(Directory.Exists(_carpeta));

        escritor.Guardar("id_prueba", "contenido-de-prueba");

        Assert.True(Directory.Exists(_carpeta));
    }

    [Fact]
    public void Guardar_deja_el_contenido_tal_cual_se_pidio()
    {
        var escritor = new SshKeyFileWriter(_carpeta);

        var ruta = escritor.Guardar("id_prueba", "contenido-de-prueba");

        Assert.Equal("contenido-de-prueba", File.ReadAllText(ruta));
    }

    [Fact]
    public void Guardar_devuelve_la_ruta_completa_dentro_de_la_carpeta_configurada()
    {
        var escritor = new SshKeyFileWriter(_carpeta);

        var ruta = escritor.Guardar("id_prueba", "contenido-de-prueba");

        Assert.Equal(Path.Combine(_carpeta, "id_prueba"), ruta);
    }

    [Fact]
    public void Guardar_no_pisa_un_archivo_existente()
    {
        var escritor = new SshKeyFileWriter(_carpeta);
        escritor.Guardar("id_prueba", "el original");

        var ex = Assert.Throws<IOException>(() => escritor.Guardar("id_prueba", "el nuevo"));

        Assert.Contains("id_prueba", ex.Message, StringComparison.Ordinal);
        Assert.Equal("el original", File.ReadAllText(Path.Combine(_carpeta, "id_prueba")));
    }

    [Fact]
    public void Guardar_dos_nombres_distintos_deja_los_dos_archivos()
    {
        var escritor = new SshKeyFileWriter(_carpeta);

        escritor.Guardar("id_uno", "contenido uno");
        escritor.Guardar("id_dos", "contenido dos");

        Assert.Equal("contenido uno", File.ReadAllText(Path.Combine(_carpeta, "id_uno")));
        Assert.Equal("contenido dos", File.ReadAllText(Path.Combine(_carpeta, "id_dos")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Guardar_rechaza_un_nombre_vacio(string nombre)
    {
        var escritor = new SshKeyFileWriter(_carpeta);

        Assert.Throws<ArgumentException>(() => escritor.Guardar(nombre, "contenido"));
    }

    [Fact]
    public void Guardar_rechaza_un_nombre_con_caracteres_invalidos()
    {
        var escritor = new SshKeyFileWriter(_carpeta);

        Assert.Throws<ArgumentException>(() => escritor.Guardar("id:prueba*", "contenido"));
    }

    [Fact]
    public void Guardar_rechaza_contenido_vacio()
    {
        var escritor = new SshKeyFileWriter(_carpeta);

        Assert.Throws<ArgumentException>(() => escritor.Guardar("id_prueba", ""));
    }

    [Fact]
    public void Guardar_deja_el_archivo_accesible_solo_para_el_usuario_actual()
    {
        var escritor = new SshKeyFileWriter(_carpeta);
        var ruta = escritor.Guardar("id_prueba", "contenido-de-prueba");

        var seguridad = new FileInfo(ruta).GetAccessControl();

        Assert.True(seguridad.AreAccessRulesProtected, "La herencia debería estar cortada.");

        var reglas = seguridad.GetAccessRules(true, false, typeof(SecurityIdentifier));
        var usuarioActual = WindowsIdentity.GetCurrent().User;

        Assert.All(
            reglas.Cast<FileSystemAccessRule>(),
            regla => Assert.Equal(usuarioActual, regla.IdentityReference));

        Assert.Contains(
            reglas.Cast<FileSystemAccessRule>(),
            regla => regla.IdentityReference == usuarioActual
                && regla.AccessControlType == AccessControlType.Allow
                && regla.FileSystemRights.HasFlag(FileSystemRights.FullControl));
    }
}
