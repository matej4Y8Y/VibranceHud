using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VibranceHud.Crosshair;

namespace VibranceHud.Controls
{
    /// <summary>
    /// One cell in the crosshair gallery: a live preview of the crosshair, and a heart.
    ///
    /// The preview goes through the real <see cref="CrosshairRender"/>, not a hand-drawn
    /// approximation. A gallery that draws its own idea of each crosshair will eventually
    /// disagree with the overlay, and the user finds out only after picking one.
    ///
    /// Drawn in the user's own colour rather than the catalogue's white, so the grid shows
    /// what they would actually get.
    /// </summary>
    public sealed class CrosshairCell : Control
    {
        private const int HeartSize = 16;

        private bool _hover;
        private bool _hoverHeart;

        public CrosshairGallery.GalleryItem Item { get; }

        /// <summary>The colour and opacity to preview in - the user's current choice.</summary>
        public CrosshairConfig? PreviewStyle { get; set; }

        private bool _active;
        public bool Active
        {
            get => _active;
            set { if (_active == value) return; _active = value; Invalidate(); }
        }

        private bool _favourite;
        public bool Favourite
        {
            get => _favourite;
            set { if (_favourite == value) return; _favourite = value; Invalidate(); }
        }

        /// <summary>Raised when the heart is clicked, rather than the cell itself.</summary>
        public event EventHandler? FavouriteToggled;

        public CrosshairCell(CrosshairGallery.GalleryItem item)
        {
            Item = item;

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = item.Name;
        }

        private Rectangle HeartRect =>
            new(Width - HeartSize - 6, 6, HeartSize, HeartSize);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overHeart = HeartRect.Contains(e.Location);
            if (overHeart == _hoverHeart) return;
            _hoverHeart = overHeart;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _hoverHeart = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }

        /// <summary>
        /// The heart swallows the click.
        ///
        /// Without this, favouriting a crosshair would also apply it - so anybody tidying up
        /// their favourites would change their crosshair several times on the way through.
        /// </summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && HeartRect.Contains(e.Location))
            {
                FavouriteToggled?.Invoke(this, EventArgs.Empty);
                return;
            }

            base.OnMouseUp(e);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is not (Keys.Space or Keys.Enter)) return;
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }

        internal void TestPressKey(Keys key) => OnKeyDown(new KeyEventArgs(key));

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = 8f;

            if (_active)
                Glass.PaintAccent(g, rect, radius, Theme.Accent, alpha: 60);
            else
                Glass.PaintPanel(g, rect, radius, fillAlpha: _hover ? 175 : 140);

            using (var pen = new Pen(
                _active ? Color.FromArgb(220, Theme.Accent)
                        : Color.FromArgb(_hover ? 110 : 50, Theme.GlassEdge), 1f))
            using (var path = Glass.RoundedPath(rect, radius))
                g.DrawPath(pen, path);

            DrawPreview(g);
            DrawHeart(g);
            DrawName(g);

            if (Focused) UiHelpers.DrawFocusRing(g, ClientRectangle, radius);
        }

        /// <summary>
        /// Draw the crosshair itself, scaled to fit the cell.
        ///
        /// Scaled rather than clipped: several gallery entries are far larger than a cell,
        /// and a clipped preview shows four stubs that look nothing like the crosshair.
        /// </summary>
        private void DrawPreview(Graphics g)
        {
            var preview = Item.Config.Clone();

            // Show it in the user's colour, so the grid previews what they would get.
            if (PreviewStyle != null)
            {
                preview.ColourArgb = PreviewStyle.ColourArgb;
                preview.Opacity = PreviewStyle.Opacity;
                preview.Outline = PreviewStyle.Outline;
            }

            // The fit-to-target overload, which the saved-crosshair thumbnails already use.
            // Several gallery entries are far larger than a cell, and drawing them at their
            // real pixel size would show four clipped stubs that look nothing like the
            // crosshair they represent.
            int box = Math.Min(Width - 16, Height - 24);
            if (box <= 4) return;

            var target = new Rectangle((Width - box) / 2, (Height - 16 - box) / 2, box, box);
            CrosshairRender.Draw(g, preview, target);
        }

        private void DrawHeart(Graphics g)
        {
            // Only shown once there is a reason to look at it - a grid of thirty hearts is
            // noise, and the ones that matter are the filled ones.
            if (!_favourite && !_hover) return;

            var r = HeartRect;
            var colour = _favourite
                ? Color.FromArgb(235, 90, 120)
                : Color.FromArgb(_hoverHeart ? 200 : 90, Theme.GlassEdge);

            using var path = HeartPath(r);
            if (_favourite)
            {
                using var fill = new SolidBrush(colour);
                g.FillPath(fill, path);
            }
            else
            {
                using var pen = new Pen(colour, 1.4f);
                g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath HeartPath(Rectangle r)
        {
            var path = new GraphicsPath();
            float w = r.Width, h = r.Height;
            float cx = r.X + w / 2f;

            path.AddBezier(cx, r.Y + h * 0.28f,
                           cx, r.Y,
                           r.X, r.Y + h * 0.05f,
                           r.X, r.Y + h * 0.38f);
            path.AddBezier(r.X, r.Y + h * 0.38f,
                           r.X, r.Y + h * 0.68f,
                           cx, r.Y + h * 0.82f,
                           cx, r.Bottom);
            path.AddBezier(cx, r.Bottom,
                           cx, r.Y + h * 0.82f,
                           r.Right, r.Y + h * 0.68f,
                           r.Right, r.Y + h * 0.38f);
            path.AddBezier(r.Right, r.Y + h * 0.38f,
                           r.Right, r.Y + h * 0.05f,
                           cx, r.Y,
                           cx, r.Y + h * 0.28f);
            path.CloseFigure();
            return path;
        }

        private void DrawName(Graphics g) =>
            TextRenderer.DrawText(g, Item.Name, Design.Fonts.Caption,
                new Rectangle(2, Height - 17, Width - 4, 15),
                _active ? Theme.Text : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
    }
}
