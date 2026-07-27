using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VibranceHud.Nvidia
{
    /// <summary>What the detected card can do. Ordered, so a higher tier is a superset.</summary>
    public enum GpuTier
    {
        None = 0,   // no NVIDIA GPU - the whole card hides
        Gtx = 1,    // GTX 9/10/16 series: driver settings only
        Rtx = 2,    // RTX 20/30
        Rtx40 = 3   // RTX 40/50: also has driver-level frame generation
    }

    public static class GpuCapability
    {
        /// <summary>
        /// Read the tier straight off the adapter name. Deliberately string-based rather
        /// than querying feature bits: the name is what NVAPI reliably gives on every
        /// driver version, and an unknown/odd name degrading to None is the safe failure
        /// (hide the feature) rather than offering something that won't work.
        /// </summary>
        public static GpuTier FromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return GpuTier.None;

            var rtx = Regex.Match(name, @"RTX\s*(\d{4})", RegexOptions.IgnoreCase);
            if (rtx.Success)
                return int.Parse(rtx.Groups[1].Value) >= 4000 ? GpuTier.Rtx40 : GpuTier.Rtx;

            if (Regex.IsMatch(name, @"\bGTX\b", RegexOptions.IgnoreCase)) return GpuTier.Gtx;

            return GpuTier.None;
        }
    }

    /// <summary>One driver-level setting, with the minimum card that supports it.</summary>
    public sealed record NvidiaTweak(
        string Id, string Label, string Description, GpuTier MinTier, string AppliedText);

    /// <summary>
    /// Driver settings that measurably help how Rust *feels*, applied per-game through
    /// NVIDIA's own profile system - no injection, nothing the driver doesn't already
    /// expose in Control Panel.
    ///
    /// Everything here works on a plain GTX card on purpose: a large share of the Rust
    /// audience is on GTX 16-series or older, and a card full of toggles that silently do
    /// nothing for them would be worse than not shipping it.
    /// </summary>
    public static class NvidiaTweakCatalog
    {
        public static readonly IReadOnlyList<NvidiaTweak> All = new[]
        {
            new NvidiaTweak("power-max", "Prefer Maximum Performance",
                "Stops the GPU dropping its clocks mid-fight. The single biggest cause of "
                + "sudden frame drops that feel like lag.",
                GpuTier.Gtx, "GPU held at full clocks"),

            new NvidiaTweak("low-latency", "Low Latency Mode",
                "Cuts the queue of frames waiting to be drawn, so what you see tracks your "
                + "mouse more closely.",
                GpuTier.Gtx, "Frame queue shortened"),

            new NvidiaTweak("texture-perf", "Texture Filtering: Performance",
                "Cheaper texture filtering. Small visual cost, real frame time saving.",
                GpuTier.Gtx, "Texture filtering set to performance"),

            new NvidiaTweak("vsync-off", "Force V-Sync Off",
                "Removes the driver-side sync that caps you to the monitor and adds delay. "
                + "Leave off if you use G-Sync.",
                GpuTier.Gtx, "V-Sync forced off"),

            new NvidiaTweak("fps-cap", "Steady Frame Rate Cap",
                "Caps frames just under what your PC can hold. A steady 90 looks smoother "
                + "than a 110 that keeps dipping - consistency is what reads as smooth.",
                GpuTier.Gtx, "Frame rate capped for consistency"),
        };

        /// <summary>Only what this card can actually do - the rest never reaches the UI.</summary>
        public static IReadOnlyList<NvidiaTweak> Available(GpuTier tier) =>
            tier == GpuTier.None
                ? Array.Empty<NvidiaTweak>()
                : All.Where(t => tier >= t.MinTier).ToList();
    }
}
