using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A double-buffered panel (for the title bar and left nav) that paints its slice of
    /// the window's shared particle field, so the animated backdrop runs edge-to-edge
    /// behind the whole app - not just the content area.
    /// </summary>
    public sealed class GlowPanel : Panel
    {
        public ParticleField? Field { get; set; }

        /// <summary>0-255. Paints a translucent sheet of the theme's glass colour over the
        /// backdrop so this panel reads as its own frosted surface rather than a hole cut
        /// straight through to the wallpaper. Used by the left nav and title bar.</summary>
        public int Scrim { get; set; }

        public GlowPanel()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   // SupportsTransparentBackColor lets BackColor=Color.Transparent actually
                   // be transparent. Without this bit WinForms paints white wherever a
                   // transparent BackColor is requested - which is why the previous
                   // "transparent panel" attempt left a stark white slab behind the nav.
                   | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // No Theme.Background fill here. The animated backdrop (AppBackground) and
            // particle field paint the panel's content; only the explicitly-configured
            // Scrim (set by callers like the title bar) tints them. Scrim=0 means "let
            // the field show through untouched" - that's how the left nav goes transparent.
            Theming.AppBackground.Paint(e.Graphics, Left, Top);
            Field?.Paint(e.Graphics, Left, Top); // Left/Top are already window-relative here

            if (Scrim > 0 && Theming.AppBackground.IsSet)
                using (var veil = new SolidBrush(Color.FromArgb(Scrim, Theme.GlassFill)))
                    e.Graphics.FillRectangle(veil, ClientRectangle);
            base.OnPaint(e);
        }
    }
}
