using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VibranceHud.Controls
{
    /// <summary>
    /// The app's own scrollbar: a slim glass track with a rounded thumb.
    ///
    /// Every scrolling surface in PlexusX previously hid the Win32 bar - it is a flat
    /// grey-and-white strip down the side of a dark card and cannot be themed - which left no
    /// indication that a page had more content at all. That cost more than it saved twice
    /// over: people could not tell a page scrolled, and a page that had silently stopped
    /// scrolling looked identical to one that had nothing below the fold.
    ///
    /// Drawn rather than hidden, so the state is always visible: if the thumb is there, there
    /// is more to see, and if it does not move, the bug is obvious instead of invisible.
    /// </summary>
    public sealed class GlassScrollBar : Control
    {
        private int _value;
        private int _maximum = 100;
        private int _viewport = 10;

        private bool _hover;
        private bool _dragging;
        private int _dragOffset;

        public GlassScrollBar()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.ResizeRedraw, true);

            BackColor = Color.Transparent;
            Width = 10;
            TabStop = false;   // the page it scrolls is the keyboard target, not the bar
        }

        /// <summary>Raised when the user moves the thumb.</summary>
        public event EventHandler? Scrolled;

        /// <summary>Total scrollable content height.</summary>
        public int Maximum
        {
            get => _maximum;
            set { _maximum = Math.Max(0, value); Clamp(); Invalidate(); }
        }

        /// <summary>How much of it is on screen at once.</summary>
        public int Viewport
        {
            get => _viewport;
            set { _viewport = Math.Max(1, value); Clamp(); Invalidate(); }
        }

        /// <summary>How far down, in content pixels.</summary>
        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Clamp(value, 0, Math.Max(0, _maximum - _viewport));
                if (clamped == _value) return;
                _value = clamped;
                Invalidate();
            }
        }

        /// <summary>True when there is anything to scroll. The bar hides itself otherwise -
        /// a full-length thumb that cannot move is just a decoration.</summary>
        public bool Needed => _maximum > _viewport;

        private void Clamp() => Value = _value;

        private int TrackTop => 4;
        private int TrackHeight => Math.Max(1, Height - 8);

        private int ThumbHeight
        {
            get
            {
                if (!Needed) return TrackHeight;
                // Proportional, with a floor: on a very long page an exactly proportional
                // thumb becomes a few pixels tall and impossible to grab.
                float share = _viewport / (float)_maximum;
                return Math.Max(28, (int)(TrackHeight * share));
            }
        }

        private int ThumbTop
        {
            get
            {
                if (!Needed) return TrackTop;
                float progress = _value / (float)(_maximum - _viewport);
                return TrackTop + (int)((TrackHeight - ThumbHeight) * progress);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Needed) return;

            if (e.Y >= ThumbTop && e.Y <= ThumbTop + ThumbHeight)
            {
                _dragging = true;
                _dragOffset = e.Y - ThumbTop;
            }
            else
            {
                // Clicking the track jumps the thumb to the pointer rather than paging, which
                // is what people expect from a bar this slim.
                MoveThumbTo(e.Y - ThumbHeight / 2);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) MoveThumbTo(e.Y - _dragOffset);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        private void MoveThumbTo(int top)
        {
            if (!Needed) return;

            int travel = TrackHeight - ThumbHeight;
            if (travel <= 0) return;

            float progress = Math.Clamp((top - TrackTop) / (float)travel, 0f, 1f);

            int wanted = (int)Math.Round(progress * (_maximum - _viewport));
            if (wanted == _value) return;

            _value = wanted;
            Invalidate();
            Scrolled?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Needed) return;   // nothing to scroll, nothing to draw

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float radius = Math.Max(1f, (Width - 4) / 2f);

            // The track: barely there, just enough to show how far down you are.
            var track = new RectangleF(2f, TrackTop, Math.Max(1, Width - 4), TrackHeight);
            using (var path = Glass.RoundedPath(track, radius))
            using (var back = new SolidBrush(Color.FromArgb(40, Theme.GlassEdge)))
                g.FillPath(back, path);

            var thumb = new RectangleF(2f, ThumbTop, Math.Max(1, Width - 4), ThumbHeight);
            using (var path = Glass.RoundedPath(thumb, radius))
            {
                int alpha = _dragging ? 235 : _hover ? 200 : 150;
                using var fill = new SolidBrush(Color.FromArgb(alpha, Theme.Accent));
                g.FillPath(fill, path);
            }
        }
    }
}
