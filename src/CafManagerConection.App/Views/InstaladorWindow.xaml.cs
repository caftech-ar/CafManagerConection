using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.App.Services;

namespace CafManagerConection.App.Views;

/// <summary>Elección entre los instaladores que publica una versión nueva.</summary>
[SupportedOSPlatform("windows")]
public partial class InstaladorWindow : Window
{
    private readonly List<(RadioButton Boton, InstaladorDisponible Instalador)> _elecciones = [];

    private InstaladorWindow(string version, IReadOnlyList<InstaladorDisponible> instaladores)
    {
        InitializeComponent();

        _encabezado.Text = $"La versión {version} publica más de un instalador. Elegí cuál bajar.";

        foreach (var instalador in instaladores)
        {
            Agregar(instalador);
        }

        var preferido = _elecciones.Find(e => e.Instalador.EsElInstalado);

        (preferido.Boton ?? _elecciones[0].Boton).IsChecked = true;
    }

    /// <summary>El instalador elegido, o <c>null</c> si se canceló.</summary>
    public InstaladorDisponible? Elegido { get; private set; }

    private void Agregar(InstaladorDisponible instalador)
    {
        var titulo = new List<string> { instalador.Nombre };

        if (instalador.Tamano is { } tamano)
        {
            titulo.Add(tamano);
        }

        titulo.Add(instalador.Condicion);

        if (instalador.EsElInstalado)
        {
            titulo.Add("es el que tenés instalado");
        }

        var boton = new RadioButton
        {
            Content = string.Join(" · ", titulo),
            Margin = new Thickness(0, 0, 0, 8),
        };

        _opciones.Children.Add(boton);
        _elecciones.Add((boton, instalador));
    }

    private void AlAceptar(object sender, RoutedEventArgs e)
    {
        Elegido = _elecciones.Find(x => x.Boton.IsChecked == true).Instalador;
        DialogResult = true;
    }

    private void AlCancelar(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>Pide elegir entre los instaladores; devuelve <c>null</c> si se cancela.</summary>
    /// <param name="owner">Ventana sobre la que se abre el diálogo.</param>
    /// <param name="version">Versión disponible, para el encabezado.</param>
    /// <param name="instaladores">Los instaladores entre los que se elige.</param>
    public static InstaladorDisponible? Pedir(
        Window owner, string version, IReadOnlyList<InstaladorDisponible> instaladores)
    {
        ArgumentNullException.ThrowIfNull(instaladores);

        var ventana = new InstaladorWindow(version, instaladores) { Owner = owner };

        return ventana.ShowDialog() == true ? ventana.Elegido : null;
    }
}
