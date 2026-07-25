using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace VibranceHud.Theming
{
    /// <summary>
    /// The optional user image painted behind the whole app, underneath the plexus field.
    ///
    /// The particle field repaints continuously, so the scaled + dimmed bitmap is built
    /// once and cached: per frame this is a single blit, no more expensive than the solid
    /// fill it replaces. Rescaling a 4K wallpaper every frame would visibly stutter.
    ///
    /// Like the field, the cached image is sized to the WHOLE window and each panel/page
    /// draws its slice via an offset, so the picture runs continuously behind the nav and
    /// title bar instead of restarting in every control.
    /// </summary>
    public static class AppBackground
    {
        private static Bitmap? _source;
        private static Bitmap? _cache;
        private static int _cacheW, _cacheH, _cacheDim = -1, _cacheBlur = -1;

        /// <summary>0-80: how far the image is darkened so the UI stays readable.</summary>
        public static int Dim { get; private set; }

        /// <summary>0-100: how far the image is softened. Baked into the cached bitmap,
        /// so it costs nothing per frame.</summary>
        public static int Blur { get; private set; }

        public const int MaxBlur = 100;

        public static bool IsSet => _source != null;

        /// <summary>Load an image. Returns false (leaving the previous one) if it can't be
        /// read - a missing or corrupt file must never take the app down.</summary>
        public static bool Load(string path, int dim, int blur = 0)
        {
            try
            {
                // Copy through a stream so the file isn't left locked on disk.
                Bitmap copy;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var img = Image.FromStream(fs))
                    copy = new Bitmap(img);

                _source?.Dispose();
                _source = copy;
                Dim = Math.Clamp(dim, ImagePalette.MinDim, ImagePalette.MaxDim);
                Blur = Math.Clamp(blur, 0, MaxBlur);
                InvalidateCache();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void SetDim(int dim)
        {
            int d = Math.Clamp(dim, ImagePalette.MinDim, ImagePalette.MaxDim);
            if (d == Dim) return;
            Dim = d;
            InvalidateCache();
        }

        public static void SetBlur(int blur)
        {
            int b = Math.Clamp(blur, 0, MaxBlur);
            if (b == Blur) return;
            Blur = b;
            InvalidateCache();
        }

        public static void Clear()
        {
            _source?.Dispose();
            _source = null;
            InvalidateCache();
        }

        /// <summary>A small copy of the image for colour extraction - averages out
        /// compression noise and keeps the vote cheap.</summary>
        public static Color[] SamplePixels(int side = 64)
        {
            if (_source == null) return Array.Empty<Color>();

            using var small = new Bitmap(side, side);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(_source, 0, 0, side, side);
            }

            var px = new Color[side * side];
            for (int y = 0, i = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    px[i++] = small.GetPixel(x, y);
            return px;
        }

        private static int _winW, _winH;

        /// <summary>Told the window size by the window, exactly like the particle field is,
        /// so every panel and page draws a slice of the same picture.</summary>
        public static void Resize(int w, int h)
        {
            _winW = Math.Max(1, w);
            _winH = Math.Max(1, h);
        }

        public static void Paint(Graphics g, int offsetX, int offsetY)
        {
            var bmp = Cached(_winW, _winH);
            if (bmp == null) return;
            g.DrawImage(bmp, -offsetX, -offsetY, bmp.Width, bmp.Height);
        }

        private static Bitmap? Cached(int w, int h)
        {
            if (_source == null || w <= 0 || h <= 0) return null;
            if (_cache != null && _cacheW == w && _cacheH == h
                && _cacheDim == Dim && _cacheBlur == Blur) return _cache;

            var built = new Bitmap(w, h);
            using (var g = Graphics.FromImage(built))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Cover the window without distorting: scale to fill, centre the overflow.
                float scale = Math.Max(w / (float)_source.Width, h / (float)_source.Height);
                int dw = (int)Math.Ceiling(_source.Width * scale);
                int dh = (int)Math.Ceiling(_source.Height * scale);
                if (Blur > 0)
                {
                    // Downscale-then-upscale: GDI+ has no gaussian, but a bilinear round
                    // trip through a smaller bitmap is a good soft blur and costs almost
                    // nothing. Bigger divisor = softer.
                    int div = 1 + Blur / 6;
                    int sw = Math.Max(2, dw / div), sh = Math.Max(2, dh / div);
                    using var small = new Bitmap(sw, sh);
                    using (var sg = Graphics.FromImage(small))
                    {
                        sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        sg.DrawImage(_source, 0, 0, sw, sh);
                    }
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(small, (w - dw) / 2, (h - dh) / 2, dw, dh);
                }
                else
                {
                    g.DrawImage(_source, (w - dw) / 2, (h - dh) / 2, dw, dh);
                }

                // Bake the dim in rather than compositing it every frame.
                if (Dim > 0)
                    using (var shade = new SolidBrush(Color.FromArgb(Dim * 255 / 100, 0, 0, 0)))
                        g.FillRectangle(shade, 0, 0, w, h);
            }

            _cache?.Dispose();
            _cache = built;
            _cacheW = w; _cacheH = h; _cacheDim = Dim; _cacheBlur = Blur;
            return _cache;
        }

        private static void InvalidateCache()
        {
            _cache?.Dispose();
            _cache = null;
            _cacheDim = -1;
            _cacheBlur = -1;
        }
    }
}
