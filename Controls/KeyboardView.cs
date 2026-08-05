using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>A key on the drawn keyboard: what the game calls it, and what to print on it.</summary>
    public sealed record KeyCap(string Id, string Label, float Width = 1f);

    /// <summary>
    /// A drawn keyboard you drop commands onto.
    ///
    /// Owner-drawn rather than a grid of Buttons: sixty-odd controls each repainting their own
    /// transparent background over the glass card is exactly the repaint storm that made the
    /// sliders feel heavy, and a real keyboard needs rows of different widths that no layout
    /// panel gives you for free.
    ///
    /// Key ids are the game's own names ("f1", "mouse4"), not WinForms Keys values, because
    /// they are written straight into the game's config.
    /// </summary>
    public sealed class KeyboardView : Control
    {
        private const int Gap = 4;
        private const int Radius = 5;

        private static readonly Font CapFont = new(Theme.FontFamily, 7.5f, FontStyle.Bold);
        private static readonly Font BoundFont = new(Theme.FontFamily, 6.5f);

        /// <summary>
        /// A compact 60%-style layout plus the function row and the mouse buttons.
        ///
        /// Deliberately not a full 104-key board: the numpad and navigation cluster are almost
        /// never bound in these games, and leaving them out makes every remaining key big
        /// enough to read and hit.
        /// </summary>
        private static readonly KeyCap[][] Rows =
        {
            new[]
            {
                new KeyCap("escape", "Esc"), new KeyCap("f1", "F1"), new KeyCap("f2", "F2"),
                new KeyCap("f3", "F3"), new KeyCap("f4", "F4"), new KeyCap("f5", "F5"),
                new KeyCap("f6", "F6"), new KeyCap("f7", "F7"), new KeyCap("f8", "F8"),
                new KeyCap("f9", "F9"), new KeyCap("f10", "F10"), new KeyCap("f11", "F11"),
                new KeyCap("f12", "F12"),
            },
            new[]
            {
                new KeyCap("1", "1"), new KeyCap("2", "2"), new KeyCap("3", "3"),
                new KeyCap("4", "4"), new KeyCap("5", "5"), new KeyCap("6", "6"),
                new KeyCap("7", "7"), new KeyCap("8", "8"), new KeyCap("9", "9"),
                new KeyCap("0", "0"), new KeyCap("minus", "-"), new KeyCap("equals", "="),
                new KeyCap("backspace", "Bksp", 1.8f),
            },
            new[]
            {
                new KeyCap("tab", "Tab", 1.5f), new KeyCap("q", "Q"), new KeyCap("w", "W"),
                new KeyCap("e", "E"), new KeyCap("r", "R"), new KeyCap("t", "T"),
                new KeyCap("y", "Y"), new KeyCap("u", "U"), new KeyCap("i", "I"),
                new KeyCap("o", "O"), new KeyCap("p", "P"), new KeyCap("leftbracket", "["),
                new KeyCap("rightbracket", "]"),
            },
            new[]
            {
                new KeyCap("capslock", "Caps", 1.8f), new KeyCap("a", "A"), new KeyCap("s", "S"),
                new KeyCap("d", "D"), new KeyCap("f", "F"), new KeyCap("g", "G"),
                new KeyCap("h", "H"), new KeyCap("j", "J"), new KeyCap("k", "K"),
                new KeyCap("l", "L"), new KeyCap("semicolon", ";"), new KeyCap("return", "Enter", 1.9f),
            },
            new[]
            {
                new KeyCap("leftshift", "Shift", 2.3f), new KeyCap("z", "Z"), new KeyCap("x", "X"),
                new KeyCap("c", "C"), new KeyCap("v", "V"), new KeyCap("b", "B"),
                new KeyCap("n", "N"), new KeyCap("m", "M"), new KeyCap("comma", ","),
                new KeyCap("period", "."), new KeyCap("slash", "/"), new KeyCap("rightshift", "Shift", 1.7f),
            },
            new[]
            {
                new KeyCap("leftcontrol", "Ctrl", 1.5f), new KeyCap("leftalt", "Alt", 1.3f),
                new KeyCap("space", "Space", 6f), new KeyCap("rightalt", "Alt", 1.3f),
                new KeyCap("rightcontrol", "Ctrl", 1.5f),
            },
            new[]
            {
                new KeyCap("mouse1", "M1", 1.4f), new KeyCap("mouse2", "M2", 1.4f),
                new KeyCap("mouse3", "M3", 1.4f), new KeyCap("mouse4", "M4", 1.4f),
                new KeyCap("mouse5", "M5", 1.4f),
                new KeyCap("mousewheelup", "Wheel ↑", 2f), new KeyCap("mousewheeldown", "Wheel ↓", 2f),
            },
        };

        /// <summary>
        /// Every key id the board actually draws.
        ///
        /// Exposed so other code can be checked against it. A key named in
        /// <see cref="Keybinds.GameDefaultBinds"/> that this board does not draw produces a
        /// warning nobody can see - the board can only tint a key it renders.
        /// </summary>
        public static IReadOnlyCollection<string> AllKeyIds() =>
            Rows.SelectMany(r => r.Select(k => k.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Rectangle> _hitboxes = new();
        private string? _hoverKey;
        private string? _dropTargetKey;

        /// <summary>keyId → the short label shown on a bound key.</summary>
        public IReadOnlyDictionary<string, string> Bound { get; set; } =
            new Dictionary<string, string>();

        /// <summary>
        /// keyId → what the GAME already uses that key for by default.
        ///
        /// Drawn differently from <see cref="Bound"/> and always loses to it: a key the user
        /// has deliberately taken over should read as theirs, not as a warning. Without this
        /// the board shows sixty free-looking keys, most of which are not free, and the first
        /// thing somebody learns about the feature is that it broke their movement.
        /// </summary>
        public IReadOnlyDictionary<string, string> GameDefaults { get; set; } =
            new Dictionary<string, string>();

        /// <summary>Raised when a command is dropped on a key, or a key is clicked.</summary>
        public event EventHandler<KeyboardKeyEventArgs>? KeyActivated;

        /// <summary>Raised when a command is dropped onto a key.</summary>
        public event EventHandler<KeyboardDropEventArgs>? CommandDropped;

        public KeyboardView()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            AllowDrop = true;
            Cursor = Cursors.Hand;
        }

        /// <summary>Tallest the board wants to be for a given width, so the page can size it.</summary>
        public int PreferredHeightFor(int width)
        {
            int unit = UnitFor(width);
            return Rows.Length * (unit + Gap) + Gap;
        }

        private static int UnitFor(int width)
        {
            // The widest row decides the key size, so every row fits.
            float widest = Rows.Max(r => r.Sum(k => k.Width) + r.Length * 0.001f);
            int perUnit = (int)((width - Gap) / widest) - Gap;
            return Math.Max(18, perUnit);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var key = KeyAt(e.Location);
            if (key == _hoverKey) return;
            _hoverKey = key;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverKey = null;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var key = KeyAt(e.Location);
            if (key != null)
                KeyActivated?.Invoke(this, new KeyboardKeyEventArgs(key, e.Button));
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            var local = PointToClient(new Point(e.X, e.Y));
            var key = KeyAt(local);

            e.Effect = key != null && e.Data?.GetDataPresent(typeof(string)) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;

            if (key == _dropTargetKey) return;
            _dropTargetKey = key;
            Invalidate();
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            _dropTargetKey = null;
            Invalidate();
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            var local = PointToClient(new Point(e.X, e.Y));
            var key = KeyAt(local);
            _dropTargetKey = null;

            if (key != null && e.Data?.GetData(typeof(string)) is string commandId)
                CommandDropped?.Invoke(this, new KeyboardDropEventArgs(key, commandId));

            Invalidate();
        }

        private string? KeyAt(Point p) =>
            _hitboxes.FirstOrDefault(kv => kv.Value.Contains(p)).Key;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            _hitboxes.Clear();
            int unit = UnitFor(Width);
            int y = Gap;

            foreach (var row in Rows)
            {
                int x = Gap;
                foreach (var cap in row)
                {
                    int w = (int)(unit * cap.Width) + (int)((cap.Width - 1) * Gap);
                    var rect = new Rectangle(x, y, w, unit);
                    _hitboxes[cap.Id] = rect;
                    DrawKey(g, rect, cap);
                    x += w + Gap;
                }
                y += unit + Gap;
            }
        }

        private void DrawKey(Graphics g, Rectangle rect, KeyCap cap)
        {
            bool bound = Bound.ContainsKey(cap.Id);
            bool hover = _hoverKey == cap.Id;
            bool target = _dropTargetKey == cap.Id;

            // A key the user has taken over is theirs; the game's default for it stops
            // mattering the moment they override it, so Bound always wins.
            bool gameUses = !bound && GameDefaults.ContainsKey(cap.Id);

            using var path = Glass.RoundedPath(
                new RectangleF(rect.X + 0.5f, rect.Y + 0.5f, rect.Width - 1, rect.Height - 1), Radius);

            Color fill = target
                ? Color.FromArgb(90, Theme.Accent)
                : bound
                    ? Color.FromArgb(52, Theme.Accent)
                    // Warm amber, not the accent: this is "careful", not "yours". Using the
                    // accent for both would make the board look mostly-bound on first open.
                    : gameUses
                        ? Color.FromArgb(hover ? 46 : 26, 240, 180, 90)
                        : Color.FromArgb(hover ? 60 : 30, Theme.GlassEdge);
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);

            using (var pen = new Pen(
                target || bound ? Color.FromArgb(220, Theme.Accent)
                    : gameUses ? Color.FromArgb(hover ? 150 : 90, 240, 180, 90)
                    : Color.FromArgb(hover ? 120 : 55, Theme.GlassEdge),
                target ? 1.8f : 1f))
                g.DrawPath(pen, path);

            // On a bound key the command wins the space - which key it is, you already know
            // from where it sits on the board.
            if (bound && rect.Height >= 26)
            {
                TextRenderer.DrawText(g, cap.Label, BoundFont,
                    new Rectangle(rect.X, rect.Y + 2, rect.Width, 10), Theme.TextDim,
                    TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(g, Bound[cap.Id], CapFont,
                    new Rectangle(rect.X + 1, rect.Y + 10, rect.Width - 2, rect.Height - 12),
                    Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis);
            }
            else if (gameUses && rect.Height >= 26)
            {
                // Key name on top, what the game does with it underneath - same shape as a
                // bound key, so the board reads consistently, but dimmer so it never competes
                // with the user's own binds for attention.
                TextRenderer.DrawText(g, cap.Label, CapFont,
                    new Rectangle(rect.X, rect.Y + 2, rect.Width, 12), Theme.TextDim,
                    TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(g, GameDefaults[cap.Id], BoundFont,
                    new Rectangle(rect.X + 1, rect.Y + 12, rect.Width - 2, rect.Height - 14),
                    Color.FromArgb(200, 240, 180, 90),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis);
            }
            else
            {
                TextRenderer.DrawText(g, bound ? Bound[cap.Id] : cap.Label, CapFont, rect,
                    bound ? Theme.Text : Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis);
            }
        }
    }

    public sealed class KeyboardKeyEventArgs : EventArgs
    {
        public KeyboardKeyEventArgs(string key, MouseButtons button) { Key = key; Button = button; }
        public string Key { get; }
        public MouseButtons Button { get; }
    }

    public sealed class KeyboardDropEventArgs : EventArgs
    {
        public KeyboardDropEventArgs(string key, string commandId) { Key = key; CommandId = commandId; }
        public string Key { get; }
        public string CommandId { get; }
    }
}
