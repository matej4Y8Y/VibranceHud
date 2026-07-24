using System.Drawing;

namespace VibranceHud
{
    /// <summary>
    /// One named colour scheme. The colored dark themes share a matte-black base and differ
    /// only in the accent + plexus colours; Light is the monochrome scheme. <see cref="Theme"/>
    /// copies the chosen palette into its static properties so all the paint code stays the same.
    /// </summary>
    public sealed record ThemePalette(
        string Name, bool IsLight,
        Color Background, Color Surface, Color SurfaceHover, Color Border,
        Color GlassFill, Color GlassEdge, Color Text, Color TextDim,
        Color Accent, Color AccentDim,
        Color PlexusNodeA, Color PlexusNodeB, Color PlexusLine)
    {
        /// <summary>The dot shown in the Settings theme picker for this theme.</summary>
        public Color Swatch => Accent;
    }
}
