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

        /// <summary>Length of each arm in pixels.</summary>
        public int Size { get; set; } = 8;

        /// <summary>Arm width in pixels.</summary>
        public int Thickness { get; set; } = 2;

        /// <summary>Empty space between the centre and the inner end of each arm.</summary>
        public int Gap { get; set; } = 4;

        /// <summary>Draw a dark outline so the crosshair stays visible on any background.</summary>
        public bool Outline { get; set; } = true;

        /// <summary>Add a dot in the middle (ignored for the Dot shape, which is only a dot).</summary>
        public bool CentreDot { get; set; }

        public CrosshairConfig Clone() => (CrosshairConfig)MemberwiseClone();
    }
}
