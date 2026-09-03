using System.Text;
using Xunit;

namespace CafManagerConection.Domain.Tests;

// El instalador mostraba «AplicaciÃ³n» en lugar de «Aplicación». La causa no era el texto: era
// que makensis decide la codificacion del fuente por el BOM, y sin BOM lo lee en la pagina ANSI
// del sistema. `Unicode true` no alcanza: eso rige el ejecutable generado, no como se lee el .nsi.
public sealed class InstaladorLegibleTests
{
    private static readonly byte[] BomUtf8 = [0xEF, 0xBB, 0xBF];

    [Theory]
    [MemberData(nameof(Guiones))]
    public void Todo_guion_del_instalador_con_texto_acentuado_empieza_con_BOM(string relativa)
    {
        var ruta = Path.Combine(Repositorio.Raiz(), relativa);
        var bytes = File.ReadAllBytes(ruta);

        Assert.True(
            bytes.Length >= 3 && bytes[0] == BomUtf8[0] && bytes[1] == BomUtf8[1]
            && bytes[2] == BomUtf8[2],
            $"{relativa} no arranca con BOM UTF-8, así que makensis va a leer sus acentos en la "
            + "página ANSI y el instalador los va a mostrar mal.");
    }

    [Theory]
    [MemberData(nameof(Guiones))]
    public void El_texto_acentuado_esta_en_UTF8_y_no_en_la_pagina_ANSI(string relativa)
    {
        var ruta = Path.Combine(Repositorio.Raiz(), relativa);
        var bytes = File.ReadAllBytes(ruta);

        var estricto = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        // Si el archivo estuviera en CP1252, un acento seria un byte suelto >= 0x80 y esto tira.
        var texto = estricto.GetString(bytes);

        Assert.Contains("ó", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void El_instalador_declara_Unicode()
    {
        var ruta = Path.Combine(Repositorio.Raiz(), "installer", "CafManagerConection.nsi");

        Assert.Contains("Unicode true", File.ReadAllText(ruta), StringComparison.Ordinal);
    }

    public static TheoryData<string> Guiones()
    {
        var datos = new TheoryData<string>();
        var carpeta = Path.Combine(Repositorio.Raiz(), "installer");

        if (!Directory.Exists(carpeta))
        {
            return datos;
        }

        foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(archivo) is not (".nsi" or ".nsh"))
            {
                continue;
            }

            if (File.ReadAllBytes(archivo).Any(b => b >= 0x80))
            {
                datos.Add(Path.GetRelativePath(Repositorio.Raiz(), archivo));
            }
        }

        return datos;
    }
}
