using System.Runtime.Versioning;
using System.Windows.Forms;
using System.Drawing;

namespace CafManagerConection.Terminal;

/// <summary>Dibuja el menú contextual del terminal con sus mismos colores.</summary>
[SupportedOSPlatform("windows")]
internal sealed class MenuOscuro : ToolStripProfessionalRenderer
{
    public MenuOscuro(TerminalPalette paleta)
        : base(new Colores(paleta))
    {
        Paleta = paleta;
    }

    private TerminalPalette Paleta { get; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item?.Enabled == false
            ? Mezclar(Paleta.Foreground, Paleta.Background, 0.55)
            : Paleta.Foreground;

        base.OnRenderItemText(e);
    }

    private static Color Mezclar(Color a, Color b, double proporcion) => Color.FromArgb(
        (int)(a.R + ((b.R - a.R) * proporcion)),
        (int)(a.G + ((b.G - a.G) * proporcion)),
        (int)(a.B + ((b.B - a.B) * proporcion)));

    private sealed class Colores(TerminalPalette paleta) : ProfessionalColorTable
    {
        private Color Fondo => paleta.Background;

        private Color Realce => Mezclar(paleta.Background, paleta.Foreground, 0.22);

        private Color Borde => Mezclar(paleta.Background, paleta.Foreground, 0.35);

        public override Color ToolStripDropDownBackground => Fondo;

        public override Color ImageMarginGradientBegin => Fondo;

        public override Color ImageMarginGradientMiddle => Fondo;

        public override Color ImageMarginGradientEnd => Fondo;

        public override Color MenuBorder => Borde;

        public override Color MenuItemBorder => Realce;

        public override Color MenuItemSelected => Realce;

        public override Color MenuItemSelectedGradientBegin => Realce;

        public override Color MenuItemSelectedGradientEnd => Realce;

        public override Color MenuItemPressedGradientBegin => Fondo;

        public override Color MenuItemPressedGradientMiddle => Fondo;

        public override Color MenuItemPressedGradientEnd => Fondo;

        public override Color SeparatorDark => Borde;

        public override Color SeparatorLight => Borde;
    }
}
