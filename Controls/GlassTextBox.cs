using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud
{
    /// <summary>
    /// A text field in the app's glass language: a rounded, themed fill with a hairline rim
    /// that brightens to the accent while it holds focus.
    ///
    /// Every text field in PlexusX was a stock <see cref="TextBox"/> with
    /// <c>BorderStyle.FixedSingle</c>. That border is drawn by Win32 in a colour the app does
    /// not choose, and it is square - so each field read as a hard grey rectangle stamped onto
    /// a rounded glass card. Same complaint as the stock Button, same answer.
    ///
    /// Unlike <see cref="GlassDropdown"/> this hosts a real TextBox rather than reimplementing
    /// it: caret placement, selection, IME, undo and clipboard are not worth rewriting to
    /// change a border. The inner box is borderless and painted the same colour as the fill,
    /// so the only thing left of the Win32 chrome is the text itself.
    /// </summary>
    public sealed class GlassTextBox : Control
    {
        private readonly TextBox _inner;

        /// <summary>Horizontal inset. Wide enough that the inner box - which is square, and
        /// solid - stays clear of the rounded corners it sits inside.</summary>
        private const int SideInset = 10;

        public GlassTextBox()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);

            BackColor = Color.Transparent;

            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 9.5f),
            };

            // The rim is the only thing that shows focus, and the rim belongs to the parent -
            // so the parent has to repaint when the inner box gains or loses it.
            _inner.GotFocus += (s, e) => Invalidate();
            _inner.LostFocus += (s, e) => Invalidate();
            _inner.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);

            Controls.Add(_inner);

            // Last. Assigning Height lays the control out, and a layout that runs before
            // _inner exists dereferences null - which is exactly what setting it in the first
            // line of the constructor did.
            Height = 30;
        }

        /// <summary>The hosted box, for the few callers that need more than text - read-only,
        /// selection, a monospace font. Its border and colours are the wrapper's business.</summary>
        public TextBox Inner => _inner;

        [System.ComponentModel.Browsable(true)]
        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value;
        }

        // Passed straight through, so a caller can still write one object initialiser instead
        // of a constructor followed by four lines of Inner.Whatever.

        public string PlaceholderText
        {
            get => _inner.PlaceholderText;
            set => _inner.PlaceholderText = value;
        }

        public bool ReadOnly
        {
            get => _inner.ReadOnly;
            set => _inner.ReadOnly = value;
        }

        public int MaxLength
        {
            get => _inner.MaxLength;
            set => _inner.MaxLength = value;
        }

        public CharacterCasing CharacterCasing
        {
            get => _inner.CharacterCasing;
            set => _inner.CharacterCasing = value;
        }

        /// <summary>Multiline fields fill the container instead of sitting on one centred
        /// line, and get a scrollbar rather than silently hiding the overflow.</summary>
        public bool Multiline
        {
            get => _inner.Multiline;
            set
            {
                _inner.Multiline = value;
                // Deliberately no scrollbar. A Win32 scrollbar is the one part of a TextBox
                // that cannot be themed - a flat grey strip down the side of a glass panel -
                // and it is the same reason the pages hide theirs. The wheel, the caret and
                // Page Up/Down all still scroll a multiline TextBox without it.
                if (value) _inner.ScrollBars = ScrollBars.None;
                PerformLayout();
            }
        }

        /// <summary>Focus goes to the box that can actually take text, not to the wrapper -
        /// otherwise tabbing into the field leaves the caret nowhere.</summary>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _inner.Focus();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            // WinForms can lay a control out before its own constructor has finished - any
            // size assignment does it - so this cannot assume the field is there yet.
            if (_inner is null) return;

            int w = Math.Max(1, Width - SideInset * 2);

            if (_inner.Multiline)
            {
                // Fills, minus the rim. PreferredHeight is a single line's worth and would
                // crop a multiline field to its first row.
                _inner.SetBounds(SideInset, 6, w, Math.Max(1, Height - 12));
                return;
            }

            // Centred on its own line height rather than filling the control: a TextBox is as
            // tall as its font and stretching it just leaves dead space above the text.
            int h = Math.Min(_inner.PreferredHeight, Math.Max(1, Height - 6));
            _inner.SetBounds(SideInset, Math.Max(0, (Height - h) / 2), w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Re-read on every paint so a theme switch cannot leave the inner box painting the
            // old palette - the failure the stock buttons had, and the reason RestylePrimary
            // had to exist at all.
            if (_inner.BackColor != Theme.Surface) _inner.BackColor = Theme.Surface;
            if (_inner.ForeColor != Theme.Text) _inner.ForeColor = Theme.Text;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = Math.Min(8f, (Height - 1) / 2f);

            using (var path = Glass.RoundedPath(rect, radius))
            {
                using (var fill = new SolidBrush(Theme.Surface))
                    g.FillPath(fill, path);

                bool focused = _inner.Focused;
                using var rim = new Pen(focused ? Theme.Accent : Theme.Border, focused ? 1.4f : 1f);
                g.DrawPath(rim, path);
            }
        }
    }
}
