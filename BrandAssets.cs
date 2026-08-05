using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace VibranceHud
{
    /// <summary>
    /// Loads the embedded brand images - the horizontal PlexusX logo lockup and the scene
    /// preset art - and picks the right one for the current theme. Cached so a ~30fps repaint
    /// never re-decodes a PNG.
    /// </summary>
    public static class BrandAssets
    {
        private static readonly Dictionary<string, Image?> _cache = new();

        /// <summary>
        /// Guards the cache.
        ///
        /// The dictionary used to be touched without one. In the app that is almost always a
        /// single UI thread, but "almost" is the problem: a plain Dictionary read concurrently
        /// with a write can return a wrong value or loop forever, and it showed up as a chip
        /// intermittently coming back with no art at all.
        /// </summary>
        private static readonly object _gate = new();

        /// <summary>Which embedded logo to use for a light vs dark theme.</summary>
        public static string LogoResourceName(bool light) =>
            light ? "logo-horizontal-black.png" : "logo-horizontal-white.png";

        /// <summary>The horizontal logo image for the current theme (null if unavailable).</summary>
        public static Image? HorizontalLogo(bool light) => Load(LogoResourceName(light));

        /// <summary>Embedded scene-preset art. Pass "balanced" / "forest" / "desert" /
        /// "snow".</summary>
        public static Image? PresetChip(string name) => Load("preset-" + name + ".png");

        private static Image? Load(string resourceName)
        {
            lock (_gate)
            {
                if (_cache.TryGetValue(resourceName, out var cached)) return cached;

                Image? image = null;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        // Copy the pixels into a Bitmap we own outright.
                        //
                        // Image.FromStream keeps a reference to the stream for the lifetime of
                        // the image - GDI+ reads from it lazily - so returning one built on a
                        // `using` stream hands back an image whose backing store has already
                        // been disposed. It survives long enough to look fine and then throws
                        // "a generic error occurred in GDI+" the next time it is drawn.
                        using var decoded = Image.FromStream(stream);
                        image = new Bitmap(decoded);
                    }
                }

                // Only successes are cached. Caching a null would make one transient failure
                // permanent for the life of the process - the chip would lose its backdrop
                // and never get it back, with no way to retry.
                if (image != null) _cache[resourceName] = image;
                return image;
            }
        }
    }
}
