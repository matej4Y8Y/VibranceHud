using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Apex
{
    /// <summary>
    /// One Apex Legends optimization, expressed as the videoconfig.txt settings it writes.
    /// Every key here is a real, client-side Apex setting (verified, not invented) - an
    /// invented one would silently do nothing.
    /// </summary>
    public sealed record ApexTweak(string Label, string Description, IReadOnlyList<ApexTweakValue> Values)
    {
        public bool IsOn(ApexConfig cfg) =>
            Values.All(v => string.Equals(cfg.Get(v.Key), v.On, System.StringComparison.OrdinalIgnoreCase));

        public void Write(IDictionary<string, string> changes, bool on)
        {
            foreach (var v in Values) changes[v.Key] = on ? v.On : v.Off;
        }
    }

    /// <param name="On">Value when the optimization is applied.</param>
    /// <param name="Off">Value that restores stock behaviour.</param>
    public sealed record ApexTweakValue(string Key, string On, string Off);

    public static class ApexTweaks
    {
        private static ApexTweak T(string label, string desc, params ApexTweakValue[] values) =>
            new(label, desc, values);

        public static readonly IReadOnlyList<ApexTweak> All = new[]
        {
            T("Uncapped FPS", "Removes the frame-rate cap (fps_max 0).",
                new ApexTweakValue("setting.fps_max", "0", "144")),
            T("Disable Shadows", "Turns off dynamic shadows for a steadier frame-rate.",
                new ApexTweakValue("setting.csm_enabled", "0", "1")),
            T("Low Model Detail", "Switches models to their lowest detail level sooner.",
                new ApexTweakValue("setting.r_lod_switch_scale", "0.35", "1")),
            T("Disable Anti-Aliasing", "Turns off anti-aliasing for more FPS.",
                new ApexTweakValue("setting.mat_antialias_mode", "0", "12")),
            T("Disable Adaptive Resolution", "Stops the game from dynamically scaling render resolution.",
                new ApexTweakValue("setting.dvs_enable", "0", "1")),
        };
    }
}
