using System;

namespace VibranceHud.Design
{
    /// <summary>
    /// The one place sizes come from.
    ///
    /// Every page used to invent its own numbers - 28 here, 24 there, then a -4 nudge to
    /// line something up - which is why the UI read as almost-aligned rather than aligned.
    /// These steps are the only spacing values new layout code may use.
    ///
    /// Values are LOGICAL pixels at 96 DPI. Run them through <see cref="Scale"/> before
    /// handing them to a control, so one piece of layout code is correct at 100% and 200%
    /// alike. That is what makes PerMonitorV2 possible: without a scale to drive it, going
    /// per-monitor just breaks every hardcoded coordinate in the app.
    /// </summary>
    public static class Tokens
    {
        public const int BaseDpi = 96;

        // Spacing scale. Nothing between these steps is permitted.
        public const int XS = 4;
        public const int S = 8;
        public const int M = 12;
        public const int L = 16;
        public const int XL = 24;
        public const int XXL = 32;
        public const int XXXL = 48;

        /// <summary>Live DPI. Set by the window on creation and on every DPI change.</summary>
        public static int Dpi { get; set; } = BaseDpi;

        /// <summary>Convert logical pixels to device pixels at the current DPI.</summary>
        public static int Scale(int logical) => ScaleAt(Dpi, logical);

        /// <summary>
        /// DPI-explicit form, so the maths is testable without a display.
        ///
        /// A positive input never returns 0: a 1px divider that rounds away to nothing is
        /// exactly how borders silently vanish at fractional scale factors.
        /// </summary>
        public static int ScaleAt(int dpi, int logical)
        {
            if (logical == 0) return 0;
            if (dpi <= 0) dpi = BaseDpi;

            int scaled = (int)Math.Round(logical * (dpi / (double)BaseDpi), MidpointRounding.AwayFromZero);
            return logical > 0 ? Math.Max(1, scaled) : Math.Min(-1, scaled);
        }

        /// <summary>Float form, for pen widths and other sub-pixel work.</summary>
        public static float ScaleF(float logical) => logical * (Dpi / (float)BaseDpi);
    }
}
