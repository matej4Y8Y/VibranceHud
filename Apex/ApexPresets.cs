using System.Collections.Generic;

namespace VibranceHud.Apex
{
    /// <summary>
    /// A one-click Apex Legends loadout: which videoconfig tweaks should be on. Competitive
    /// turns every FPS tweak on; Cinematic leaves full visuals. Config only - no vibrance bundled.
    /// </summary>
    public sealed record ApexPreset(string Name, string Description, bool AllTweaksOn);

    public static class ApexPresets
    {
        public static readonly ApexPreset Competitive =
            new("Competitive", "All FPS tweaks on - max frames and a clean view.", AllTweaksOn: true);

        public static readonly ApexPreset Cinematic =
            new("Cinematic", "Full visuals and effects.", AllTweaksOn: false);

        public static readonly IReadOnlyList<ApexPreset> All = new[] { Competitive, Cinematic };
    }
}
