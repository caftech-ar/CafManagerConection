using System.Drawing;
using System.Runtime.Versioning;

namespace CafManagerConection.Terminal;

/// <summary>Paleta del terminal: los 16 colores ANSI, la extensión de 256 y el color por omisión (FR-027).</summary>
[SupportedOSPlatform("windows")]
public sealed class TerminalPalette
{
    private readonly Color[] _ansi = new Color[16];
    private readonly Color[] _extended = new Color[256];

    private TerminalPalette(bool dark)
    {
        IsDark = dark;

        if (dark)
        {
            Foreground = Color.FromArgb(204, 204, 204);
            Background = Color.FromArgb(12, 12, 12);

            _ansi[0] = Color.FromArgb(12, 12, 12);
            _ansi[1] = Color.FromArgb(197, 15, 31);
            _ansi[2] = Color.FromArgb(19, 161, 14);
            _ansi[3] = Color.FromArgb(193, 156, 0);
            _ansi[4] = Color.FromArgb(0, 55, 218);
            _ansi[5] = Color.FromArgb(136, 23, 152);
            _ansi[6] = Color.FromArgb(58, 150, 221);
            _ansi[7] = Color.FromArgb(204, 204, 204);
            _ansi[8] = Color.FromArgb(118, 118, 118);
            _ansi[9] = Color.FromArgb(231, 72, 86);
            _ansi[10] = Color.FromArgb(22, 198, 12);
            _ansi[11] = Color.FromArgb(249, 241, 165);
            _ansi[12] = Color.FromArgb(59, 120, 255);
            _ansi[13] = Color.FromArgb(180, 0, 158);
            _ansi[14] = Color.FromArgb(97, 214, 214);
            _ansi[15] = Color.FromArgb(242, 242, 242);
        }
        else
        {
            Foreground = Color.FromArgb(24, 24, 27);
            Background = Color.FromArgb(255, 255, 255);

            _ansi[0] = Color.FromArgb(40, 40, 40);
            _ansi[1] = Color.FromArgb(170, 20, 30);
            _ansi[2] = Color.FromArgb(20, 120, 20);
            _ansi[3] = Color.FromArgb(150, 110, 0);
            _ansi[4] = Color.FromArgb(20, 60, 190);
            _ansi[5] = Color.FromArgb(140, 30, 150);
            _ansi[6] = Color.FromArgb(20, 120, 170);
            _ansi[7] = Color.FromArgb(90, 90, 90);
            _ansi[8] = Color.FromArgb(120, 120, 120);
            _ansi[9] = Color.FromArgb(200, 40, 50);
            _ansi[10] = Color.FromArgb(30, 150, 30);
            _ansi[11] = Color.FromArgb(180, 140, 20);
            _ansi[12] = Color.FromArgb(40, 90, 220);
            _ansi[13] = Color.FromArgb(170, 50, 180);
            _ansi[14] = Color.FromArgb(30, 150, 200);
            _ansi[15] = Color.FromArgb(20, 20, 20);
        }

        FondoCoincidencia = dark
            ? Color.FromArgb(122, 98, 0)
            : Color.FromArgb(255, 235, 130);

        FondoCoincidenciaActual = Color.FromArgb(255, 165, 0);
        TextoSobreCoincidencia = Color.FromArgb(20, 20, 20);

        BuildExtended();
    }

    public bool IsDark { get; }

    public Color Foreground { get; }

    public Color Background { get; }

    public Color FondoCoincidencia { get; }

    public Color FondoCoincidenciaActual { get; }

    public Color TextoSobreCoincidencia { get; }

    public static TerminalPalette Dark { get; } = new(true);

    public static TerminalPalette Light { get; } = new(false);

    public static TerminalPalette For(bool dark) => dark ? Dark : Light;

    /// <summary>Construye los 240 colores restantes de la paleta de 256: un cubo de 6x6x6 y una escala de 24 grises, como los define XTerm.</summary>
    private void BuildExtended()
    {
        for (var i = 0; i < 16; i++)
        {
            _extended[i] = _ansi[i];
        }

        var niveles = new[] { 0, 95, 135, 175, 215, 255 };

        for (var r = 0; r < 6; r++)
        {
            for (var g = 0; g < 6; g++)
            {
                for (var b = 0; b < 6; b++)
                {
                    _extended[16 + (r * 36) + (g * 6) + b] =
                        Color.FromArgb(niveles[r], niveles[g], niveles[b]);
                }
            }
        }

        for (var i = 0; i < 24; i++)
        {
            var v = 8 + (i * 10);
            _extended[232 + i] = Color.FromArgb(v, v, v);
        }
    }

    public Color Resolve(short index, bool isBackground, CellFlags flags)
    {
        if (index == TerminalCell.DefaultColor)
        {
            return isBackground ? Background : Foreground;
        }

        if (index >= 256)
        {
            // Color de 24 bits, empaquetado en 5-5-5 por encima del rango de paleta.
            var packed = index - 256;
            return Color.FromArgb(
                ((packed >> 10) & 0x1F) << 3,
                ((packed >> 5) & 0x1F) << 3,
                (packed & 0x1F) << 3);
        }

        var color = _extended[Math.Clamp((int)index, 0, 255)];

        if (!isBackground && flags.HasFlag(CellFlags.Bold) && index < 8)
        {
            color = _ansi[index + 8];
        }

        return color;
    }
}
