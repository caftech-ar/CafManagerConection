using System.Runtime.Versioning;
using System.Windows;
using CafManagerConection.Domain.Credentials;

namespace CafManagerConection.App.Views;

public enum ModoDeClaveMaestra
{
    /// <summary>Primera vez: se define, se repite y se advierte que perderla es irrecuperable.</summary>
    Crear,

    /// <summary>Ya existe: sólo se pide para abrir.</summary>
    Desbloquear,

    /// <summary>Se cambia por otra desde preferencias.</summary>
    Cambiar,
}

/// <summary>Pide la clave maestra. Devuelve <c>char[]</c> y no <c>string</c>: una cadena queda en el montón hasta que el recolector la levante y no se puede pisar con ceros.</summary>
[SupportedOSPlatform("windows")]
public partial class ClaveMaestraWindow : Window
{
    private readonly ModoDeClaveMaestra _modo;

    public ClaveMaestraWindow(ModoDeClaveMaestra modo)
    {
        _modo = modo;

        InitializeComponent();

        var creando = modo != ModoDeClaveMaestra.Desbloquear;

        _bloqueRepetir.Visibility = creando ? Visibility.Visible : Visibility.Collapsed;
        _avisoDePerdida.Visibility = creando ? Visibility.Visible : Visibility.Collapsed;
        _requisitos.Visibility = creando ? Visibility.Visible : Visibility.Collapsed;
        _fuerza.Visibility = creando ? Visibility.Visible : Visibility.Collapsed;

        _titulo.Text = modo switch
        {
            ModoDeClaveMaestra.Crear => "Poné una clave maestra",
            ModoDeClaveMaestra.Cambiar => "Cambiá la clave maestra",
            _ => "Desbloqueá tus contraseñas",
        };

        _explicacion.Text = modo switch
        {
            ModoDeClaveMaestra.Crear =>
                "Con ella se cifran las contraseñas de tus conexiones. Es opcional: sin clave "
                + "maestra se guardan en claro y no están protegidas.",
            ModoDeClaveMaestra.Cambiar =>
                "Se recifran todas las contraseñas guardadas, en una sola operación. También "
                + "podés quitarla, y entonces quedan guardadas en claro.",
            _ => "Las conexiones y las carpetas se ven igual sin ella; lo que queda sin funcionar "
                 + "es usar y guardar contraseñas.",
        };

        _botonCancelar.Content = modo switch
        {
            ModoDeClaveMaestra.Crear => "Sin clave maestra",
            ModoDeClaveMaestra.Cambiar => "Quitar la clave maestra",
            _ => "Después",
        };

        if (modo == ModoDeClaveMaestra.Cambiar)
        {
            _bloqueActual.Visibility = Visibility.Visible;
            _rotuloClave.Text = "Clave maestra nueva";
        }

        if (creando)
        {
            MostrarRequisitos();
        }

        Loaded += (_, _) => _clave.Focus();
    }

    /// <summary>Lo que el usuario tipeó. El llamador la pisa con ceros cuando termina de usarla.</summary>
    public char[]? Clave { get; private set; }

    /// <summary>El usuario eligió seguir sin clave maestra. Sólo posible al crear.</summary>
    public bool SinClaveMaestra { get; private set; }

    /// <summary>Sólo en modo Cambiar: la clave que el usuario tiene puesta hoy. Vacía en los otros modos.</summary>
    public char[]? ClaveActual { get; private set; }

    private void AlEscribir(object sender, RoutedEventArgs e)
    {
        _error.Visibility = Visibility.Collapsed;

        if (_modo != ModoDeClaveMaestra.Desbloquear)
        {
            MostrarRequisitos();
        }
    }

    private void MostrarRequisitos()
    {
        var tipeada = Tipeada();

        try
        {
            var falta = PoliticaDeClaveMaestra.Revisar(tipeada);

            _requisitos.Text = falta == FaltaEnLaClaveMaestra.Nada
                ? "Cumple los requisitos."
                : PoliticaDeClaveMaestra.Explicar(falta);

            _fuerza.Text = tipeada.Length == 0
                ? "Fuerza: —"
                : $"Fuerza: {PoliticaDeClaveMaestra.Nombrar(PoliticaDeClaveMaestra.Fuerza(tipeada))}";
        }
        finally
        {
            Array.Clear(tipeada);
        }
    }

    private char[] Tipeada() => Leer(_clave);

    private char[] Repetida() => Leer(_repetir);

    /// <summary>Saca el texto del <c>PasswordBox</c> a un <c>char[]</c> que se puede pisar, sin pasar por un <c>string</c>.</summary>
    private static char[] Leer(System.Windows.Controls.PasswordBox caja)
    {
        var seguro = caja.SecurePassword;
        var puntero = System.Runtime.InteropServices.Marshal
            .SecureStringToGlobalAllocUnicode(seguro);

        try
        {
            var letras = new char[seguro.Length];

            for (var i = 0; i < letras.Length; i++)
            {
                letras[i] = (char)System.Runtime.InteropServices.Marshal
                    .ReadInt16(puntero, i * 2);
            }

            return letras;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(puntero);
        }
    }

    private void AlAceptar(object sender, RoutedEventArgs e)
    {
        var tipeada = Tipeada();

        if (tipeada.Length == 0)
        {
            Fallar(_modo == ModoDeClaveMaestra.Crear
                ? "Escribí la clave maestra, o elegí «Sin clave maestra»."
                : "Escribí la clave maestra.");

            return;
        }

        if (_modo != ModoDeClaveMaestra.Desbloquear)
        {
            var falta = PoliticaDeClaveMaestra.Revisar(tipeada);

            if (falta != FaltaEnLaClaveMaestra.Nada)
            {
                Array.Clear(tipeada);
                Fallar(PoliticaDeClaveMaestra.Explicar(falta));
                return;
            }

            var repetida = Repetida();
            var coinciden = tipeada.AsSpan().SequenceEqual(repetida);
            Array.Clear(repetida);

            if (!coinciden)
            {
                Array.Clear(tipeada);
                Fallar("Las dos no coinciden.");
                return;
            }
        }

        if (_modo == ModoDeClaveMaestra.Cambiar)
        {
            ClaveActual = Leer(_actual);
        }

        Clave = tipeada;
        DialogResult = true;
    }

    private void Fallar(string motivo)
    {
        _error.Text = motivo;
        _error.Visibility = Visibility.Visible;
        _clave.Focus();
    }

    private void AlCancelar(object sender, RoutedEventArgs e)
    {
        SinClaveMaestra = _modo is ModoDeClaveMaestra.Crear or ModoDeClaveMaestra.Cambiar;

        if (_modo == ModoDeClaveMaestra.Cambiar)
        {
            ClaveActual = Leer(_actual);
        }

        DialogResult = false;
    }
}
