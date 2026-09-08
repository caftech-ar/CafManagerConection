using CafManagerConection.App.Views;

namespace CafManagerConection.App.Tests.Views;

public sealed class MainWindowTests
{
    [Fact]
    public void Con_solo_sesiones_no_menciona_tuneles()
    {
        var texto = MainWindow.TextoDeAvisoDeCierre(sesiones: 2, tunelesActivos: 0);

        Assert.Equal("Hay 2 sesión(es) abierta(s). Se van a cerrar.", texto);
    }

    [Fact]
    public void Con_solo_tuneles_no_menciona_sesiones()
    {
        var texto = MainWindow.TextoDeAvisoDeCierre(sesiones: 0, tunelesActivos: 3);

        Assert.Equal("Hay 3 túnel(es) activo(s). Se van a cerrar.", texto);
    }

    [Fact]
    public void Con_las_dos_cosas_las_menciona_juntas()
    {
        var texto = MainWindow.TextoDeAvisoDeCierre(sesiones: 1, tunelesActivos: 2);

        Assert.Equal("Hay 1 sesión(es) abierta(s) y 2 túnel(es) activo(s). Se van a cerrar.", texto);
    }
}
