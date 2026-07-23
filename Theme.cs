using System.Drawing;

namespace VibranceHud
{
    /// <summary>
    /// The one place the HUD's look lives. Premium-dark rules applied here:
    ///  - Layered near-blacks (never pure #000) so surfaces separate and gain depth.
    ///  - A single violet accent, used only on "live" elements (value, slider fill,
    ///    brand mark, button text) - restraint is what reads as premium.
    ///  - A ~50%-opacity white edge on the card: simulates light catching the rim of
    ///    glass, which is why dark rounded panels with faint white borders feel like
    ///    physical translucent objects instead of flat rectangles.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Background = Color.FromArgb(10, 10, 12);
        public static readonly Color Surface = Color.FromArgb(22, 22, 27);
        public static readonly Color SurfaceHover = Color.FromArgb(34, 33, 42);
        public static readonly Color Border = Color.FromArgb(48, 47, 58);

        /// <summary>Glass rim: grey at low opacity, drawn 1px around the card/window -
        /// a soft, barely-there edge rather than the old bright white line.</summary>
        public static readonly Color GlassEdge = Color.FromArgb(64, 150, 150, 150);

        public static readonly Color Text = Color.FromArgb(240, 240, 246);
        public static readonly Color TextDim = Color.FromArgb(128, 128, 142);

        public static readonly Color Accent = Color.FromArgb(167, 139, 250);      // violet
        public static readonly Color AccentDim = Color.FromArgb(109, 84, 190);

        public const string FontFamily = "Segoe UI";
    }
}
