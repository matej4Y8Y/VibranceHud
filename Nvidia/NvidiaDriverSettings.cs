using System;
using System.Linq;
using NvAPIWrapper;
using NvAPIWrapper.DRS;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native.Exceptions;
using NvAPIWrapper.Native.General;

namespace VibranceHud.Nvidia
{
    /// <summary>
    /// Outcome of trying to apply one NVIDIA driver setting. The wrapper used to collapse
    /// every failure into a boolean false, which made "driver not installed", "setting id
    /// isn't known to this driver version", and "needs admin to save the profile" all
    /// surface as the same cryptic "Driver didn't accept this setting" message.
    /// </summary>
    public enum NvidiaApplyResult
    {
        /// <summary>The value was written and the session saved cleanly.</summary>
        Success,
        /// <summary>NVAPI refused to save the profile, most likely because writing
        /// <c>C:\ProgramData\NVIDIA Corporation\Drs\nvdrsdb0.bin</c> needs admin.
        /// The UI should offer an "Apply as admin" button that relaunches PlexusX
        /// elevated to apply this same single tweak.</summary>
        NeedsAdmin,
        /// <summary>Any other failure: unknown setting id, driver mismatch, NVAPI
        /// not initialised, etc. Surfaced to the user as a generic "didn't accept".</summary>
        Unsupported,
    }

    /// <summary>Applies driver tweaks. Injected so the UI is testable without a GPU.</summary>
    public interface INvidiaDriverSettings
    {
        GpuTier Tier { get; }
        NvidiaApplyResult Apply(string tweakId, bool on, int fpsCap);

        /// <summary>
        /// Best-effort probe: does this driver actually accept the given tweak id?
        /// Returns false (never throws) on unknown ids, missing drivers, or any
        /// NVAPI error. Used by the Scan button to filter the toggles the UI shows.
        /// </summary>
        bool IsSupported(string tweakId);
    }

    /// <summary>
    /// Writes NVIDIA's own per-application profile settings for Rust - the same values
    /// the Control Panel writes, through the documented driver-settings API. No injection
    /// and nothing game-side, so it carries no anti-cheat risk.
    ///
    /// Every call is defensive: a driver that doesn't expose a given setting, or a session
    /// that won't open, returns an error result instead of throwing. A tweak that can't be
    /// applied must never take the app down or leave the toggle lying about its state.
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

        public NvidiaApplyResult Apply(string tweakId, bool on, int fpsCap)
        {
            if (Tier == GpuTier.None) return NvidiaApplyResult.Unsupported;

            try
            {
                using var session = DriverSettingsSession.CreateAndLoad();
                var profile = FindOrCreateProfile(session);
                if (profile == null) return NvidiaApplyResult.Unsupported;

                foreach (var (id, value) in ValuesFor(tweakId, on, fpsCap))
                    profile.SetSetting(id, value);

                session.Save();
                return NvidiaApplyResult.Success;
            }
            catch (NVIDIAApiException ex) when (IsPermissionStatus(ex.Status))
            {
                // The per-user DRS profile file under ProgramData\NVIDIA Corporation\Drs
                // is locked behind admin on most installs. NVAPI raises several different
                // status codes that all map to the same root cause (insufficient privilege
                // to write that file), so catch the whole family rather than guessing the
                // right status name. With this the UI can offer the elevated-helper path
                // instead of the cryptic "driver didn't accept" message.
                return NvidiaApplyResult.NeedsAdmin;
            }
            catch (NVIDIANotSupportedException)
            {
                // The current driver build doesn't recognise this KnownSettingId. The
                // Scan probe should normally catch this first, but a few mid-life driver
                // updates change id meanings, so the runtime check has to agree.
                return NvidiaApplyResult.Unsupported;
            }
            catch
            {
                return NvidiaApplyResult.Unsupported;
            }
        }

                /// <summary>True when the NVAPI failure was caused by the running process not
                /// having write access to the per-user DRS file (under ProgramData). NVAPI
                /// reports this with several different status codes depending on driver version
                /// and which internal call returned first, so the truth-set is wider than just
                /// AccessDenied.</summary>
                private static bool IsPermissionStatus(Status s) =>
                    s == Status.AccessDenied ||
                    s == Status.InvalidUserPrivilege ||
                    s == Status.SetNotAllowed ||
                    s == Status.ProfileRemoved ||
                    s == Status.RequestUserToDisableDWM;

        private static DriverSettingsProfile? FindOrCreateProfile(DriverSettingsSession session)
        {
            var existing = session.FindProfileByName(ProfileName);
            if (existing != null) return existing;

            var created = DriverSettingsProfile.CreateProfile(session, ProfileName);
            // Bind the profile to Rust's executable, or the settings apply to nothing.
            ProfileApplication.CreateApplication(created, RustExe);
            return created;
        }

        /// <summary>The driver values behind each toggle. "Off" writes the driver's own stock value
        /// back rather than deleting the setting, so reverting is explicit and predictable.</summary>
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

        /// <summary>
        /// Probes whether the driver accepts the given tweak id. Goes through the same
        /// NVAPI session code path as <see cref="Apply"/> but only reads - on a freshly
        /// installed driver some KnownSettingIds throw "setting not supported" on the
        /// existing profile, which is exactly the signal Scan needs to hide the toggle.
        /// Never throws: returns false on any exception so the Scan button stays safe
        /// even when NVAPI is in a bad state.
        /// </summary>
        public bool IsSupported(string tweakId)
        {
            if (Tier == GpuTier.None) return false;
            if (string.IsNullOrEmpty(tweakId)) return false;

            // Some tweaks map to multiple settings (none today, but a vector for the
            // future). The tweak is only "supported" if every backing setting is
            // readable - otherwise the driver won't accept the full write either.
            var ids = SettingIdsFor(tweakId);
            if (ids.Length == 0) return false;

            try
            {
                using var session = DriverSettingsSession.CreateAndLoad();
                var profile = FindOrCreateProfile(session);
                if (profile == null) return false;

                foreach (var (_, id) in ids)
                {
                    // GetSetting returns null when the driver reports "unknown
                    // setting id" against this version. That is the signal to hide.
                    if (profile.GetSetting(id) == null) return false;
                }
                return true;
            }
            catch
            {
                // NVAPI is never authoritative for the UI - it can be missing,
                // mismatched, or simply busy. False keeps the toggle hidden, which
                // is the conservative outcome the Scan flow is designed for.
                return false;
            }
        }

        /// <summary>The driver setting ids a tweak touches. Kept separate from
        /// <see cref="ValuesFor"/> so <see cref="IsSupported"/> doesn't have to fabricate
        /// a placeholder value to extract the ids back out.</summary>
        private static (KnownSettingId, uint)[] SettingIdsFor(string tweakId) =>
            tweakId switch
            {
                "power-max" => new[] { (KnownSettingId.D3DOpenGLGPUMaximumPower, 0u) },
                "low-latency" => new[] { (KnownSettingId.PreRenderLimit, 0u) },
                "texture-perf" => new[] { (KnownSettingId.QualityEnhancements, 0u) },
                "vsync-off" => new[] { (KnownSettingId.VSyncMode, 0u) },
                "fps-cap" => new[]
                {
                    (KnownSettingId.PerformanceStateFrameRateLimiter, 0u)
                },
                _ => Array.Empty<(KnownSettingId, uint)>()
            };

        /// <summary>
        /// Headless entry point used by the elevated relaunch. Replays the same
        /// write the un-elevated <see cref="Apply"/> would do, but with the UAC
        /// grant on it; the success result here is the only signal the outer
        /// process gets back. Same defensive shape so a settings id that the
        /// driver version doesn't recognise still resolves to Unsupported rather
        /// than throwing.
        /// </summary>
        public static NvidiaApplyResult ApplyHeadless(string tweakId, bool on, int fpsCap)
        {
            try
            {
                try { NVIDIA.Initialize(); } catch { /* already initialised */ }

                using var session = DriverSettingsSession.CreateAndLoad();
                var profile = FindOrCreateProfile(session);
                if (profile == null) return NvidiaApplyResult.Unsupported;

            foreach (var (id, value) in ValuesFor(tweakId, on, fpsCap))
                profile.SetSetting(id, value);

            session.Save();
            return NvidiaApplyResult.Success;
        }
        catch (NVIDIAApiException ex) when (IsPermissionStatus(ex.Status))
                    {
                        // If this fires while we're already elevated, something else is wrong
                        // (file locked, profile corrupt) - but the "needs admin" status is the
                        // best signal the caller has to show the user.
                        return NvidiaApplyResult.NeedsAdmin;
                    }
        catch
        {
            return NvidiaApplyResult.Unsupported;
        }
        }
    }

    /// <summary>Stand-in when this PC has no NVIDIA GPU - the card hides itself.</summary>
    public sealed class NullNvidiaDriverSettings : INvidiaDriverSettings
    {
        public GpuTier Tier => GpuTier.None;
        public NvidiaApplyResult Apply(string tweakId, bool on, int fpsCap) => NvidiaApplyResult.Unsupported;
        // No NVIDIA card means no driver to ask. Always false, never throws - the Scan
        // button must be safe on machines where the rest of the card is hidden.
        public bool IsSupported(string tweakId) => false;
    }
}
