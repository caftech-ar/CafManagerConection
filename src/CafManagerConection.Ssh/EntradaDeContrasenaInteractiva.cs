using System.Text;

namespace CafManagerConection.Ssh;

public enum ResultadoDeEntrada
{
    Continua,

    Confirmada,

    Cancelada,
}

/// <summary>Acumula lo que el usuario tipea para un pedido de contraseña interactivo, sin eco en pantalla.</summary>
public sealed class EntradaDeContrasenaInteractiva
{
    private readonly List<byte> _bytes = [];

    /// <summary>Procesa un tramo de bytes tecleados y dice si el pedido sigue, se confirmó o se canceló.</summary>
    public ResultadoDeEntrada Alimentar(ReadOnlySpan<byte> datos)
    {
        foreach (var b in datos)
        {
            switch (b)
            {
                case (byte)'\r':
                case (byte)'\n':
                    return ResultadoDeEntrada.Confirmada;

                case 0x1b:
                    return ResultadoDeEntrada.Cancelada;

                case 0x7f:
                case (byte)'\b':
                    if (_bytes.Count > 0)
                    {
                        _bytes.RemoveAt(_bytes.Count - 1);
                    }

                    break;

                default:
                    _bytes.Add(b);
                    break;
            }
        }

        return ResultadoDeEntrada.Continua;
    }

    /// <summary>Devuelve lo tipeado hasta ahora y vacía el búfer.</summary>
    public string TomarTexto()
    {
        var copia = _bytes.ToArray();
        var texto = Encoding.UTF8.GetString(copia);

        // Se pisan con ceros el búfer y la copia que deja ToArray: limpiar sólo la lista deja el secreto en el segundo arreglo.
        for (var i = 0; i < _bytes.Count; i++)
        {
            _bytes[i] = 0;
        }

        Array.Clear(copia);
        _bytes.Clear();

        return texto;
    }
}
