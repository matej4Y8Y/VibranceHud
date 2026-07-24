using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VibranceHud
{
    /// <summary>
    /// The set of themes the user can pick from. Colored themes reuse one matte-black dark
    /// base so only the accent has to be chosen; Light is separate. Lookup falls back to the
    /// default so an unknown/old saved name can never leave the app unstyled.
    /// </summary>
    public static class ThemeCatalog
    {
        public const string DefaultName = "Violet";

        private static ThemePalette Dark(string name, Color accent, Color accentDim,
            Color nodeA, Color nodeB, Color line) => new(
            name, IsLight: false,
            Background: Color.FromArgb(10, 10, 12),
            Surface: Color.FromArgb(22, 22, 27),
            SurfaceHover: Color.FromArgb(34, 33, 42),
            Border: Color.FromArgb(48, 47, 58),
            GlassFill: Color.FromArgb(10, 10, 12),
            GlassEdge: Color.FromArgb(148, 148, 158),
            Text: Color.FromArgb(240, 240, 246),
            TextDim: Color.FromArgb(128, 128, 142),
            Accent: accent, AccentDim: accentDim,
            PlexusNodeA: nodeA, PlexusNodeB: nodeB, PlexusLine: line);

        private static readonly ThemePalette Light = new(
            "Light", IsLight: true,
            Background: Color.FromArgb(244, 244, 247),
            Surface: Color.FromArgb(255, 255, 255),
            SurfaceHover: Color.FromArgb(230, 230, 236),
            Border: Color.FromArgb(203, 203, 212),
            GlassFill: Color.FromArgb(255, 255, 255),
            GlassEdge: Color.FromArgb(120, 120, 132),
            Text: Color.FromArgb(20, 20, 26),
            TextDim: Color.FromArgb(108, 108, 120),
            Accent: Color.FromArgb(26, 26, 32),
            AccentDim: Color.FromArgb(66, 66, 76),
            PlexusNodeA: Color.FromArgb(70, 70, 80),
            PlexusNodeB: Color.FromArgb(120, 120, 132),
            PlexusLine: Color.FromArgb(95, 95, 106));

        public static readonly IReadOnlyList<ThemePalette> All = new[]
        {
            Dark("Violet", Color.FromArgb(167, 139, 250), Color.FromArgb(109, 84, 190),
                Color.FromArgb(167, 139, 250), Color.FromArgb(232, 96, 214), Color.FromArgb(150, 130, 240)),
            Dark("Emerald", Color.FromArgb(52, 211, 153), Color.FromArgb(16, 150, 105),
                Color.FromArgb(52, 211, 153), Color.FromArgb(16, 185, 129), Color.FromArgb(40, 180, 130)),
            Dark("Crimson", Color.FromArgb(248, 113, 113), Color.FromArgb(180, 50, 60),
                Color.FromArgb(248, 113, 113), Color.FromArgb(225, 70, 100), Color.FromArgb(214, 74, 84)),
            Light,
        };

        /// <summary>The palette with this name, or the default if it's unknown.</summary>
        public static ThemePalette ByName(string? name) =>
            All.FirstOrDefault(p => p.Name == name) ?? All.First(p => p.Name == DefaultName);

        /// <summary>
        /// Which theme to use, honouring the new saved name but migrating the old boolean:
        /// a saved name wins; otherwise the legacy "light theme on" maps to Light; else default.
        /// </summary>
        public static ThemePalette Resolve(string? savedName, bool legacyLight)
        {
            if (!string.IsNullOrWhiteSpace(savedName) && All.Any(p => p.Name == savedName))
                return ByName(savedName);
            return legacyLight ? ByName("Light") : ByName(DefaultName);
        }
    }
}
