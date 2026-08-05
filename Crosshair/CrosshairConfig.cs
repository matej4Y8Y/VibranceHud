namespace VibranceHud.Crosshair
{
    public enum CrosshairShape
    {
        Cross,
        Dot,
        Circle,
        T
    }

    /// <summary>
    /// One saved crosshair. Plain data so it serialises straight into settings and the
    /// geometry can be built from it without touching a window.
    /// </summary>
    public sealed class CrosshairConfig
    {
        public string Name { get; set; } = "New crosshair";
        public CrosshairShape Shape { get; set; } = CrosshairShape.Cross;

        /// <summary>Colour as ARGB, so it round-trips through JSON without a converter.</summary>
        public int ColourArgb { get; set; } = unchecked((int)0xFF00FF66);

        /// <summary>Legacy whole-pixel arm length. Kept only so a crosshair saved before the
        /// sliders gained decimals still loads at the shape its owner set; new code reads
        /// <see cref="ResolvedSize"/>.</summary>
        public int Size { get; set; } = 8;

        /// <summary>Legacy whole-pixel arm width. See <see cref="Size"/>.</summary>
        public int Thickness { get; set; } = 2;

        /// <summary>Legacy whole-pixel centre gap. See <see cref="Size"/>.</summary>
        public int Gap { get; set; } = 4;

        // ---- tenths ----
        //
        // Whole pixels were too coarse to aim with. At the sizes people actually use, one
        // step of thickness is the difference between a usable crosshair and an unusable one,
        // and there was nothing between 2 and 3.
        //
        // Stored as tenths of a pixel in an int rather than as a float: the sliders are
        // integer controls, JSON round-trips an int exactly, and it keeps every saved value
        // free of binary-fraction drift. Null means "saved before this existed", which
        // migrates from the whole-pixel value above rather than resetting the crosshair.

        /// <summary>Arm length in tenths of a pixel. Null on configs saved before decimals.</summary>
        public int? SizeTenths { get; set; }

        /// <summary>Arm width in tenths of a pixel. Null on configs saved before decimals.</summary>
        public int? ThicknessTenths { get; set; }

        /// <summary>Centre gap in tenths of a pixel. Null on configs saved before decimals.</summary>
        public int? GapTenths { get; set; }

        /// <summary>Arm length actually used, in pixels.</summary>
        public float ResolvedSize => (SizeTenths ?? Size * 10) / 10f;

        /// <summary>Arm width actually used, in pixels.</summary>
        public float ResolvedThickness => (ThicknessTenths ?? Thickness * 10) / 10f;

        /// <summary>Centre gap actually used, in pixels.</summary>
        public float ResolvedGap => (GapTenths ?? Gap * 10) / 10f;

        /// <summary>
        /// Write a tenths value and keep the legacy field roughly in step.
        ///
        /// The legacy fields are still written because a user can downgrade, and a build that
        /// only knows about whole pixels would otherwise read 8 for a crosshair they had set
        /// to 3.4. Rounded rather than truncated so the fallback is the nearest shape, not a
        /// systematically thinner one.
        /// </summary>
        public void SetSizeTenths(int tenths)
        {
            SizeTenths = tenths;
            Size = LegacyPixels(tenths, floor: 1);
        }

        public void SetThicknessTenths(int tenths)
        {
            ThicknessTenths = tenths;
            Thickness = LegacyPixels(tenths, floor: 1);
        }

        public void SetGapTenths(int tenths)
        {
            GapTenths = tenths;
            // A gap of zero is a legitimate crosshair, so this one may round to nothing.
            Gap = LegacyPixels(tenths, floor: 0);
        }

        /// <summary>
        /// Nearest whole pixel for the legacy field, never below the floor.
        ///
        /// The floor is load-bearing for size and thickness. Anything under 0.5px rounds to
        /// zero, and a build that only reads the whole-pixel field would then draw a crosshair
        /// with no length or no width - an invisible crosshair, from a downgrade, with nothing
        /// on screen to explain it.
        ///
        /// AwayFromZero rather than the default: .NET rounds halves to even, so 0.5 would
        /// land on 0 and 2.5 on 2, making the fallback systematically thinner than what the
        /// user chose.
        /// </summary>
        private static int LegacyPixels(int tenths, int floor) =>
            System.Math.Max(floor,
                (int)System.Math.Round(tenths / 10.0, System.MidpointRounding.AwayFromZero));

        /// <summary>Draw a dark outline so the crosshair stays visible on any background.</summary>
        public bool Outline { get; set; } = true;

        /// <summary>Add a dot in the middle (ignored for the Dot shape, which is only a dot).</summary>
        public bool CentreDot { get; set; }

        public CrosshairConfig Clone() => (CrosshairConfig)MemberwiseClone();
    }
}
