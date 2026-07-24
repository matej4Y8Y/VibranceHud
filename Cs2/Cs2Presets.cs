using System.Collections.Generic;

namespace VibranceHud.Cs2
{
    /// <summary>
    /// A one-click CS2 loadout: which autoexec tweaks should be on. Competitive turns the FPS
    /// tweaks on; Cinematic leaves full visuals. Config only - no vibrance bundled.
    /// </summary>
    public sealed record Cs2Preset(string Name, string Description, bool AllTweaksOn);

    public static class Cs2Presets
    {
        public static readonly Cs2Preset Competitive =
            new("Competitive", "All FPS tweaks on - max frames and a clean view.", AllTweaksOn: true);

        public static readonly Cs2Preset Cinematic =
            new("Cinematic", "Full visuals and effects.", AllTweaksOn: false);

        public static readonly IReadOnlyList<Cs2Preset> All = new[] { Competitive, Cinematic };
    }
}
