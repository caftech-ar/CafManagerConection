using System.Text.RegularExpressions;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.Domain.Tests;

public sealed class CatalogoDeIconosTests
{
    private const string CarpetaDeIconos = "CafManagerConection.App/Assets/Iconos";

    [Fact]
    public void El_catalogo_tiene_una_entrada_por_cada_svg_del_repositorio()
    {
        var enDisco = SvgDelRepositorio()
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);

        var enElCatalogo = CatalogoDeIconos.Iconos
            .Select(icono => icono.Clave)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(enDisco.Except(enElCatalogo));
        Assert.Empty(enElCatalogo.Except(enDisco));
    }

    [Fact]
    public void Ninguna_clave_se_repite()
    {
        var claves = CatalogoDeIconos.Iconos.Select(icono => icono.Clave).ToList();

        Assert.Equal(claves.Count, claves.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Toda_clave_del_catalogo_resuelve_a_una_geometria()
    {
        var geometrias = ClavesDeLosDiccionariosGenerados();

        foreach (var icono in CatalogoDeIconos.Iconos)
        {
            Assert.True(
                geometrias.Contains(icono.ClaveDeRecurso),
                $"«{icono.Clave}» no tiene geometría en ningún Themes/Iconos.*.xaml.");
        }
    }

    [Fact]
    public void Los_diccionarios_generados_no_declaran_geometrias_que_el_catalogo_no_nombre()
    {
        var delCatalogo = CatalogoDeIconos.Iconos
            .Select(icono => icono.ClaveDeRecurso)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var clave in ClavesDeLosDiccionariosGenerados())
        {
            Assert.True(delCatalogo.Contains(clave), $"«{clave}» está generada pero no está en el catálogo.");
        }
    }

    [Fact]
    public void El_origen_que_declara_el_catalogo_es_el_que_dice_el_svg()
    {
        foreach (var archivo in SvgDelRepositorio())
        {
            var clave = Path.GetFileNameWithoutExtension(archivo);
            var icono = CatalogoDeIconos.Resolver(clave);
            Assert.NotNull(icono);

            var declarado = Regex.Match(File.ReadAllText(archivo), "<!--\\s*(.+?)\\s*-->").Groups[1].Value;

            Assert.Equal(icono.Origen.ToString(), declarado);
        }
    }

    [Fact]
    public void Cada_svg_esta_en_la_carpeta_de_su_grupo()
    {
        foreach (var archivo in SvgDelRepositorio())
        {
            var icono = CatalogoDeIconos.Resolver(Path.GetFileNameWithoutExtension(archivo));
            Assert.NotNull(icono);

            var familia = icono.Grupo.Familia == FamiliaDeIcono.Concepto ? "conceptos" : "logos";
            var esperada = Path.Combine(familia, icono.Grupo.Clave);

            Assert.Contains(esperada, Path.GetDirectoryName(archivo), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("tabler", ModoDePintado.Trazo)]
    [InlineData("simple-icons", ModoDePintado.Relleno)]
    [InlineData("devicon", ModoDePintado.Relleno)]
    public void El_modo_de_pintado_lo_define_el_paquete_de_origen(string paquete, ModoDePintado esperado)
    {
        var delPaquete = CatalogoDeIconos.Iconos
            .Where(icono => icono.Origen.Paquete.Contains(paquete, StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(delPaquete);
        Assert.All(delPaquete, icono => Assert.Equal(esperado, icono.Modo));
    }

    [Fact]
    public void El_icono_de_desconocido_existe_en_el_catalogo()
    {
        Assert.True(CatalogoDeIconos.EsValido(CatalogoDeIconos.ClaveDesconocido));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("carpeta")]
    [InlineData("base-de-datos")]
    [InlineData("no-existe")]
    public void Una_clave_que_el_catalogo_no_tiene_devuelve_null(string? clave)
    {
        Assert.Null(CatalogoDeIconos.Resolver(clave));
        Assert.False(CatalogoDeIconos.EsValido(clave));
    }

    [Fact]
    public void Los_grupos_del_catalogo_son_los_que_tienen_carpeta()
    {
        var enDisco = SvgDelRepositorio()
            .Select(archivo => Path.GetFileName(Path.GetDirectoryName(archivo))!)
            .ToHashSet(StringComparer.Ordinal);

        var enElCatalogo = CatalogoDeIconos.Grupos
            .Select(grupo => grupo.Clave)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(enDisco, enElCatalogo);
    }

    // «Acciones» es chrome entero; «Organización y navegación» mezcla glifos de la interfaz con
    // carpeta, etiqueta y favorito, que sí sirven para una conexión.
    [Theory]
    [InlineData("search")]
    [InlineData("filter")]
    [InlineData("dots-vertical")]
    [InlineData("chevron-right")]
    [InlineData("plus")]
    [InlineData("trash")]
    [InlineData("minimize")]
    public void Un_glifo_de_la_propia_interfaz_no_se_ofrece(string clave) =>
        Assert.False(CatalogoDeIconos.Resolver(clave)!.EnElSelector, clave);

    [Theory]
    [InlineData("folder")]
    [InlineData("star")]
    [InlineData("tag")]
    [InlineData("bookmark")]
    [InlineData("server")]
    [InlineData("postgresql")]
    public void Un_icono_que_identifica_algo_si_se_ofrece(string clave) =>
        Assert.True(CatalogoDeIconos.Resolver(clave)!.EnElSelector, clave);

    [Fact]
    public void El_grupo_de_acciones_no_ofrece_ni_uno()
    {
        var acciones = CatalogoDeIconos.Iconos.Where(i => i.Grupo == CatalogoDeIconos.Acciones);

        Assert.NotEmpty(acciones);
        Assert.All(acciones, icono => Assert.False(icono.EnElSelector, icono.Clave));
    }

    [Fact]
    public void Toda_entrada_tiene_etiqueta()
    {
        Assert.All(CatalogoDeIconos.Iconos, icono => Assert.False(string.IsNullOrWhiteSpace(icono.Etiqueta)));
    }

    private static IEnumerable<string> SvgDelRepositorio() =>
        Directory.EnumerateFiles(
            Path.Combine(Repositorio.Raiz(), "src", CarpetaDeIconos.Replace('/', Path.DirectorySeparatorChar)),
            "*.svg",
            SearchOption.AllDirectories);

    private static HashSet<string> ClavesDeLosDiccionariosGenerados()
    {
        var claves = new HashSet<string>(StringComparer.Ordinal);

        foreach (var archivo in Repositorio.ArchivosDe("CafManagerConection.App", "Iconos.*.xaml"))
        {
            foreach (Match coincidencia in Regex.Matches(File.ReadAllText(archivo), "x:Key=\"(Icono\\.[^\"]+)\""))
            {
                claves.Add(coincidencia.Groups[1].Value);
            }
        }

        return claves;
    }
}
