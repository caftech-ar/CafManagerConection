using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CafManagerConection.App.Services;
using CafManagerConection.Domain.Settings;

namespace CafManagerConection.App.Views;

/// <summary>Elegir un icono del catálogo, con buscador y filtros por grupo y por familia.</summary>
[SupportedOSPlatform("windows")]
public partial class SelectorDeIconosWindow : Window
{
    private const string TodosLosGrupos = "Todos los grupos";

    private readonly CollectionViewSource _vista = new();

    /// <summary>Clave elegida, o null si se pidió volver al icono por omisión.</summary>
    public string? IconoElegido { get; private set; }

    /// <param name="claveActual">La que está elegida hoy, para marcarla al abrir.</param>
    public SelectorDeIconosWindow(string? claveActual)
    {
        InitializeComponent();

        Temas.AplicarBarraDeTitulo(this);

        _grupo.Items.Add(TodosLosGrupos);

        foreach (var grupo in CatalogoDeIconos.Grupos.Where(
                     g => CatalogoDeIconos.Iconos.Any(i => i.Grupo == g && i.EnElSelector)))
        {
            _grupo.Items.Add(grupo.Nombre);
        }

        _grupo.SelectedIndex = 0;

        _vista.GroupDescriptions.Add(new PropertyGroupDescription("Grupo.Nombre"));

        // Marcado por código y no en el XAML: ahí el Checked corre durante InitializeComponent(),
        // cuando los campos con x:Name todavía son null.
        _todo.IsChecked = true;

        Refiltrar();

        if (CatalogoDeIconos.Resolver(claveActual) is { EnElSelector: true } actual)
        {
            _grilla.SelectedItem = actual;
            _grilla.ScrollIntoView(actual);
        }

        Loaded += (_, _) => _busqueda.Focus();
        PreviewKeyDown += AlPresionarTecla;
    }

    private GrupoDeIconos? GrupoElegido =>
        _grupo.SelectedIndex <= 0
            ? null
            : CatalogoDeIconos.Grupos.FirstOrDefault(g => g.Nombre == (string)_grupo.SelectedItem);

    private FamiliaDeIcono? FamiliaElegida =>
        _conceptos.IsChecked == true ? FamiliaDeIcono.Concepto
        : _logos.IsChecked == true ? FamiliaDeIcono.Logo
        : null;

    private void AlEscribir(object sender, TextChangedEventArgs e) => Refiltrar();

    private void AlCambiarLaFamilia(object sender, RoutedEventArgs e) => Refiltrar();

    private void AlCambiarElGrupo(object sender, SelectionChangedEventArgs e) => Refiltrar();

    private void AlCambiarLaSeleccion(object sender, SelectionChangedEventArgs e) =>
        _elegir.IsEnabled = _grilla.SelectedItem is IconoDelCatalogo;

    private void AlHacerDobleClic(object sender, MouseButtonEventArgs e) => Aceptar();

    private void AlElegir(object sender, RoutedEventArgs e) => Aceptar();

    private void AlQuitarElIcono(object sender, RoutedEventArgs e)
    {
        IconoElegido = null;
        DialogResult = true;
    }

    private void Refiltrar()
    {
        var encontrados = BuscadorDeIconos.Filtrar(_busqueda.Text, GrupoElegido, FamiliaElegida);

        // Fijar la fuente crea una vista nueva, asi que la grilla tiene que volver a tomarla.
        _vista.Source = encontrados;
        _grilla.ItemsSource = _vista.View;

        var hayAlgo = encontrados.Count > 0;

        _grilla.Visibility = hayAlgo ? Visibility.Visible : Visibility.Collapsed;
        _vacio.Visibility = hayAlgo ? Visibility.Collapsed : Visibility.Visible;
        _vacio.Text = $"Ningún icono coincide con «{_busqueda.Text.Trim()}».";

        _cuenta.Text = hayAlgo
            ? $"{encontrados.Count} de {CuantosOfreceElSelector} · ↑↓ recorrer · Enter elegir · Esc cancelar"
            : string.Empty;

        _elegir.IsEnabled = _grilla.SelectedItem is IconoDelCatalogo;
    }

    private static int CuantosOfreceElSelector =>
        CatalogoDeIconos.Iconos.Count(i => i.EnElSelector);

    // Desde el buscador, bajar lleva a la grilla: se abre, se escribe y se baja sin tocar el ratón.
    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && _busqueda.IsKeyboardFocusWithin && _grilla.Items.Count > 0)
        {
            _grilla.SelectedIndex = Math.Max(_grilla.SelectedIndex, 0);
            (_grilla.ItemContainerGenerator.ContainerFromIndex(_grilla.SelectedIndex) as ListBoxItem)
                ?.Focus();

            e.Handled = true;
        }
    }

    private void Aceptar()
    {
        if (_grilla.SelectedItem is not IconoDelCatalogo icono)
        {
            return;
        }

        IconoElegido = icono.Clave;
        DialogResult = true;
    }
}
