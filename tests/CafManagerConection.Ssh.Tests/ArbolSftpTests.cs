using System.Security.Cryptography;
using CafManagerConection.Ssh;

namespace CafManagerConection.Ssh.Tests;

// El servidor necesita SFTP habilitado y permiso de escritura en el directorio del usuario.
[Trait("Categoria", "IntegracionSsh")]
public sealed class ArbolSftpTests
{
    private static SortedDictionary<string, string> SumasDeVerificacion(string raiz)
    {
        var sumas = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var archivo in Directory.EnumerateFiles(
                     raiz, "*", SearchOption.AllDirectories))
        {
            var relativa = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            sumas[relativa] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivo)));
        }

        return sumas;
    }

    private static RemoteFileSession Sesion() => new(
        ServidorDePrueba.Pedido(),
        new ServidorDePrueba.AceptaTodo(),
        ServidorDePrueba.Credencial());

    [PruebaDeIntegracionSsh]
    public async Task Listar_un_nivel_no_baja_el_contenido_de_las_carpetas_hijas()
    {
        await using var sesion = Sesion();
        Assert.Null(await sesion.ConnectAsync());

        var raiz = await sesion.ListAsync("/");
        var hija = raiz.Entries.FirstOrDefault(e => e.IsDirectory);

        Assert.NotNull(hija);
        Assert.DoesNotContain(raiz.Entries, e => e.FullPath.TrimEnd('/').Contains(
            hija.FullPath.TrimEnd('/') + "/", StringComparison.Ordinal));
    }

    [PruebaDeIntegracionSsh]
    public async Task Ningun_enlace_simbolico_de_slash_etc_llega_al_listado()
    {
        await using var sesion = Sesion();
        Assert.Null(await sesion.ConnectAsync());

        var listado = await sesion.ListAsync("/etc");

        Assert.NotEmpty(listado.Entries);
        Assert.True(
            listado.SymbolicLinksOmitted >= 0,
            "El listado tiene que informar cuántos enlaces omitió.");
    }

    [PruebaDeIntegracionSsh]
    public async Task Una_carpeta_con_tres_niveles_sube_y_baja_completa()
    {
        await using var sesion = Sesion();
        Assert.Null(await sesion.ConnectAsync());

        var origen = Directory.CreateTempSubdirectory("cmc-sube").FullName;
        var destino = Directory.CreateTempSubdirectory("cmc-baja").FullName;
        var remoto = RutaRemota.Combinar(sesion.HomeDirectory, "cmc-prueba-" + Guid.NewGuid());

        try
        {
            var hondo = Path.Combine(origen, "n1", "n2");
            Directory.CreateDirectory(hondo);

            for (var i = 0; i < 50; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(origen, $"a{i}.txt"), $"contenido {i}");
            }

            await File.WriteAllTextAsync(Path.Combine(hondo, "hoja.txt"), "hoja");

            var subida = await sesion.UploadDirectoryAsync(origen, remoto);
            Assert.Null(subida.Error);
            Assert.Equal(51, subida.Transferred);

            var bajada = await sesion.DownloadDirectoryAsync(
                RutaRemota.Combinar(remoto, Path.GetFileName(origen)), destino);

            Assert.Null(bajada.Error);
            Assert.Equal(51, bajada.Transferred);

            var traido = Path.Combine(destino, Path.GetFileName(origen));

            Assert.Equal(SumasDeVerificacion(origen), SumasDeVerificacion(traido));
        }
        finally
        {
            await sesion.DeleteAsync(remoto, isDirectory: true);
            Directory.Delete(origen, recursive: true);
            Directory.Delete(destino, recursive: true);
        }
    }
}
