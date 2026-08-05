using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A scene-preset chip: a drawn glyph, the name, and a swatch showing what the preset
    /// actually does to colour.
    ///
    /// The swatch is not decoration. It is a grey ramp - shadow, midtone, highlight - pushed
    /// through the preset's real colour matrix, so a look that cools and lifts shadows shows
    /// a bar that cools and lifts shadows. Change the numbers in DisplayPresets and the chip
    /// follows on its own.
    ///
    /// Deliberately no photographs. Screenshot thumbnails are what every competitor in this
    /// space uses, they need cropping and licensing per game, and they show the game rather
    /// than the thing PlexusX sells - which is the treatment applied on top of it. A drawn
    /// chip also matches the rest of the app, where every icon is GDI so it renders the same
    /// on every machine.
    /// </summary>
    public sealed class PresetChip : Control
    {
        private const int Radius = 12;
        private const int Gutter = 14;
        private const int SwatchH = 10;

        /// <summary>
        /// How much of the biome photo comes through as the chip's backdrop.
        ///
        /// Note this is alpha, not perceived strength, and the two are not the same over a
        /// near-black card: half of a bright sky is still a bright mid-grey, so a literal
        /// 0.5 reads as a photograph with a panel on it rather than a hint of place. 0.3 is
        /// where it stops competing with the glyph, the name and the swatch, which are the
        /// things actually carrying the meaning.
        ///
        /// No scrim over the top - legibility is handled by <see cref="DrawTextWithHalo"/>,
        /// which costs the image nothing.
        /// </summary>
        private const float PhotoOpacity = 0.15f;

        /// <summary>Trimmed off each edge of the source art. The photos are screenshots and
        /// screenshots carry a hairline frame, which shows up as a bright line down the chip's
        /// sides.</summary>
        private const float SourceInset = 0.02f;

        private static readonly Font TitleFont = new(Theme.FontFamily, 10f, FontStyle.Bold);

        private string _caption = "";
        private bool _hover;
        private bool _active;

        public string Caption { get => _caption; set { _caption = value ?? ""; Invalidate(); } }
        public string Subtitle { get; set; } = "";

        /// <summary>Which glyph to draw: "balanced", "forest", "desert" or "snow".</summary>
        public string Kind { get; set; } = "balanced";

        /// <summary>The biome photo, used as a dimmed backdrop behind the whole chip rather
        /// than as a thumbnail panel beside the text.</summary>
        public Image? Photo { get; set; }

        /// <summary>The preset's colour matrix, used to tint the swatch. Null draws a plain
        /// neutral ramp, which is exactly right for a preset that changes nothing.</summary>
        public float[]? Matrix { get; set; }

        public bool Active
        {
            get => _active;
            set { if (_active == value) return; _active = value; Invalidate(); }
        }

        public new event EventHandler? Click;

        public PresetChip()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(150, 74);
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
                Click?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space) return;
            Click?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using var shape = Glass.RoundedPath(rect, Radius);

            int fillAlpha = _active ? 200 : _hover ? 170 : 140;
            using (var body = new SolidBrush(Color.FromArgb(fillAlpha, Theme.GlassFill)))
                g.FillPath(body, shape);

            DrawPhotoBackdrop(g, shape);

            if (_active)
                using (var tint = new SolidBrush(Color.FromArgb(34, Theme.Accent)))
                    g.FillPath(tint, shape);

            using (var rim = new Pen(
                _active ? Color.FromArgb(235, Theme.Accent)
                        : Color.FromArgb(_hover ? 130 : 70, Theme.GlassEdge),
                _active ? 1.8f : 1f))
                g.DrawPath(rim, shape);

            var ink = _active ? Theme.Accent : Theme.TextDim;
            DrawGlyph(g, Kind, new Rectangle(Gutter, 15, 18, 18), ink);

            var nameRect = new Rectangle(Gutter + 26, 12, Width - Gutter - 32, 24);
            DrawTextWithHalo(g, _caption, nameRect);

            DrawSwatch(g, new Rectangle(Gutter, Height - SwatchH - 16, Width - 2 * Gutter, SwatchH));

            if (Focused)
            {
                var f = new RectangleF(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4);
                using var focus = new Pen(Color.FromArgb(220, Theme.Accent), 1.4f);
                using var focusPath = Glass.RoundedPath(f, Radius + 2);
                g.DrawPath(focus, focusPath);
            }
        }

        /// <summary>
        /// The caption, offset one pixel in the opposite tone before the real thing.
        ///
        /// This is what replaced the scrim. Snow and Desert are near-white photographs and a
        /// white caption laid straight onto them disappears; a one-pixel contrasting outline
        /// holds the letterforms apart from whatever is behind them without dimming the photo
        /// by a single percent, which a scrim cannot claim.
        /// </summary>
        private static void DrawTextWithHalo(Graphics g, string text, Rectangle rect)
        {
            const TextFormatFlags flags =
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

            // On the dark themes the text is light, so the halo is dark, and the other way
            // round on Light - otherwise it would be invisible exactly where it is needed.
            var halo = Color.FromArgb(150, Theme.IsLight ? Color.White : Color.Black);
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                TextRenderer.DrawText(g, text, TitleFont,
                    new Rectangle(rect.X + dx, rect.Y + dy, rect.Width, rect.Height), halo, flags);

            TextRenderer.DrawText(g, text, TitleFont, rect, Theme.Text, flags);
        }

        /// <summary>
        /// The biome photo across the whole chip at half strength.
        ///
        /// Behind everything rather than beside it: a photo in its own panel next to the text
        /// is the layout every competitor uses, and it also costs half the chip's width. As a
        /// backdrop it gives the chip a sense of place while the glyph, name and swatch keep
        /// the full width to themselves.
        ///
        /// The scrim is not optional. Snow and Desert are near-white photographs and the
        /// caption sits directly on them; at 50% with no scrim the white text vanishes into
        /// the sky.
        /// </summary>
        private void DrawPhotoBackdrop(Graphics g, GraphicsPath shape)
        {
            if (Photo == null) return;

            var saved = g.Save();
            g.SetClip(shape);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Crop the screenshot frame, then cover the chip without distorting the picture.
            float ix = Photo.Width * SourceInset, iy = Photo.Height * SourceInset;
            var src = new RectangleF(ix, iy, Photo.Width - 2 * ix, Photo.Height - 2 * iy);
            float scale = Math.Max(Width / src.Width, Height / src.Height);
            float cropW = Width / scale, cropH = Height / scale;
            var crop = new RectangleF(
                src.X + (src.Width - cropW) / 2f,
                src.Y + (src.Height - cropH) / 2f,
                cropW, cropH);

            var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = PhotoOpacity };
            using (var attrs = new System.Drawing.Imaging.ImageAttributes())
            {
                attrs.SetColorMatrix(matrix);
                g.DrawImage(Photo, new Rectangle(0, 0, Width, Height),
                    crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, attrs);
            }

            g.Restore(saved);
        }

        /// <summary>
        /// A shadow-to-highlight grey ramp with the preset applied to it. Five stops rather
        /// than two, because a preset that lifts shadows and holds highlights does something
        /// in the middle that a straight two-colour gradient would skip straight past.
        /// </summary>
        private void DrawSwatch(Graphics g, Rectangle bar)
        {
            if (bar.Width < 8 || bar.Height < 2) return;

            const int stops = 5;
            var colours = new Color[stops];
            for (int i = 0; i < stops; i++)
            {
                // 40..215 rather than 0..255: real scenes live between, and the extremes
                // clip on almost any preset, which would flatten every swatch alike.
                int grey = 40 + i * (215 - 40) / (stops - 1);
                colours[i] = Transform(Color.FromArgb(grey, grey, grey));
            }

            using var path = Glass.RoundedPath(bar, bar.Height / 2f);
            using var brush = new LinearGradientBrush(
                new Rectangle(bar.X, bar.Y - 1, bar.Width, bar.Height + 2),
                colours[0], colours[stops - 1], LinearGradientMode.Horizontal);

            var blend = new ColorBlend(stops);
            blend.Colors = colours;
            blend.Positions = new float[stops];
            for (int i = 0; i < stops; i++) blend.Positions[i] = i / (float)(stops - 1);
            brush.InterpolationColors = blend;
            brush.WrapMode = WrapMode.TileFlipXY;   // no seam at either end

            g.FillPath(brush, path);
            using var edge = new Pen(Color.FromArgb(60, Theme.GlassEdge), 1f);
            g.DrawPath(edge, path);
        }

        /// <summary>Push one colour through the preset's 5x5 matrix, in the same row-vector
        /// convention the overlay uses: new = old * M, with the last row as a constant.</summary>
        private Color Transform(Color c)
        {
            var m = Matrix;
            if (m == null || m.Length < 25) return c;

            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float nr = r * m[0] + g * m[5] + b * m[10] + m[20];
            float ng = r * m[1] + g * m[6] + b * m[11] + m[21];
            float nb = r * m[2] + g * m[7] + b * m[12] + m[22];
            return Color.FromArgb(Byte(nr), Byte(ng), Byte(nb));
        }

        private static int Byte(float v) => Math.Clamp((int)Math.Round(v * 255f), 0, 255);

        /// <summary>Drawn, not a font glyph: private-use codepoints render as boxes wherever
        /// the font is missing, which is not acceptable in a shipped product.</summary>
        private static void DrawGlyph(Graphics g, string kind, Rectangle r, Color colour)
        {
            using var pen = new Pen(colour, 1.6f) { LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(colour);

            switch (kind)
            {
                case "forest":   // two stacked conifer tiers over a trunk
                    g.DrawLines(pen, new[]
                    {
                        new Point(r.X + r.Width / 2, r.Y),
                        new Point(r.X + 2, r.Y + r.Height / 2),
                        new Point(r.Right - 2, r.Y + r.Height / 2),
                    });
                    g.DrawLines(pen, new[]
                    {
                        new Point(r.X + r.Width / 2, r.Y + r.Height / 4),
                        new Point(r.X + 1, r.Y + r.Height - 4),
                        new Point(r.Right - 1, r.Y + r.Height - 4),
                    });
                    g.DrawLine(pen, r.X + r.Width / 2, r.Y + r.Height - 4, r.X + r.Width / 2, r.Bottom);
                    break;

                case "desert":
                    // Sun pushed into the top-right, NOT centred over the dunes. Centred, a
                    // disc with an arc beneath it is precisely the head-and-shoulders shape
                    // of an account avatar, and that is what it was being read as.
                    g.FillEllipse(brush, r.Right - 7, r.Y, 6, 6);
                    // Two overlapping dune crests, wide and low, meeting the edges.
                    g.DrawCurve(pen, new[]
                    {
                        new Point(r.X, r.Y + r.Height - 3),
                        new Point(r.X + r.Width / 3, r.Y + r.Height - 9),
                        new Point(r.X + r.Width * 2 / 3, r.Y + r.Height - 4),
                        new Point(r.Right, r.Y + r.Height - 8),
                    }, 0.6f);
                    g.DrawCurve(pen, new[]
                    {
                        new Point(r.X, r.Bottom - 1),
                        new Point(r.X + r.Width / 2, r.Bottom - 6),
                        new Point(r.Right, r.Bottom - 2),
                    }, 0.6f);
                    break;

                case "snow":     // six-spoke flake
                    float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, rad = r.Width / 2f;
                    for (int i = 0; i < 3; i++)
                    {
                        double a = Math.PI / 3 * i;
                        float dx = (float)Math.Cos(a) * rad, dy = (float)Math.Sin(a) * rad;
                        g.DrawLine(pen, cx - dx, cy - dy, cx + dx, cy + dy);
                    }
                    break;

                default:         // balanced: circle, half filled - the app's own contrast mark
                    g.DrawEllipse(pen, r);
                    g.FillPie(brush, r, -90, 180);
                    break;
            }
        }
    }
}
