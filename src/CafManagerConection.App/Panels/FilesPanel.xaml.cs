using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CafManagerConection.App.Services;
using CafManagerConection.App.Views;
using CafManagerConection.Ssh;

namespace CafManagerConection.App.Panels;

/// <summary>Explorador de archivos del servidor por SFTP, sobre la misma conexión SSH (US6).</summary>
[SupportedOSPlatform("windows")]
public partial class FilesPanel : UserControl
{
    private readonly RemoteFileSession _sesion;
    private readonly Func<string> _nombreConexion;
    private readonly ObservableCollection<NodoRemoto> _raices = [];

    private NodoRemoto? _raiz;

    public FilesPanel(RemoteFileSession sesion, Func<string> nombreConexion)
    {
        _sesion = sesion;
        _nombreConexion = nombreConexion;

        InitializeComponent();

        _arbol.ItemsSource = _raices;
    }

    public async Task IniciarAsync()
    {
        var error = await _sesion.ConnectAsync().ConfigureAwait(true);

        if (error is not null)
        {
            _resumen.Text = error;
            return;
        }

        _raiz = Vigilado(new NodoRemoto(_sesion.HomeDirectory, _sesion.HomeDirectory, true));
        _raices.Clear();
        _raices.Add(_raiz);

        MostrarDestino();
        _raiz.Desplegar();
    }

    private NodoRemoto Vigilado(NodoRemoto nodo)
    {
        nodo.SolicitaCarga += AlSolicitarCarga;
        return nodo;
    }

    private async void AlSolicitarCarga(object? sender, EventArgs e)
    {
        if (sender is NodoRemoto carpeta)
        {
            await CargarAsync(carpeta).ConfigureAwait(true);
        }
    }

    private async Task CargarAsync(NodoRemoto carpeta)
    {
        var listado = await _sesion.ListAsync(carpeta.Ruta).ConfigureAwait(true);

        carpeta.Completar(listado.Entries.Select(entrada => Vigilado(new NodoRemoto(
            entrada.Name,
            entrada.FullPath,
            entrada.IsDirectory,
            entrada.IsDirectory ? string.Empty : Tamano(entrada.SizeBytes),
            entrada.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")))));

        var carpetas = listado.Entries.Count(entrada => entrada.IsDirectory);

        _resumen.Text = ResumenDeListado.Describir(
            carpeta.Ruta,
            carpetas,
            listado.Entries.Count - carpetas,
            listado.SymbolicLinksOmitted);
    }

    private NodoRemoto? Seleccionado =>
        _arbol.SelectedItem is NodoRemoto { EsMarcador: false } nodo ? nodo : null;

    private NodoRemoto? CarpetaDeDestino =>
        Seleccionado is { } nodo
            ? nodo.EsCarpeta ? nodo : nodo.Padre ?? _raiz
            : _raiz;

    private void AlCambiarSeleccion(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        MostrarDestino();

    private void MostrarDestino() =>
        _destino.Text = CarpetaDeDestino is { } carpeta
            ? $"Destino de la subida: {carpeta.Ruta}"
            : string.Empty;

    private void AlSubir(object sender, RoutedEventArgs e)
    {
        if (_botonSubir.ContextMenu is { } menu)
        {
            menu.PlacementTarget = _botonSubir;
            menu.IsOpen = true;
        }
    }

    private async void AlSubirArchivos(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Subir a {_nombreConexion()}",
            Multiselect = true,
        };

        if (dialogo.ShowDialog() == true)
        {
            await SubirAsync(dialogo.FileNames).ConfigureAwait(true);
        }
    }

    private async void AlSubirCarpeta(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog
        {
            Title = $"Subir un directorio a {_nombreConexion()}",
        };

        if (dialogo.ShowDialog() == true)
        {
            await SubirAsync([dialogo.FolderName]).ConfigureAwait(true);
        }
    }

    private void AlArrastrarEncima(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private async void AlSoltarArchivos(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] rutas)
        {
            await SubirAsync(rutas).ConfigureAwait(true);
        }
    }

    /// <summary>FR-189a: la carpeta remota se confirma antes de transferir el primer byte.</summary>
    private async Task SubirAsync(IReadOnlyList<string> locales)
    {
        if (CarpetaDeDestino is not { } destino)
        {
            _resumen.Text = "No hay una carpeta del servidor abierta.";
            return;
        }

        var archivos = locales.Where(File.Exists).ToList();
        var carpetas = locales.Where(Directory.Exists).ToList();

        if (archivos.Count + carpetas.Count == 0)
        {
            _resumen.Text = "No hay nada que subir.";
            return;
        }

        if (!Dialogos.Confirmar(
                Window.GetWindow(this)!,
                "Subir al servidor",
                ResumenDeListado.ConfirmacionDeSubida(
                    destino.Ruta, _nombreConexion(), archivos.Count + carpetas.Count),
                "Subir"))
        {
            return;
        }

        _progreso.Visibility = Visibility.Visible;

        var enviados = await SubirArchivosAsync(archivos, destino.Ruta).ConfigureAwait(true);
        var deCarpetas = 0;

        foreach (var carpeta in carpetas)
        {
            _resumen.Text = "Subiendo un directorio…";

            var resultado = await _sesion
                .UploadDirectoryAsync(carpeta, destino.Ruta, Avance())
                .ConfigureAwait(true);

            deCarpetas += resultado.Transferred;

            if (resultado.Error is not null)
            {
                _resumen.Text = resultado.Error;
            }
        }

        _progreso.Visibility = Visibility.Collapsed;

        destino.Recargar();

        _resumen.Text = $"{enviados + deCarpetas} archivo(s) subidos a {destino.Ruta}";
    }

    private async Task<int> SubirArchivosAsync(IReadOnlyList<string> archivos, string destino)
    {
        ConflictResolution? paraTodos = null;
        var enviados = 0;

        foreach (var local in archivos)
        {
            var nombre = Path.GetFileName(local);
            var remoto = RutaRemota.Combinar(destino, nombre);

            if (_sesion.Exists(remoto))
            {
                if (paraTodos is null)
                {
                    var (decision, aplicarATodos) = ConflictWindow.Preguntar(
                        Window.GetWindow(this)!, nombre);

                    if (aplicarATodos)
                    {
                        paraTodos = decision;
                    }

                    if (decision == ConflictResolution.Skip)
                    {
                        continue;
                    }

                    if (decision == ConflictResolution.KeepBoth)
                    {
                        remoto = RutaRemota.Combinar(destino, SinPisar(nombre));
                    }
                }
                else
                {
                    if (paraTodos == ConflictResolution.Skip)
                    {
                        continue;
                    }

                    if (paraTodos == ConflictResolution.KeepBoth)
                    {
                        remoto = RutaRemota.Combinar(destino, SinPisar(nombre));
                    }
                }
            }

            _resumen.Text = $"Subiendo {nombre}…";

            var resultado = await _sesion
                .UploadAsync(local, remoto, Avance()).ConfigureAwait(true);

            if (resultado.Success)
            {
                enviados++;
            }
            else
            {
                _resumen.Text = $"{nombre}: {resultado.Error}";
            }
        }

        return enviados;
    }

    private async void AlBajar(object sender, RoutedEventArgs e)
    {
        if (Seleccionado is not { } nodo)
        {
            _resumen.Text = "Elegí un archivo o una carpeta del árbol para bajar.";
            return;
        }

        if (nodo.EsCarpeta)
        {
            await BajarCarpetaAsync(nodo).ConfigureAwait(true);
            return;
        }

        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar como",
            FileName = nodo.Nombre,
        };

        if (dialogo.ShowDialog() != true)
        {
            return;
        }

        _progreso.Visibility = Visibility.Visible;
        _resumen.Text = $"Bajando {nodo.Nombre}…";

        var resultado = await _sesion
            .DownloadAsync(nodo.Ruta, dialogo.FileName, Avance()).ConfigureAwait(true);

        _progreso.Visibility = Visibility.Collapsed;

        _resumen.Text = resultado.Success
            ? $"{nodo.Nombre} guardado en {dialogo.FileName}"
            : $"{nodo.Nombre}: {resultado.Error}";
    }

    private async Task BajarCarpetaAsync(NodoRemoto carpeta)
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog
        {
            Title = $"Guardar «{carpeta.Nombre}» en",
        };

        if (dialogo.ShowDialog() != true)
        {
            return;
        }

        _progreso.Visibility = Visibility.Visible;
        _resumen.Text = $"Bajando {carpeta.Nombre}…";

        var resultado = await _sesion
            .DownloadDirectoryAsync(carpeta.Ruta, dialogo.FolderName, Avance())
            .ConfigureAwait(true);

        _progreso.Visibility = Visibility.Collapsed;

        _resumen.Text = resultado.Error is not null
            ? $"{carpeta.Nombre}: {resultado.Error}"
            : $"{resultado.Transferred} archivo(s) guardados en {dialogo.FolderName}";
    }

    private void AlActualizar(object sender, RoutedEventArgs e) => CarpetaDeDestino?.Recargar();

    private async void AlCrearCarpeta(object sender, RoutedEventArgs e)
    {
        if (CarpetaDeDestino is not { } destino)
        {
            return;
        }

        var nombre = TextPromptWindow.Pedir(
            Window.GetWindow(this)!, "Nueva carpeta", "Nombre de la carpeta");

        if (nombre is null)
        {
            return;
        }

        var error = await _sesion
            .CreateDirectoryAsync(RutaRemota.Combinar(destino.Ruta, nombre)).ConfigureAwait(true);

        if (error is not null)
        {
            _resumen.Text = error;
            return;
        }

        destino.Recargar();
    }

    private async void AlRenombrar(object sender, RoutedEventArgs e)
    {
        if (Seleccionado is not { } nodo || nodo.Padre is not { } padre)
        {
            return;
        }

        var nombre = TextPromptWindow.Pedir(
            Window.GetWindow(this)!, "Renombrar", "Nuevo nombre", nodo.Nombre);

        if (nombre is null || nombre == nodo.Nombre)
        {
            return;
        }

        var error = await _sesion
            .RenameAsync(nodo.Ruta, RutaRemota.Combinar(padre.Ruta, nombre)).ConfigureAwait(true);

        if (error is not null)
        {
            _resumen.Text = error;
            return;
        }

        padre.Recargar();
    }

    private async void AlEliminar(object sender, RoutedEventArgs e)
    {
        if (Seleccionado is not { } nodo || nodo.Padre is not { } padre)
        {
            return;
        }

        var detalle = nodo.EsCarpeta
            ? $"Se va a eliminar la carpeta «{nodo.Nombre}» del servidor, con su contenido."
            : $"Se va a eliminar «{nodo.Nombre}» del servidor.";

        if (!Dialogos.Confirmar(
                Window.GetWindow(this)!, "Eliminar del servidor", detalle, "Eliminar"))
        {
            return;
        }

        var error = await _sesion.DeleteAsync(nodo.Ruta, nodo.EsCarpeta).ConfigureAwait(true);

        if (error is not null)
        {
            _resumen.Text = error;
            return;
        }

        padre.Recargar();
    }

    private Progress<TransferProgress> Avance() =>
        new(avance => _progreso.Value = avance.Percent);

    /// <summary>Agrega un sufijo al nombre para no pisar lo que ya está.</summary>
    private static string SinPisar(string nombre)
    {
        var baseNombre = Path.GetFileNameWithoutExtension(nombre);
        var extension = Path.GetExtension(nombre);
        var sello = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");

        return $"{baseNombre} ({sello}){extension}";
    }

    private static string Tamano(long bytes)
    {
        string[] unidades = ["B", "KiB", "MiB", "GiB", "TiB"];
        double valor = bytes;
        var i = 0;

        while (valor >= 1024 && i < unidades.Length - 1)
        {
            valor /= 1024;
            i++;
        }

        return i == 0 ? $"{bytes} B" : $"{valor:0.#} {unidades[i]}";
    }
}
