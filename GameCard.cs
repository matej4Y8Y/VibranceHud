using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VibranceHud.Games;

namespace VibranceHud
{
    /// <summary>
    /// A clickable card for one detected game: a logo tile (the game's initial on an accent
    /// tile - swap for real logos later), its name, an "Installed" marker, and a Configure
    /// hint. Clicking opens that game's settings.
    /// </summary>
    public sealed class GameCard : Control
    {
        private bool _hover;

        public DetectedGame Game { get; }

        public GameCard(DetectedGame game)
        {
            Game = game;
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(200, 160);
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, 16, 16);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rectF = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Glass.PaintPanel(g, rectF, 12, fillAlpha: _hover ? 170 : 145);
            if (_hover)
                using (var pen = new Pen(Theme.Accent, 1f))
                using (var path = Glass.RoundedPath(rectF, 12))
                    g.DrawPath(pen, path);

            // Logo tile: accent-tinted rounded square with the game's initial.
            var tile = new Rectangle(20, 20, 52, 52);
            using (var tilePath = Rounded(tile, 12))
            using (var tileFill = new SolidBrush(Theme.AccentDim))
                g.FillPath(tileFill, tilePath);
            using (var initFont = new Font(Theme.FontFamily, 20f, FontStyle.Bold))
                TextRenderer.DrawText(g, Game.Game.DisplayName.Substring(0, 1), initFont, tile, Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using (var nameFont = new Font(Theme.FontFamily, 12f, FontStyle.Bold))
                TextRenderer.DrawText(g, Game.Game.DisplayName, nameFont,
                    new Rectangle(20, 84, Width - 40, 24), Theme.Text, TextFormatFlags.Left);

            using (var dot = new SolidBrush(Color.FromArgb(80, 220, 130)))
                g.FillEllipse(dot, 20, 116, 8, 8);
            using (var small = new Font(Theme.FontFamily, 8f))
            {
                TextRenderer.DrawText(g, "Installed", small, new Rectangle(32, 111, 100, 16), Theme.TextDim,
                    TextFormatFlags.Left);
                TextRenderer.DrawText(g, "Configure ›", small, new Rectangle(Width - 92, 111, 72, 16), Theme.Accent,
                    TextFormatFlags.Right);
            }
        }

        private static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
