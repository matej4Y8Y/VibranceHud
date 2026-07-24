using System.Drawing;

namespace VibranceHud
{
    /// <summary>
    /// The app's color palette, swappable at runtime between a dark (purple) theme and a
    /// light black-and-white theme. Controls read these values in their paint code, so a
    /// theme change followed by a repaint (or window rebuild) restyles the whole UI.
    /// </summary>
    public static class Theme
    {
        public const string FontFamily = "Segoe UI";

        public static bool IsLight { get; private set; }

        /// <summary>The name of the currently applied theme (e.g. "Violet", "Emerald").</summary>
        public static string CurrentName { get; private set; } = ThemeCatalog.DefaultName;

        public static Color Background { get; private set; }
        public static Color Surface { get; private set; }
        public static Color SurfaceHover { get; private set; }
        public static Color Border { get; private set; }

        /// <summary>Base fill for frosted-glass panels.</summary>
        public static Color GlassFill { get; private set; }
        /// <summary>Grey rounded edge for glass panels.</summary>
        public static Color GlassEdge { get; private set; }

        public static Color Text { get; private set; }
        public static Color TextDim { get; private set; }

        /// <summary>Selection / primary accent (violet in dark, near-black in light).</summary>
        public static Color Accent { get; private set; }
        public static Color AccentDim { get; private set; }

        // Plexus background colors.
        public static Color PlexusNodeA { get; private set; }
        public static Color PlexusNodeB { get; private set; }
        public static Color PlexusLine { get; private set; }

        static Theme() => Apply(ThemeCatalog.ByName(ThemeCatalog.DefaultName));

        /// <summary>Apply a theme by name (falls back to the default if unknown).</summary>
        public static void Apply(string name) => Apply(ThemeCatalog.ByName(name));

        /// <summary>Copy a palette into the live theme so every control repaints in it.</summary>
        public static void Apply(ThemePalette p)
        {
            CurrentName = p.Name;
            IsLight = p.IsLight;
            Background = p.Background;
            Surface = p.Surface;
            SurfaceHover = p.SurfaceHover;
            Border = p.Border;
            GlassFill = p.GlassFill;
            GlassEdge = p.GlassEdge;
            Text = p.Text;
            TextDim = p.TextDim;
            Accent = p.Accent;
            AccentDim = p.AccentDim;
            PlexusNodeA = p.PlexusNodeA;
            PlexusNodeB = p.PlexusNodeB;
            PlexusLine = p.PlexusLine;
        }
    }
}
