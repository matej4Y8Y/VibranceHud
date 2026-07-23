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

        public GlowPanel()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var back = new SolidBrush(Theme.Background))
                e.Graphics.FillRectangle(back, ClientRectangle);
            Field?.Paint(e.Graphics, Left, Top); // Left/Top are already window-relative here
            base.OnPaint(e);
        }
    }
}
