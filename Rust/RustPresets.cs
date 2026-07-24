using System.Collections.Generic;

namespace VibranceHud.Rust
{
    /// <summary>
    /// A one-click Rust loadout: graphics quality + FPS limit + FOV, and which optimization
    /// tweaks (by <see cref="Tweak.Label"/>) should be on. Config only - vibrance is a separate
    /// global setting and deliberately isn't bundled here.
    /// </summary>
    public sealed record RustPreset(string Name, string Description,
        int Quality, int Fps, int Fov, IReadOnlySet<string> TweaksOn);

    public static class RustPresets
    {
        /// <summary>Everything off the shelf for max frames and clarity.</summary>
        public static readonly RustPreset Competitive = new(
            "Competitive", "Lowest visuals, max FPS - built for frames and clarity.",
            Quality: 1, Fps: 0, Fov: 90,
            TweaksOn: new HashSet<string>
            {
                "Disable Gibs", "Disable Blood", "Low Grass Quality", "No Depth of Field",
                "No Contact Shadows", "No Soft Particles", "Fast Shadow LOD", "VSync Off",
                "No Camera Shake", "Instant Craft UI",
            });

        /// <summary>Full visuals for a good-looking, cinematic experience.</summary>
        public static readonly RustPreset Cinematic = new(
            "Cinematic", "High visuals and effects - for the best-looking game.",
            Quality: 4, Fps: 144, Fov: 90,
            TweaksOn: new HashSet<string>());

        public static readonly IReadOnlyList<RustPreset> All = new[] { Competitive, Cinematic };
    }
}
