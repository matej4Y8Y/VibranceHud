using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VibranceHud.Games;

namespace VibranceHud
{
    /// <summary>
    /// The game the app is pointed at, sitting in the left nav above the version.
    ///
    /// Reads as a status line more than a control - a small caption, the game's name, and a
    /// chevron - because most of the time it is telling you where you are rather than asking
    /// you to change it.
    ///
    /// Only installed games are offered. A picker that lists things you cannot choose is a
    /// dead end; the full catalogue lives on the Game tab, which is where you go when nothing
    /// is selected anyway.
    /// </summary>
    public sealed class GameChooser : Control
    {
        private const int Radius = 10;

        private static readonly Font CaptionFont = new(Theme.FontFamily, 7f, FontStyle.Bold);
        private static readonly Font NameFont = new(Theme.FontFamily, 10f, FontStyle.Bold);

        private readonly GameSelection _selection;
        private bool _hover;
        private bool _open;

        public GameChooser(GameSelection selection)
        {
            _selection = selection;

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 46;
            TabStop = true;

            _selection.Changed += (_, _) => Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Open();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space && e.KeyCode != Keys.Down) return;
            Open();
            e.Handled = true;
        }

        private void Open()
        {
            // Nothing installed: there is genuinely no choice to offer, so don't open an
            // empty menu on them. The Game tab explains what to check.
            if (_selection.NothingInstalled) return;

            var menu = new ToolStripDropDown
            {
                AutoClose = true,
                DropShadowEnabled = false,
                Padding = new Padding(6),
                BackColor = Theme.Surface,
                Renderer = new ChooserRenderer(),
            };

            foreach (var detected in _selection.Installed)
            {
                var game = detected.Game;
                menu.Items.Add(Item(game.DisplayName, game.Id, _selection.CurrentId == game.Id));
            }

            // Desktop is always last and always available - it is the one state that can
            // never be invalid, and it is how you get out of a game you no longer want the
            // app pointed at.
            menu.Items.Add(new ToolStripSeparator { Margin = new Padding(0, 4, 0, 4) });
            menu.Items.Add(Item("Desktop", null, _selection.CurrentId == null));

            menu.Closed += (_, _) => { _open = false; Invalidate(); };

            // Round the popup itself. The renderer draws a rounded rim, but the window under
            // it is square, so without this the corners sit outside the border it just drew -
            // the same square-corner problem the splash screen had.
            menu.Opened += (_, _) =>
            {
                using var path = Glass.RoundedPath(
                    new RectangleF(0, 0, menu.Width, menu.Height), 10);
                menu.Region = new Region(path);
            };

            _open = true;
            Invalidate();
            // Opens upward: this control lives at the bottom of the nav, so a downward menu
            // would run off the window.
            menu.Show(this, new Point(0, -menu.GetPreferredSize(Size.Empty).Height - 6));
        }

        private ToolStripMenuItem Item(string text, string? id, bool active)
        {
            // A leading dot marks the current game rather than colour alone: on the light
            // theme the accent is near-black and reads as ordinary text, so colour on its own
            // was not actually saying anything.
            var item = new ToolStripMenuItem((active ? "•   " : "     ") + text)
            {
                Font = new Font(Theme.FontFamily, 9.5f, active ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = active ? Theme.Text : Theme.TextDim,
                AutoSize = false,
                Size = new Size(Math.Max(Width, 190) - 12, 32),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 10, 0),
            };
            item.Click += (_, _) => _selection.Select(id);
            return item;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using var shape = Glass.RoundedPath(rect, Radius);

            using (var fill = new SolidBrush(Color.FromArgb(
                _open ? 190 : _hover ? 165 : 130, Theme.GlassFill)))
                g.FillPath(fill, shape);
            using (var rim = new Pen(Color.FromArgb(_hover || _open ? 130 : 70, Theme.GlassEdge), 1f))
                g.DrawPath(rim, shape);

            TextRenderer.DrawText(g, UiHelpers.Spaced("GAME"), CaptionFont,
                new Rectangle(12, 6, Width - 24, 12), Theme.TextDim, TextFormatFlags.Left);

            var (name, colour) = _selection.NothingInstalled
                ? ("No games found", Theme.TextDim)
                : _selection.Current is { } game
                    ? (game.DisplayName, Theme.Text)
                    : ("Desktop", Theme.TextDim);

            TextRenderer.DrawText(g, name, NameFont,
                new Rectangle(12, 19, Width - 34, 20), colour,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            if (!_selection.NothingInstalled)
                DrawChevron(g, new Point(Width - 16, Height / 2 + 3), _open);

            if (Focused)
            {
                var f = new RectangleF(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4);
                using var focus = new Pen(Color.FromArgb(200, Theme.Accent), 1.4f);
                using var focusPath = Glass.RoundedPath(f, Radius + 2);
                g.DrawPath(focus, focusPath);
            }
        }

        /// <summary>Drawn rather than a glyph font - Segoe MDL2 codepoints render as boxes
        /// wherever that font is missing or substituted.</summary>
        private static void DrawChevron(Graphics g, Point centre, bool pointingDown)
        {
            const int w = 5, h = 3;
            using var pen = new Pen(Theme.TextDim, 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            int dy = pointingDown ? h : -h;
            g.DrawLines(pen, new[]
            {
                new Point(centre.X - w, centre.Y - dy / 2),
                new Point(centre.X,     centre.Y + dy - dy / 2),
                new Point(centre.X + w, centre.Y - dy / 2),
            });
        }

        /// <summary>Paints the popup in the app's palette instead of the Office-style
        /// gradient the stock renderer uses.</summary>
        private sealed class ChooserRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using var back = new SolidBrush(Theme.Surface);
                e.Graphics.FillRectangle(back, e.AffectedBounds);
            }

            /// <summary>A hairline rule rather than the stock 3D-etched separator, which
            /// looks like it came from a different decade of Windows.</summary>
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                var b = e.Item.Bounds;
                using var pen = new Pen(Color.FromArgb(90, Theme.GlassEdge), 1f);
                int y = b.Top + b.Height / 2;
                e.Graphics.DrawLine(pen, b.Left + 10, y, b.Right - 10, y);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new RectangleF(0.5f, 0.5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                using var path = Glass.RoundedPath(r, 10);
                using var pen = new Pen(Theme.Border, 1f);
                e.Graphics.DrawPath(pen, path);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new RectangleF(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                using var path = Glass.RoundedPath(r, 7);
                using var fill = new SolidBrush(Theme.SurfaceHover);
                e.Graphics.FillPath(fill, path);
            }
        }
    }
}
