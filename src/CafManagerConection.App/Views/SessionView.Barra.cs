using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forma = System.Windows.Shapes;

namespace CafManagerConection.App.Views;

/// <summary>Barra de acciones de una sesión SSH.</summary>
[SupportedOSPlatform("windows")]
public partial class SessionView
{
    /// <summary>Llena la barra y la muestra. Sólo para sesiones SSH.</summary>
    private void ArmarBarraDeAcciones()
    {
        if (_accionesSesion.Children.Count == 0)
        {
            Armar();
        }

        _barraSesion.Visibility = Visibility.Visible;
    }

    private void Armar()
    {
        Agregar("IconoCopiarTodo", "Copiar toda la sesión al portapapeles", CopiarTodo);
        Agregar("IconoGuardarComo", "Guardar toda la sesión en un archivo", GuardarEnArchivo);
        Agregar("IconoBorrarHistorial", "Borrar el historial de desplazamiento", BorrarHistorial);
        Agregar(
            "IconoRestablecer",
            "Restablecer el terminal, como el comando reset — no toca la conexión",
            RestablecerTerminal);

        Agregar("IconoReconectar", "Reconectar: cierra la conexión y la vuelve a abrir",
            Reconectar);

        Agregar("IconoElevar", "Elevar a root con sudo -i", ElevarShell);

        Agregar(
            "IconoPaleta",
            "Comandos guardados (Ctrl+Shift+P)",
            AbrirPaleta);

        AgregarHerramientasExternas();
    }

    /// <summary>Un botón por herramienta externa instalada (FR-143).</summary>
    private void AgregarHerramientasExternas()
    {
        foreach (var herramienta in _root.Herramientas.Instaladas)
        {
            var nombre = Services.LanzadorExterno.Nombre(herramienta);
            var cual = herramienta;

            var icono = herramienta == Infrastructure.HerramientaExterna.Putty
                ? "IconoTerminalExterna"
                : "IconoArchivosExterno";

            Agregar(icono, $"Abrir este servidor en {nombre}", () => AbrirEn(cual));
        }
    }

    private void AbrirEn(Infrastructure.HerramientaExterna herramienta)
    {
        if (_peticionSsh is not { } peticion
            || _root.Herramientas.Ruta(herramienta) is not { } ejecutable)
        {
            return;
        }

        var destino = new Infrastructure.DestinoRemoto(
            peticion.Host, peticion.Port, peticion.UserName, peticion.PrivateKeyPath);

        var error = Services.LanzadorExterno.Abrir(herramienta, ejecutable, destino);

        Informar(error ?? $"{Services.LanzadorExterno.Nombre(herramienta)} abierto");
    }

    /// <summary>Abre la paleta de comandos sobre esta sesión (FR-147).</summary>
    private void AbrirPaleta()
    {
        if (Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        var paleta = new PaletaDeComandosWindow(
            _root,
            _registro.Connection.Name,
            ConnectionId,
            MandarComando)
        {
            Owner = ventana,
        };

        paleta.ShowDialog();
    }

    private void MandarComando(string texto, bool ejecutar)
    {
        if (_ssh is null || State != Domain.Sessions.SessionState.Connected)
        {
            Informar("La sesión no está conectada");
            return;
        }

        var limpio = texto.Replace("\r\n", "\r").Replace('\n', '\r');
        var salida = ejecutar ? limpio + "\r" : limpio;

        _ssh.Send(Encoding.UTF8.GetBytes(salida));

        Informar(ejecutar ? "Comando enviado" : "Comando escrito en el prompt");
    }

    private void Agregar(string icono, string ayuda, Action accion)
    {
        var boton = new Button
        {
            Style = (Style)FindResource("AccionDeSesion"),
            Width = 30,
            Padding = new Thickness(0),
            ToolTip = ayuda,
            Content = new Forma.Path
            {
                Data = (Geometry)FindResource(icono),
                Width = 15,
                Height = 15,
                Stretch = Stretch.Uniform,

                Fill = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD8)),
            },
        };

        boton.Click += (_, _) => accion();
        _accionesSesion.Children.Add(boton);
    }

    private void CopiarTodo()
    {
        if (_terminal is not { } terminal)
        {
            return;
        }

        var texto = terminal.TextoCompleto;

        try
        {
            Clipboard.SetText(texto);
            Informar($"Copiado: {Lineas(texto)} línea(s) de la sesión");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Informar("No se pudo copiar: el portapapeles está ocupado");
        }
    }

    private void GuardarEnArchivo()
    {
        if (_terminal is not { } terminal)
        {
            return;
        }

        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar la sesión",
            Filter = "Texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
            DefaultExt = ".txt",
            AddExtension = true,
            FileName = $"{Sanear(_registro.Connection.Name)}-"
                       + $"{DateTimeOffset.Now:yyyyMMdd-HHmm}.txt",
        };

        if (dialogo.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        var texto = terminal.TextoCompleto;

        try
        {
            File.WriteAllText(dialogo.FileName, texto, new UTF8Encoding(false));

            Informar($"Guardado: {Lineas(texto)} línea(s) en {Path.GetFileName(dialogo.FileName)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _root.Logger.TechnicalError("guardar la sesión en un archivo", ex);
            Informar("No se pudo guardar el archivo. El motivo quedó en el registro");
        }
    }

    private void BorrarHistorial()
    {
        if (_terminal is not { } terminal)
        {
            return;
        }

        var tenia = terminal.LineasDeHistorial;
        terminal.BorrarHistorial();

        Informar(tenia == 0
            ? "No había historial que borrar"
            : $"Historial borrado: {tenia} línea(s)");
    }

    private void RestablecerTerminal()
    {
        _terminal?.Restablecer();
        Informar("Terminal restablecido");
    }

    private void Reconectar()
    {
        if (Window.GetWindow(this) is not { } ventana)
        {
            return;
        }

        var confirmado = Services.Dialogos.Confirmar(
            ventana,
            "¿Reconectar la sesión?",
            "Se cierra la conexión con el servidor y se abre de nuevo. Lo que esté corriendo en "
            + "esta sesión se corta, y el contenido del terminal se pierde.",
            "Reconectar");

        if (confirmado)
        {
            _ = ReconectarAsync();
        }
    }

    private void ElevarShell()
    {
        if (_ssh is null || State != Domain.Sessions.SessionState.Connected)
        {
            Informar("La sesión no está conectada");
            return;
        }

        _ssh.Send(Encoding.UTF8.GetBytes("sudo -i\r"));
        _root.Logger.PlatformActionPerformed(ConnectionId, "sudo -i");
        Informar("Enviado «sudo -i»");
    }

    private void Informar(string mensaje)
    {
        _terminal?.Focus();
        Dispatcher.BeginInvoke(() => Informo?.Invoke(this, mensaje));
    }

    private static int Lineas(string texto) =>
        texto.Length == 0 ? 0 : texto.Count(c => c == '\n');

    /// <summary>Deja el nombre usable como nombre de archivo.</summary>
    private static string Sanear(string nombre)
    {
        var limpio = new string(
            [.. nombre.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)]);

        return limpio.Trim().Length == 0 ? "sesion" : limpio.Trim();
    }
}
