using System;
using System.Linq;
using NvAPIWrapper;
using NvAPIWrapper.DRS;
using NvAPIWrapper.GPU;

namespace VibranceHud.Nvidia
{
    /// <summary>Applies driver tweaks. Injected so the UI is testable without a GPU.</summary>
    public interface INvidiaDriverSettings
    {
        GpuTier Tier { get; }
        bool Apply(string tweakId, bool on, int fpsCap);
    }

    /// <summary>
    /// Writes NVIDIA's own per-application profile settings for Rust - the same values
    /// the Control Panel writes, through the documented driver-settings API. No injection
    /// and nothing game-side, so it carries no anti-cheat risk.
    ///
    /// Every call is defensive: a driver that doesn't expose a given setting, or a session
    /// that won't open, returns false instead of throwing. A tweak that can't be applied
    /// must never take the app down or leave the toggle lying about its state.
    /// </summary>
    public sealed class NvidiaDriverSettings : INvidiaDriverSettings
    {
        private const string RustExe = "RustClient.exe";
        private const string ProfileName = "PlexusX - Rust";

        public GpuTier Tier { get; }

        public NvidiaDriverSettings()
        {
            // NVAPI must be initialised before ANY of its APIs, including the driver
            // settings session. VibranceController happens to call this too, but relying
            // on that is why every Apply() was silently returning false: without it the
            // DRS session throws immediately and the toggle reports "didn't accept".
            try { NVIDIA.Initialize(); } catch { /* already initialised, or no driver */ }
            Tier = DetectTier();
        }

        private static GpuTier DetectTier()
        {
            try
            {
                var gpu = PhysicalGPU.GetPhysicalGPUs().FirstOrDefault();
                return gpu == null ? GpuTier.None : GpuCapability.FromName(gpu.FullName);
            }
            catch
            {
                return GpuTier.None; // no NVIDIA driver at all
            }
        }

        public bool Apply(string tweakId, bool on, int fpsCap)
        {
            if (Tier == GpuTier.None) return false;

            try
            {
                using var session = DriverSettingsSession.CreateAndLoad();
                var profile = FindOrCreateProfile(session);
                if (profile == null) return false;

                foreach (var (id, value) in ValuesFor(tweakId, on, fpsCap))
                    profile.SetSetting(id, value);

                session.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static DriverSettingsProfile? FindOrCreateProfile(DriverSettingsSession session)
        {
            var existing = session.FindProfileByName(ProfileName);
            if (existing != null) return existing;

            var created = DriverSettingsProfile.CreateProfile(session, ProfileName);
            // Bind the profile to Rust's executable, or the settings apply to nothing.
            ProfileApplication.CreateApplication(created, RustExe);
            return created;
        }

        /// <summary>
        /// The driver values behind each toggle. "Off" writes the driver's own stock value
        /// back rather than deleting the setting, so reverting is explicit and predictable.
        /// </summary>
        private static (KnownSettingId, uint)[] ValuesFor(string tweakId, bool on, int fpsCap) =>
            tweakId switch
            {
                // 1 = Prefer Maximum Performance, 0 = Adaptive (driver default)
                "power-max" => new[] { (KnownSettingId.D3DOpenGLGPUMaximumPower, on ? 1u : 0u) },

                // Pre-rendered frames: 1 = shortest queue, 0 = let the app decide
                "low-latency" => new[] { (KnownSettingId.PreRenderLimit, on ? 1u : 0u) },

                // Texture filtering quality: 0x10 = High Performance, 0x14 = Quality
                "texture-perf" => new[] { (KnownSettingId.QualityEnhancements, on ? 0x10u : 0x14u) },

                // V-Sync: 0 = force off, 1 = application-controlled
                "vsync-off" => new[] { (KnownSettingId.VSyncMode, on ? 0u : 1u) },

                // Frame limiter, in frames per second. 0 disables the cap.
                "fps-cap" => new[]
                {
                    (KnownSettingId.PerformanceStateFrameRateLimiter,
                     on ? (uint)Math.Clamp(fpsCap, 30, 480) : 0u)
                },

                _ => Array.Empty<(KnownSettingId, uint)>()
            };
    }

    /// <summary>Stand-in when this PC has no NVIDIA GPU - the card hides itself.</summary>
    public sealed class NullNvidiaDriverSettings : INvidiaDriverSettings
    {
        public GpuTier Tier => GpuTier.None;
        public bool Apply(string tweakId, bool on, int fpsCap) => false;
    }
}
