using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Cs2
{
    /// <summary>
    /// One CS2 optimization, expressed as the autoexec convars it writes. Every convar here
    /// is a real, client-side CS2 command (verified, not invented) - an invented one would
    /// silently do nothing.
    /// </summary>
    public sealed record Cs2Tweak(string Label, string Description, IReadOnlyList<Cs2TweakValue> Values)
    {
        public bool IsOn(Cs2Config cfg) =>
            Values.All(v => string.Equals(cfg.Get(v.Convar), v.On, System.StringComparison.OrdinalIgnoreCase));

        public void Write(IDictionary<string, string> changes, bool on)
        {
            foreach (var v in Values) changes[v.Convar] = on ? v.On : v.Off;
        }
    }

    /// <param name="On">Value when the optimization is applied.</param>
    /// <param name="Off">Value that restores stock behaviour.</param>
    public sealed record Cs2TweakValue(string Convar, string On, string Off);

    public static class Cs2Tweaks
    {
        private static Cs2Tweak T(string label, string desc, params Cs2TweakValue[] values) =>
            new(label, desc, values);

        public static readonly IReadOnlyList<Cs2Tweak> All = new[]
        {
            T("Uncapped FPS", "Removes the frame-rate cap (fps_max 0).",
                new Cs2TweakValue("fps_max", "0", "400")),
            T("Disable Dynamic Lighting", "Turns off dynamic lights for a steadier frame-rate.",
                new Cs2TweakValue("r_dynamic", "0", "1")),
            T("No First-Person Tracers", "Hides your own bullet tracers - small GPU saving, cleaner view.",
                new Cs2TweakValue("r_drawtracers_firstperson", "0", "1")),
            T("Reduce Particles", "Draws fewer particle effects for more FPS in busy fights.",
                new Cs2TweakValue("r_drawparticles", "0", "1")),
        };
    }
}
