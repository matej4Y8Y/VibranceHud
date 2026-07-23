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

        static Theme() => Apply(light: false);

        public static void Apply(bool light)
        {
            IsLight = light;
            if (light) ApplyLight();
            else ApplyDark();
        }

        private static void ApplyDark()
        {
            Background = Color.FromArgb(10, 10, 12);
            Surface = Color.FromArgb(22, 22, 27);
            SurfaceHover = Color.FromArgb(34, 33, 42);
            Border = Color.FromArgb(48, 47, 58);
            GlassFill = Color.FromArgb(10, 10, 12);
            GlassEdge = Color.FromArgb(148, 148, 158);
            Text = Color.FromArgb(240, 240, 246);
            TextDim = Color.FromArgb(128, 128, 142);
            Accent = Color.FromArgb(167, 139, 250);   // violet
            AccentDim = Color.FromArgb(109, 84, 190);
            PlexusNodeA = Color.FromArgb(167, 139, 250);
            PlexusNodeB = Color.FromArgb(232, 96, 214);
            PlexusLine = Color.FromArgb(150, 130, 240);
        }

        private static void ApplyLight()
        {
            Background = Color.FromArgb(244, 244, 247);
            Surface = Color.FromArgb(255, 255, 255);
            SurfaceHover = Color.FromArgb(230, 230, 236);
            Border = Color.FromArgb(203, 203, 212);
            GlassFill = Color.FromArgb(255, 255, 255);
            GlassEdge = Color.FromArgb(120, 120, 132);
            Text = Color.FromArgb(20, 20, 26);
            TextDim = Color.FromArgb(108, 108, 120);
            Accent = Color.FromArgb(26, 26, 32);       // near-black selection (monochrome)
            AccentDim = Color.FromArgb(66, 66, 76);
            PlexusNodeA = Color.FromArgb(70, 70, 80);   // dark grey nodes
            PlexusNodeB = Color.FromArgb(120, 120, 132);
            PlexusLine = Color.FromArgb(95, 95, 106);
        }
    }
}
