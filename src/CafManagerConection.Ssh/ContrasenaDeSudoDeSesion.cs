namespace CafManagerConection.Ssh;

/// <summary>La contraseña de <c>sudo</c> mientras esa sesión esté abierta: no se persiste, no viaja por la línea de comandos y el búfer se pisa con ceros al cerrar (FR-184e, excepción al Principio II de constitution.md:527).</summary>
public sealed class ContrasenaDeSudoDeSesion : IDisposable
{
    private char[] _bufer = [];

    private bool _utilizable;

    public bool Tiene => _utilizable && _bufer.Length > 0;

    /// <summary>Ya se le pidió al usuario en esta sesión: una contraseña equivocada repetida bloquea la cuenta.</summary>
    public bool YaSePidio { get; private set; }

    public int LargoDelBufer => _bufer.Length;

    public bool BuferEnCeros => Array.TrueForAll(_bufer, letra => letra == '\0');

    public void Guardar(ReadOnlySpan<char> contrasena)
    {
        PisarConCeros();

        _bufer = contrasena.ToArray();
        _utilizable = _bufer.Length > 0;
    }

    public void MarcarPedida() => YaSePidio = true;

    /// <summary>Presta el búfer para escribirlo en la entrada estándar de <c>sudo -S -k</c>: no deja una copia que después haya que pisar, como sí la dejaba <c>RevealSecret</c>.</summary>
    public ReadOnlyMemory<char> Prestada() =>
        Tiene ? _bufer.AsMemory() : ReadOnlyMemory<char>.Empty;

    /// <summary>La contraseña no sirvió: se pisa con ceros y no se vuelve a pedir en esta sesión.</summary>
    public void Descartar() => PisarConCeros();

    /// <summary>Cierre de la sesión: la contraseña queda como recién nacida, así que reabrir la misma conexión la vuelve a pedir (FR-184e, regla 5).</summary>
    public void Cerrar()
    {
        PisarConCeros();
        YaSePidio = false;
    }

    public void Dispose() => Cerrar();

    public override string ToString() => "ContrasenaDeSudoDeSesion(redactada)";

    private void PisarConCeros()
    {
        Array.Clear(_bufer);
        _utilizable = false;
    }
}
