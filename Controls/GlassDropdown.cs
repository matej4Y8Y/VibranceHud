using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A dropdown drawn in the app's glass language: a rounded translucent pill with a
    /// hairline rim and a hand-drawn chevron, opening a themed list.
    ///
    /// Exists because the stock <see cref="ComboBox"/> cannot be themed on Windows -
    /// even with <c>FlatStyle.Flat</c> the drop button and the popup list keep their
    /// Win32 chrome, which is exactly the light-grey rectangle that made the old
    /// Profile Editor look like a different application from the rest of PlexusX.
    ///
    /// The popup is a <see cref="ToolStripDropDown"/> rather than a borderless form:
    /// it already handles click-outside dismissal, Escape, and screen-edge flipping,
    /// none of which are worth reimplementing.
    /// </summary>
    public sealed class GlassDropdown : Control
    {
        private readonly List<string> _items = new();
        private int _selectedIndex = -1;
        private bool _hover;
        private bool _open;

        /// <summary>Raised when the selection changes, by the user or by code.</summary>
        public event EventHandler? SelectedIndexChanged;

        public GlassDropdown()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 34;
            Font = new Font(Theme.FontFamily, 9.5f);
        }

        /// <summary>Placeholder shown when nothing is selected (e.g. no games detected).</summary>
        public string Placeholder { get; set; } = "Select...";

        public IReadOnlyList<string> Items => _items;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                int clamped = _items.Count == 0 ? -1 : Math.Clamp(value, -1, _items.Count - 1);
                if (clamped == _selectedIndex) return;
                _selectedIndex = clamped;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string SelectedItem =>
            _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : "";

        /// <summary>Replace the whole item list. Selection resets to the first entry
        /// (or none when the list is empty) - the same behaviour the old ComboBox had.</summary>
        public void SetItems(IEnumerable<string> items)
        {
            _items.Clear();
            _items.AddRange(items);
            _selectedIndex = _items.Count > 0 ? 0 : -1;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Open();
        }

        private void Open()
        {
            if (_items.Count == 0) return;

            var menu = new ToolStripDropDown
            {
                AutoClose = true,
                DropShadowEnabled = false,
                Padding = new Padding(4),
                BackColor = Theme.Surface,
                Renderer = new GlassMenuRenderer(),
            };

            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(_items[i])
                {
                    Font = Font,
                    ForeColor = index == _selectedIndex ? Theme.Accent : Theme.Text,
                    // Width follows the closed control so the popup lines up under it.
                    AutoSize = false,
                    Size = new Size(Width - 8, 30),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(8, 0, 8, 0),
                };
                item.Click += (_, _) => SelectedIndex = index;
                menu.Items.Add(item);
            }

            menu.Closed += (_, _) => { _open = false; Invalidate(); };
            _open = true;
            Invalidate();
            menu.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            // Open and hover both lift the fill, so the control acknowledges the cursor
            // the same way the chips and nav buttons do.
            Glass.PaintPanel(g, rect, 10, fillAlpha: _open ? 195 : _hover ? 175 : 148);

            bool hasSelection = _selectedIndex >= 0;
            var text = hasSelection ? SelectedItem : Placeholder;
            var textColor = hasSelection ? Theme.Text : Theme.TextDim;

            TextRenderer.DrawText(g, text, Font,
                new Rectangle(14, 0, Width - 44, Height), textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            DrawChevron(g, new Point(Width - 20, Height / 2), _open);
        }

        /// <summary>
        /// A two-stroke chevron, drawn rather than taken from a glyph font. Segoe MDL2
        /// private-use codepoints render as boxes on machines where the font is missing
        /// or substituted, which is not acceptable on an app that ships to the public.
        /// </summary>
        private static void DrawChevron(Graphics g, Point centre, bool pointingUp)
        {
            const int w = 5, h = 3;
            using var pen = new Pen(Theme.TextDim, 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            int dy = pointingUp ? -h : h;
            g.DrawLines(pen, new[]
            {
                new Point(centre.X - w, centre.Y - dy / 2),
                new Point(centre.X,     centre.Y + dy - dy / 2),
                new Point(centre.X + w, centre.Y - dy / 2),
            });
        }

        /// <summary>Paints the popup list in the app's palette instead of the Office-style
        /// gradient the stock renderer uses.</summary>
        private sealed class GlassMenuRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using var back = new SolidBrush(Theme.Surface);
                e.Graphics.FillRectangle(back, e.AffectedBounds);
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
