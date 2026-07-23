using System;
using System.Collections.Generic;

namespace VibranceHud
{
    /// <summary>One display mode the monitor reports as supported.</summary>
    public readonly record struct DisplayMode(int Width, int Height, int RefreshHz)
    {
        public override string ToString() => $"{Width}x{Height}";
    }

    /// <summary>
    /// Picks resolutions out of the monitor's reported mode list. Pure - no interop - so
    /// the "always use the highest refresh rate" rule is unit-tested.
    /// </summary>
    public static class DisplayModes
    {
        /// <summary>
        /// Distinct resolutions, each paired with the highest refresh rate the monitor
        /// supports for it. Sorted widest first, then tallest.
        /// </summary>
        public static IReadOnlyList<DisplayMode> BestPerResolution(IEnumerable<DisplayMode> modes)
        {
            var best = new Dictionary<(int, int), int>();
            foreach (var m in modes)
            {
                if (m.Width <= 0 || m.Height <= 0) continue;
                var key = (m.Width, m.Height);
                if (!best.TryGetValue(key, out var hz) || m.RefreshHz > hz)
                    best[key] = m.RefreshHz;
            }

            var list = new List<DisplayMode>();
            foreach (var ((w, h), hz) in best) list.Add(new DisplayMode(w, h, hz));
            list.Sort((a, b) => a.Width != b.Width
                ? b.Width.CompareTo(a.Width)
                : b.Height.CompareTo(a.Height));
            return list;
        }

        /// <summary>The highest refresh rate available at that resolution, or 0 if unsupported.</summary>
        public static int MaxRefreshFor(IEnumerable<DisplayMode> modes, int width, int height)
        {
            int max = 0;
            foreach (var m in modes)
                if (m.Width == width && m.Height == height && m.RefreshHz > max)
                    max = m.RefreshHz;
            return max;
        }

        /// <summary>Never apply a mode the monitor didn't report - that's how you black-screen someone.</summary>
        public static bool IsSupported(IEnumerable<DisplayMode> modes, int width, int height) =>
            MaxRefreshFor(modes, width, height) > 0;
    }
}
