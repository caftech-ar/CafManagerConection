namespace CafManagerConection.App.Tests.Panels;

// FR-183a: el panel de procesos es de sólo lectura. Se lee el fuente como texto, igual que
// EstilosAplicadosTests: una acción destructiva agregada sin querer no la ve ninguna otra prueba,
// porque la interfaz WPF no tiene arnés.
public sealed class ProcesosPanelDeSoloLecturaTests
{
    /// <summary>Cómo se escribiría en el servidor lo que este panel no debe poder hacer.</summary>
    private static readonly string[] Destructivas =
    [
        "kill ",
        "kill -",
        "pkill",
        "killall",
        "renice",
        "nice -n",
        "SIGTERM",
        "SIGKILL",
        "SIGHUP",
    ];

    private static string Raiz()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (Directory.Exists(Path.Combine(directorio.FullName, "src")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException(
            $"No se encontró la raíz del repositorio subiendo desde {AppContext.BaseDirectory}.");
    }

    private static string Fuente(string archivo) => File.ReadAllText(
        Path.Combine(Raiz(), "src", "CafManagerConection.App", "Panels", archivo));

    [Fact]
    public void El_panel_de_procesos_no_ofrece_ninguna_accion_destructiva()
    {
        var fuentes = Fuente("ProcesosPanel.xaml") + Fuente("ProcesosPanel.xaml.cs");

        var encontradas = Destructivas
            .Where(v => fuentes.Contains(v, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(encontradas);
    }

    // El único botón del panel es el de escalar privilegios, que también es de lectura (FR-184b).
    [Fact]
    public void El_unico_boton_del_panel_es_el_de_escalar()
    {
        var xaml = Fuente("ProcesosPanel.xaml");

        Assert.Equal(1, xaml.Split("Click=\"").Length - 1);
        Assert.Contains("Click=\"AlEscalar\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ContextMenu", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void El_panel_de_procesos_dice_que_es_de_solo_lectura()
    {
        Assert.Contains("Sólo lectura", Fuente("ProcesosPanel.xaml"), StringComparison.Ordinal);
    }

    // Lo único que el panel ejecuta en el servidor es la lectura de /proc del ColectorDeProcesos.
    [Fact]
    public void El_panel_de_procesos_no_ejecuta_comandos_propios()
    {
        var codigo = Fuente("ProcesosPanel.xaml.cs");

        Assert.DoesNotContain("RunAsync", codigo, StringComparison.Ordinal);
        Assert.DoesNotContain("RunWithSudo", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void Las_busquedas_de_esta_prueba_encuentran_el_panel()
    {
        Assert.Contains("ProcesosPanel", Fuente("ProcesosPanel.xaml"), StringComparison.Ordinal);
        Assert.NotEmpty(Destructivas);
    }
}
