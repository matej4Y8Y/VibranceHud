using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VibranceHud.Display;

namespace VibranceHud.Controls
{
    /// <summary>
    /// One colour preset, shown as what it does rather than as its name.
    ///
    /// The strip is the app's sample colours - skin, foliage, sky, dirt, concrete, near-black -
    /// each run through the real pipeline for this preset. Not an approximation: a tile that
    /// only looked roughly right would be a promise the app breaks the moment it is applied.
    /// </summary>
    public sealed class PresetTile : Control
    {
        private bool _hover;
        private bool _active;

        public PresetTile(ColourPreset preset)
        {
            Preset = preset;

            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.Selectable, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.RadioButton;
            AccessibleName = preset.Name;
            Size = new Size(140, 74);
        }

        public ColourPreset Preset { get; }

        /// <summary>Raised when the pointer enters or leaves, so the page can show the larger
        /// preview for whichever tile is under the cursor.</summary>
        public event EventHandler? HoverChanged;

        public bool Active
        {
            get => _active;
            set { if (_active == value) return; _active = value; Invalidate(); }
        }

        public bool Hovered => _hover;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
            HoverChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
            HoverChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
            // Keyboard users get the same larger preview the mouse gets.
            HoverChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }

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
            Glass.PaintPanel(g, rect, 10f, fillAlpha: _active ? 190 : _hover ? 170 : 140);

            // The strip. Drawn first and clipped to the tile's own rounded top, so it reads as
            // part of the card rather than as a rectangle sitting on one.
            int stripH = Math.Max(6, Height / 3);
            int stripY = Height - stripH - 8;
            var samples = GameColourPresets.SampleColours;

            if (Width > 16 && stripH > 0)
            {
                float w = (Width - 16f) / samples.Length;
                for (int i = 0; i < samples.Length; i++)
                {
                    using var brush = new SolidBrush(GameColourPresets.Preview(Preset, samples[i]));
                    g.FillRectangle(brush, 8 + i * w, stripY, w + 0.5f, stripH);
                }

                using var rim = new Pen(Color.FromArgb(70, Theme.GlassEdge), 1f);
                g.DrawRectangle(rim, 8, stripY, Width - 16, stripH);
            }

            TextRenderer.DrawText(g, Preset.Name, Design.Fonts.BodyBold,
                new Rectangle(10, 6, Width - 20, 20),
                _active ? Theme.Text : Theme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (_active)
                using (var ring = new Pen(Theme.Accent, 1.6f))
                using (var path = Glass.RoundedPath(rect, 10f))
                    g.DrawPath(ring, path);

            if (Focused) UiHelpers.DrawFocusRing(g, ClientRectangle, 10f);
        }
    }

    /// <summary>
    /// The larger preview, shown for whichever tile the pointer is on.
    ///
    /// Exists because a 140px tile can show that a preset is warmer or louder, but not what it
    /// does to a face or to foliage. This is the same six samples at a size where that is
    /// actually readable, with the preset's name and the reason to pick it.
    /// </summary>
    public sealed class PresetPreviewPanel : Control
    {
        private ColourPreset? _preset;

        public PresetPreviewPanel()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);

            BackColor = Color.Transparent;
            Height = 96;
        }

        /// <summary>What to show. Null paints an empty frame rather than collapsing, so the
        /// row below does not jump every time the pointer leaves a tile.</summary>
        public ColourPreset? Preset
        {
            get => _preset;
            set { _preset = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Glass.PaintPanel(g, rect, 10f, fillAlpha: 150);

            if (_preset == null)
            {
                TextRenderer.DrawText(g, "Hover a preset to see it", Design.Fonts.Body,
                    ClientRectangle, Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var samples = GameColourPresets.SampleColours;
            int stripH = Math.Max(8, Height / 2 - 6);
            int stripY = Height - stripH - 10;

            if (Width > 24)
            {
                float w = (Width - 20f) / samples.Length;
                for (int i = 0; i < samples.Length; i++)
                {
                    using var brush = new SolidBrush(GameColourPresets.Preview(_preset, samples[i]));
                    g.FillRectangle(brush, 10 + i * w, stripY, w + 0.5f, stripH);
                }

                using var rim = new Pen(Color.FromArgb(80, Theme.GlassEdge), 1f);
                g.DrawRectangle(rim, 10, stripY, Width - 20, stripH);
            }

            TextRenderer.DrawText(g, _preset.Name, Design.Fonts.BodyBold,
                new Rectangle(12, 8, Width - 24, 18), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, _preset.Why, Design.Fonts.Caption,
                new Rectangle(12, 26, Width - 24, 20), Theme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
