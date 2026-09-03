using System.Runtime.Versioning;
using System.Windows;
using CafManagerConection.Ssh;

namespace CafManagerConection.App.Views;

/// <summary>Pregunta qué hacer con un archivo que ya existe en el destino (FR-106).</summary>
[SupportedOSPlatform("windows")]
public partial class ConflictWindow : Window
{
    private ConflictResolution _decision = ConflictResolution.Skip;

    private ConflictWindow(string nombre)
    {
        InitializeComponent();
        _nombre.Text = nombre;
    }

    private void AlSobrescribir(object sender, RoutedEventArgs e) =>
        Decidir(ConflictResolution.Overwrite);

    private void AlConservarAmbos(object sender, RoutedEventArgs e) =>
        Decidir(ConflictResolution.KeepBoth);

    private void AlOmitir(object sender, RoutedEventArgs e) =>
        Decidir(ConflictResolution.Skip);

    private void Decidir(ConflictResolution decision)
    {
        _decision = decision;
        DialogResult = true;
    }

    /// <summary>Devuelve la decisión y si hay que aplicarla al resto de la cola.</summary>
    public static (ConflictResolution Resolution, bool ApplyToAll) Preguntar(
        Window owner, string nombre)
    {
        var ventana = new ConflictWindow(nombre) { Owner = owner };
        var aceptado = ventana.ShowDialog() == true;

        return aceptado
            ? (ventana._decision, ventana._paraTodos.IsChecked == true)
            : (ConflictResolution.Skip, true);
    }
}
