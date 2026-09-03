using System.Text;
using System.Windows.Forms;

namespace CafManagerConection.Terminal;

/// <summary>Traduce las teclas de Windows a las secuencias que espera un servidor Unix (FR-032).</summary>
public static class KeyboardMapper
{
    /// <summary>Bytes a enviar, o <c>null</c> si la tecla no genera nada por sí sola.</summary>
    public static byte[]? Map(
        Keys keyCode, bool control, bool alt, bool shift, bool applicationCursor, bool applicationKeypad = false)
    {
        if (applicationKeypad && !control && !alt && TecladoNumericoDeAplicacion(keyCode) is { } numerico)
        {
            return Encoding.ASCII.GetBytes(numerico);
        }

        var cursorPrefix = applicationCursor ? "\x1bO" : "\x1b[";

        var texto = keyCode switch
        {
            Keys.Up => cursorPrefix + "A",
            Keys.Down => cursorPrefix + "B",
            Keys.Right => cursorPrefix + "C",
            Keys.Left => cursorPrefix + "D",
            Keys.Home => applicationCursor ? "\x1bOH" : "\x1b[H",
            Keys.End => applicationCursor ? "\x1bOF" : "\x1b[F",

            Keys.Insert => "\x1b[2~",
            Keys.Delete => "\x1b[3~",
            Keys.PageUp => "\x1b[5~",
            Keys.PageDown => "\x1b[6~",

            Keys.F1 => "\x1bOP",
            Keys.F2 => "\x1bOQ",
            Keys.F3 => "\x1bOR",
            Keys.F4 => "\x1bOS",
            Keys.F5 => "\x1b[15~",
            Keys.F6 => "\x1b[17~",
            Keys.F7 => "\x1b[18~",
            Keys.F8 => "\x1b[19~",
            Keys.F9 => "\x1b[20~",
            Keys.F10 => "\x1b[21~",
            Keys.F11 => "\x1b[23~",
            Keys.F12 => "\x1b[24~",

            Keys.Back => "\x7f",
            Keys.Tab when shift => "\x1b[Z",
            Keys.Tab => "\t",
            Keys.Enter => "\r",
            Keys.Escape => "\x1b",

            _ => null,
        };

        if (texto is not null)
        {
            return Encoding.ASCII.GetBytes(texto);
        }

        if (control && !alt && keyCode is >= Keys.A and <= Keys.Z)
        {
            return [(byte)(keyCode - Keys.A + 1)];
        }

        if (control && !alt)
        {
            var especial = keyCode switch
            {
                Keys.Space => (byte)0,
                Keys.OemOpenBrackets => (byte)27,
                Keys.OemBackslash or Keys.Oem5 => (byte)28,
                Keys.OemCloseBrackets or Keys.Oem6 => (byte)29,
                _ => (byte?)null,
            };

            if (especial is { } b)
            {
                return [b];
            }
        }

        return null;
    }

    // DECKPAM manda SS3: ESC O p..y son los dígitos 0..9, n el separador decimal, j k m o los operadores.
    private static string? TecladoNumericoDeAplicacion(Keys keyCode) => keyCode switch
    {
        Keys.NumPad0 => "\x1bOp",
        Keys.NumPad1 => "\x1bOq",
        Keys.NumPad2 => "\x1bOr",
        Keys.NumPad3 => "\x1bOs",
        Keys.NumPad4 => "\x1bOt",
        Keys.NumPad5 => "\x1bOu",
        Keys.NumPad6 => "\x1bOv",
        Keys.NumPad7 => "\x1bOw",
        Keys.NumPad8 => "\x1bOx",
        Keys.NumPad9 => "\x1bOy",
        Keys.Decimal => "\x1bOn",
        Keys.Multiply => "\x1bOj",
        Keys.Add => "\x1bOk",
        Keys.Subtract => "\x1bOm",
        Keys.Divide => "\x1bOo",
        _ => null,
    };

    /// <summary>Texto tecleado normalmente. Alt actúa como Meta: antepone ESC.</summary>
    public static byte[] MapText(char c, bool alt)
    {
        var bytes = Encoding.UTF8.GetBytes(c.ToString());

        if (!alt)
        {
            return bytes;
        }

        var conEscape = new byte[bytes.Length + 1];
        conEscape[0] = 0x1b;
        bytes.CopyTo(conEscape, 1);
        return conEscape;
    }
}
