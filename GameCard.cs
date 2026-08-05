using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VibranceHud.Games;

namespace VibranceHud
{
    /// <summary>
    /// A card for one game in the catalog: a logo tile (the game's initial on an accent
    /// tile - swap for real logos later), its name, and an install-state badge. Installed
    /// games get a "Configure ›" hint and are clickable to open that game's settings;
    /// not-installed games are shown dimmed and inert.
    /// </summary>
    public sealed class GameCard : Control
    {
        private bool _hover;

        public SupportedGame SupportedGame { get; }
        public DetectedGame? Detected { get; }
        public bool IsInstalled => Detected != null;

        /// <summary>Raised when the user clicks the card's "Edit profile" affordance.
        /// Fires only for installed games (no profile to set if the game isn't installed).</summary>
        public event EventHandler? OnEditProfileRequested;

        public GameCard(SupportedGame game, DetectedGame? detected)
        {
            SupportedGame = game;
            Detected = detected;
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(200, 160);
            Cursor = IsInstalled ? Cursors.Hand : Cursors.Default;
            Margin = new Padding(0, 0, 16, 16);

            // "Edit profile ›" affordance in the bottom-right corner. Clickable only when
            // the game is installed (no point saving a profile for a game that's not).
            // We use a child Label rather than painting text inline so the click target
            // gets the standard WinForms hit-test + the standard Cursor.Hand handling
            // without us reinventing mouse routing inside the custom-paint card.
            var editProfileLink = new Label
            {
                Text = "Edit profile ›",
                Font = new Font(Theme.FontFamily, 8f),
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Visible = IsInstalled,
            };
            editProfileLink.Click += (_, _) => OnEditProfileRequested?.Invoke(this, EventArgs.Empty);
            editProfileLink.Resize += (_, _) => editProfileLink.Location = new Point(
                Width - editProfileLink.Width - 20, Height - 22);
            Controls.Add(editProfileLink);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (IsInstalled) { _hover = true; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (IsInstalled) { _hover = false; Invalidate(); }
        }

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

            // Logo tile: accent-tinted rounded square with the game's initial (dimmed when
            // not installed).
            var tile = new Rectangle(20, 20, 52, 52);
            using (var tilePath = Rounded(tile, 12))
            using (var tileFill = new SolidBrush(IsInstalled ? Theme.AccentDim : Theme.SurfaceHover))
                g.FillPath(tileFill, tilePath);
            // Cached roles rather than three fonts built on every repaint.
            TextRenderer.DrawText(g, SupportedGame.DisplayName.Substring(0, 1), Design.Fonts.Display, tile,
                IsInstalled ? Theme.Text : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, SupportedGame.DisplayName, Design.Fonts.Heading,
                new Rectangle(20, 84, Width - 40, 24), Theme.Text, TextFormatFlags.Left);

            using (var dot = new SolidBrush(IsInstalled ? Color.FromArgb(80, 220, 130) : Theme.TextDim))
                g.FillEllipse(dot, 20, 116, 8, 8);

            var small = Design.Fonts.Caption;
            TextRenderer.DrawText(g, IsInstalled ? "Installed" : "Not installed", small,
                new Rectangle(32, 111, 100, 16), Theme.TextDim, TextFormatFlags.Left);
            if (IsInstalled)
                TextRenderer.DrawText(g, "Configure ›", small, new Rectangle(Width - 92, 111, 72, 16),
                    Theme.Accent, TextFormatFlags.Right);
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
