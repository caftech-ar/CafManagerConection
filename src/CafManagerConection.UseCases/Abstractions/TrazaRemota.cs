namespace CafManagerConection.UseCases.Abstractions;

public enum TipoDeTraza
{
    Conexion,

    Comando,

    Escalada,

    Cierre,
}

public sealed record EntradaDeTraza(
    DateTimeOffset Momento,
    Guid Conexion,
    string Servidor,
    TipoDeTraza Tipo,
    string Enviado,
    int? Codigo,
    TimeSpan Duracion,
    string Salida,
    string Error)
{
    public bool Fallo => Codigo is not (null or 0);

    public int BytesEnviados => System.Text.Encoding.UTF8.GetByteCount(Enviado);

    public int BytesRecibidos =>
        System.Text.Encoding.UTF8.GetByteCount(Salida)
        + System.Text.Encoding.UTF8.GetByteCount(Error);
}

// Aparte de IAppLogger a propósito: el registro va a disco y esto no puede llegar ahí (Principio II).
public interface IRegistroDeTrazas
{
    bool Activo { get; }

    void Anotar(EntradaDeTraza entrada);
}

public sealed class RegistroDeTrazas : IRegistroDeTrazas
{
    public const int Capacidad = 500;

    public const int LargoMaximo = 4000;

    private readonly Queue<EntradaDeTraza> _entradas = new(Capacidad);
    private readonly object _traba = new();

    private long _enviados;
    private long _recibidos;
    private long _total;

    public event EventHandler<EntradaDeTraza>? Anotada;

    public bool Activo { get; set; } = true;

    public long BytesEnviados => Interlocked.Read(ref _enviados);

    public long BytesRecibidos => Interlocked.Read(ref _recibidos);

    public long Anotadas => Interlocked.Read(ref _total);

    public void Anotar(EntradaDeTraza entrada)
    {
        if (!Activo)
        {
            return;
        }

        var recortada = entrada with
        {
            Salida = Recortar(entrada.Salida),
            Error = Recortar(entrada.Error),
        };

        lock (_traba)
        {
            if (_entradas.Count == Capacidad)
            {
                _entradas.Dequeue();
            }

            _entradas.Enqueue(recortada);
        }

        Interlocked.Add(ref _enviados, entrada.BytesEnviados);
        Interlocked.Add(ref _recibidos, entrada.BytesRecibidos);
        Interlocked.Increment(ref _total);

        Anotada?.Invoke(this, recortada);
    }

    public IReadOnlyList<EntradaDeTraza> Entradas()
    {
        lock (_traba)
        {
            return [.. _entradas];
        }
    }

    public void Limpiar()
    {
        lock (_traba)
        {
            _entradas.Clear();
        }
    }

    private static string Recortar(string texto)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length <= LargoMaximo)
        {
            return texto ?? string.Empty;
        }

        var sobran = texto.Length - LargoMaximo;

        return string.Concat(
            texto.AsSpan(0, LargoMaximo),
            $"\n… ({sobran} caracteres más, recortados)");
    }
}
