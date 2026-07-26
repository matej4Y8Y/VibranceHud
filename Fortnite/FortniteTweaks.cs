using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Fortnite
{
    /// <summary>
    /// One Fortnite optimization, expressed as the GameUserSettings.ini settings it writes.
    /// Every (section, key) pair here is real and client-side (verified, not invented) - an
    /// invented one would silently do nothing.
    /// </summary>
    public sealed record FortniteTweak(string Label, string Description, IReadOnlyList<FortniteTweakValue> Values)
    {
        public bool IsOn(FortniteConfig cfg) =>
            Values.All(v => string.Equals(cfg.Get(v.Section, v.Key), v.On, System.StringComparison.OrdinalIgnoreCase));

        public void Write(ICollection<FortniteConfigEdit> edits, bool on)
        {
            foreach (var v in Values) edits.Add(new FortniteConfigEdit(v.Section, v.Key, on ? v.On : v.Off));
        }
    }

    /// <param name="On">Value when the optimization is applied.</param>
    /// <param name="Off">Value that restores stock behaviour.</param>
    public sealed record FortniteTweakValue(string Section, string Key, string On, string Off);

    /// <summary>One pending edit to GameUserSettings.ini: a section, a key, and the value to write.</summary>
    public sealed record FortniteConfigEdit(string Section, string Key, string Value);

    public static class FortniteTweaks
    {
        private const string GameUserSettings = "/Script/FortniteGame.FortGameUserSettings";
        private const string ScalabilityGroups = "ScalabilityGroups";

        private static FortniteTweak T(string label, string desc, params FortniteTweakValue[] values) =>
            new(label, desc, values);

        public static readonly IReadOnlyList<FortniteTweak> All = new[]
        {
            T("Low Scalability", "Drops view distance, shadows, anti-aliasing, textures, effects and post-processing to their lowest settings for max FPS.",
                new FortniteTweakValue(ScalabilityGroups, "sg.ViewDistanceQuality", "0", "2"),
                new FortniteTweakValue(ScalabilityGroups, "sg.ShadowsQuality", "0", "2"),
                new FortniteTweakValue(ScalabilityGroups, "sg.AntiAliasingQuality", "0", "2"),
                new FortniteTweakValue(ScalabilityGroups, "sg.TextureQuality", "0", "2"),
                new FortniteTweakValue(ScalabilityGroups, "sg.EffectsQuality", "0", "2"),
                new FortniteTweakValue(ScalabilityGroups, "sg.PostProcessQuality", "0", "2")),
            T("Uncapped Frame Rate", "Removes the frame-rate cap.",
                new FortniteTweakValue(GameUserSettings, "FrameRateLimit", "0.000000", "60.000000")),
            T("V-Sync Off", "Disables vertical sync for lower input lag.",
                new FortniteTweakValue(GameUserSettings, "bUseVsync", "False", "True")),
            T("Windowed Fullscreen", "Runs borderless windowed instead of exclusive fullscreen - also what lets the crosshair overlay draw.",
                new FortniteTweakValue(GameUserSettings, "FullscreenMode", "1", "0")),
        };
    }
}
