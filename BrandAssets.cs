using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace VibranceHud
{
    /// <summary>
    /// Loads the embedded brand images (the horizontal PlexusX logo lockup) and picks the
    /// right colour for the current theme - white on the dark themes, black on Light.
    /// Cached so the ~30fps repaint never re-decodes a PNG.
    /// </summary>
    public static class BrandAssets
    {
        private static readonly Dictionary<string, Image> _cache = new();

        /// <summary>Which embedded logo to use for a light vs dark theme.</summary>
        public static string LogoResourceName(bool light) =>
            light ? "logo-horizontal-black.png" : "logo-horizontal-white.png";

        /// <summary>The horizontal logo image for the current theme (null if unavailable).</summary>
        public static Image? HorizontalLogo(bool light)
        {
            var name = LogoResourceName(light);
            if (_cache.TryGetValue(name, out var cached)) return cached;

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) return null;
            var image = Image.FromStream(stream);
            _cache[name] = image;
            return image;
        }
    }
}
