namespace VibranceHud.Crosshair
{
    /// <summary>
    /// The bounds every crosshair value has to stay inside, in tenths of a pixel.
    ///
    /// One home for these, because they were previously written only as literals in the
    /// page's slider constructors - which meant the share codec had no way to know them and
    /// accepted anything a code contained. A decoded value outside a slider's range leaves the
    /// page disagreeing with itself: the crosshair draws at the decoded size while the slider
    /// shows its own maximum, and saving then persists the number nobody can see.
    ///
    /// The page reads these too, so a bound can only ever be changed in one place.
    /// </summary>
    public static class CrosshairLimits
    {
        public const int MinSizeTenths = 5;
        public const int MaxSizeTenths = 300;

        public const int MinThicknessTenths = 5;
        public const int MaxThicknessTenths = 100;

        public const int MinGapTenths = 0;
        public const int MaxGapTenths = 300;

        public const int MinDotTenths = 5;
        public const int MaxDotTenths = 100;

        public const int MinRingTenths = 10;
        public const int MaxRingTenths = 400;

        /// <summary>Percent, not tenths. Ten rather than zero: a crosshair at zero opacity is
        /// invisible, and a control whose far end hides the thing it configures is broken.</summary>
        public const int MinOpacity = 10;
        public const int MaxOpacity = 100;
    }
}
