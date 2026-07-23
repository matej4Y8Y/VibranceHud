using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Rust
{
    /// <summary>
    /// One optimization toggle, expressed as the convars it writes. Every convar here was
    /// verified to exist in a real Rust client.cfg - we never invent convar names, because
    /// an invented one would silently do nothing.
    /// </summary>
    public sealed record Tweak(string Label, IReadOnlyList<TweakValue> Values)
    {
        /// <summary>True when every convar already sits at its optimized value.</summary>
        public bool IsOn(RustConfig cfg) =>
            Values.All(v => string.Equals(cfg.Get(v.Convar), v.On, System.StringComparison.OrdinalIgnoreCase));

        public void Write(IDictionary<string, string> changes, bool on)
        {
            foreach (var v in Values) changes[v.Convar] = on ? v.On : v.Off;
        }
    }

    /// <param name="On">Value when the optimization is applied.</param>
    /// <param name="Off">Value that restores stock behaviour.</param>
    public sealed record TweakValue(string Convar, string On, string Off);

    public static class RustTweaks
    {
        private static Tweak T(string label, params TweakValue[] values) => new(label, values);

        /// <summary>Performance-focused toggles.</summary>
        public static readonly IReadOnlyList<Tweak> Performance = new[]
        {
            T("Disable Gibs",       new TweakValue("effects.maxgibs", "0", "-1")),
            T("Disable Blood",      new TweakValue("global.showblood", "False", "True")),
            T("Low Grass Quality",  new TweakValue("grass.quality", "0", "2"),
                                    new TweakValue("grass.displacement", "False", "True")),
            T("No Depth of Field",  new TweakValue("graphics.dof", "False", "True")),
            T("No Contact Shadows", new TweakValue("graphics.contactshadows", "False", "True")),
            T("No Soft Particles",  new TweakValue("graphicssettings.softparticles", "False", "True")),
            T("Fast Shadow LOD",    new TweakValue("graphics.aggressiveshadowlod", "True", "False")),
            T("VSync Off",          new TweakValue("graphics.vsync", "0", "1")),
        };

        /// <summary>Quality-of-life toggles.</summary>
        public static readonly IReadOnlyList<Tweak> QualityOfLife = new[]
        {
            T("Hide Own Legs",   new TweakValue("legs.enablelegs", "False", "True")),
            T("No Camera Shake", new TweakValue("client.clampscreenshake", "True", "False")),
            T("Instant Craft UI", new TweakValue("inventory.quickcraft_button_delay", "0", "0.5"),
                                  new TweakValue("inventory.quickcraft_rebuild_delay", "0", "0.025")),
        };

        public static IReadOnlyList<Tweak> All =>
            Performance.Concat(QualityOfLife).ToList();
    }
}
