using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VibranceHud
{
    /// <summary>
    /// Keeps a restored window on a screen that still exists.
    ///
    /// The quick-vibrance popup already validates its saved position this way; the main
    /// window never saved one at all, so it opened dead-centre every launch. Restoring
    /// blindly would be worse than not restoring: a position on a monitor that has since
    /// been unplugged strands the window somewhere the user cannot drag it back from.
    ///
    /// Pure geometry - no Screen calls - so every case is testable without a display.
    /// </summary>
    public static class WindowBounds
    {
        /// <summary>How much of the window has to be on a screen for the saved position to
        /// count as usable. A few visible pixels is not a window the user can grab.</summary>
        private const int MinVisibleWidth = 120;
        private const int MinVisibleHeight = 60;

        public static Rectangle ClampToVisible(Rectangle saved, IEnumerable<Rectangle> screens)
        {
            if (saved.Width <= 0 || saved.Height <= 0) return Rectangle.Empty;

            var list = screens?.Where(s => s.Width > 0 && s.Height > 0).ToList() ?? new List<Rectangle>();
            if (list.Count == 0) return Rectangle.Empty;

            // The screen showing the most of this window is the one it belongs to.
            var host = list
                .Select(s => (screen: s, overlap: Overlap(s, saved)))
                .OrderByDescending(x => (long)x.overlap.Width * x.overlap.Height)
                .First();

            bool reachable = host.overlap.Width >= MinVisibleWidth
                          && host.overlap.Height >= MinVisibleHeight;

            var target = reachable ? host.screen : list[0];

            // Never bigger than the screen it will sit on.
            int w = Math.Min(saved.Width, target.Width);
            int h = Math.Min(saved.Height, target.Height);

            if (reachable && w == saved.Width && h == saved.Height)
                return saved;

            return new Rectangle(
                target.X + (target.Width - w) / 2,
                target.Y + (target.Height - h) / 2,
                w, h);
        }

        private static Rectangle Overlap(Rectangle a, Rectangle b)
        {
            var r = a;
            r.Intersect(b);
            return r;
        }
    }
}
