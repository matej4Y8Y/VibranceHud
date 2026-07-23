using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Base for every page: paints its slice of the window's shared particle field as the
    /// background, so the whole app shares one continuous backdrop. The field instance and
    /// the page's window offset are injected by the window, which also owns the single
    /// animation timer.
    ///
    /// Derived pages that override OnPaint must call base.OnPaint(e) first, then draw their
    /// content on top; pages that only host child controls need do nothing.
    /// </summary>
    public class GlowPage : UserControl
    {
        public ParticleField? Field { get; set; }
        public Point FieldOffset { get; set; }

        protected GlowPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var back = new SolidBrush(Theme.Background))
                e.Graphics.FillRectangle(back, ClientRectangle);
            Field?.Paint(e.Graphics, FieldOffset.X, FieldOffset.Y);
            base.OnPaint(e);
        }
    }
}
