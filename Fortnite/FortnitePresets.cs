using System.Collections.Generic;

namespace VibranceHud.Fortnite
{
    /// <summary>
    /// A one-click Fortnite loadout: which GameUserSettings.ini tweaks should be on.
    /// Competitive turns every FPS tweak on; Cinematic leaves full visuals. Config only -
    /// no vibrance bundled.
    /// </summary>
    public sealed record FortnitePreset(string Name, string Description, bool AllTweaksOn);

    public static class FortnitePresets
    {
        public static readonly FortnitePreset Competitive =
            new("Competitive", "All FPS tweaks on - max frames and a clean view.", AllTweaksOn: true);

        public static readonly FortnitePreset Cinematic =
            new("Cinematic", "Full visuals and effects.", AllTweaksOn: false);

        public static readonly IReadOnlyList<FortnitePreset> All = new[] { Competitive, Cinematic };
    }
}
