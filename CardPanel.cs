using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A rounded matte "card" surface used to group content on the pages (like the panels
    /// in the reference launcher). Children should set BackColor = Theme.Surface to sit
    /// flush on it.
    /// </summary>
    public sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Glass.PaintPanel(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 12);
            base.OnPaint(e);
        }
    }
}
