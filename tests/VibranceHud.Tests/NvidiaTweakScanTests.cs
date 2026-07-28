using System.Collections.Generic;
using VibranceHud;
using VibranceHud.Nvidia;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Lock in the contract for the Scan button and the per-driver-settings probe
    /// it depends on. The probe must be best-effort (never throw - some users have
    /// driver versions where NVAPI exposes only a subset of KnownSettingIds), and
    /// the persisted scan result must round-trip through <see cref="SettingsStore"/>
    /// so the user doesn't have to rescan every launch.
    /// </summary>
    public class NvidiaTweakScanTests
    {
        [Fact]
        public void NullDriver_ReportsNothingSupported()
        {
            // No NVIDIA card at all - the probe must return false, never throw.
            // Without this guarantee, the Scan button could blow up on the very
            // machines the rest of the card is already correctly hiding itself on.
            var nvidia = new NullNvidiaDriverSettings();

            foreach (var t in NvidiaTweakCatalog.All)
                Assert.False(nvidia.IsSupported(t.Id));
        }

        [Fact]
        public void Probe_NeverThrows_OnUnknownTweakId()
        {
            // An id not in the catalog is the user's mistake or a future tweak id
            // the current driver doesn't know. Either way the call must not throw -
            // the card should be on the safe side and just say "not supported".
            var nvidia = new NullNvidiaDriverSettings();
            var ex = Record.Exception(() => nvidia.IsSupported("totally-not-a-tweak"));
            Assert.Null(ex);
        }

        [Fact]
        public void Settings_PersistAndReload_SupportedTweaks()
        {
            // A successful scan leaves a HashSet of supported tweak ids in
            // AppSettings; this must survive a save / load so the user doesn't
            // have to rescan every launch.
            var dir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "plexusx-scan-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SettingsStore(dir);
                var s = new AppSettings();
                s.NvAppSupportedTweaks = new HashSet<string> { "power-max", "fps-cap" };
                store.Save(s);

                var loaded = store.Load();
                Assert.Contains("power-max", loaded.NvAppSupportedTweaks);
                Assert.Contains("fps-cap", loaded.NvAppSupportedTweaks);
                Assert.DoesNotContain("vsync-off", loaded.NvAppSupportedTweaks);
            }
            finally
            {
                if (System.IO.Directory.Exists(dir))
                    System.IO.Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Settings_DefaultSupportedSet_IsEmptyAndStaysNonNull()
        {
            // Fresh install, no scan ever run. The empty-but-non-null set is the
            // UI's signal that no scan has happened yet - the card then shows
            // every tweak the tier allows. Scan is opt-in, not required.
            var s = new AppSettings();
            Assert.NotNull(s.NvAppSupportedTweaks);
            Assert.Empty(s.NvAppSupportedTweaks);
        }

        // ---- Tri-state Apply result (v0.8.1) ----
        // The previous Apply collapsed every failure into bool false, surfacing
        // "Driver didn't accept this setting" even when the actual cause was
        // "session.Save() denied because the DRS file is admin-write only".
        // These tests pin down the new tri-state contract so the UI can show a
        // helpful "Run as admin" hint instead of a cryptic error.

        [Fact]
        public void NullDriver_Apply_ReturnsUnsupported()
        {
            // No NVIDIA card = nothing the driver can accept. Treated as Unsupported
            // (the same category as "this driver version doesn't know this id"),
            // NOT as NeedsAdmin - we don't want to ask the user for elevation when
            // there's nothing the elevation could fix.
            var nvidia = new NullNvidiaDriverSettings();
            var result = nvidia.Apply("power-max", true, 0);
            Assert.Equal(NvidiaApplyResult.Unsupported, result);
        }

        [Fact]
        public void NullDriver_Apply_NeverThrows()
        {
            // Same as IsSupported: a call against the null driver must never throw.
            // The elevated helper path treats any exception as "fall through to non-
            // elevated branch", so the null driver has to behave.
            var nvidia = new NullNvidiaDriverSettings();
            var ex = Record.Exception(() => nvidia.Apply("power-max", true, 60));
            Assert.Null(ex);
        }

        [Fact]
        public void Settings_RustNvidiaTweaksNeedsAdmin_PersistsRoundTrip()
        {
            // When Apply returns NeedsAdmin, the page persists the tweak id into
            // RustNvidiaTweaksNeedsAdmin so the "Apply as admin" button stays visible
            // after a restart. Verify the round trip.
            var dir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "plexusx-needadmin-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SettingsStore(dir);
                var s = new AppSettings();
                s.RustNvidiaTweaksNeedsAdmin = new HashSet<string> { "low-latency", "vsync-off" };
                store.Save(s);

                var loaded = store.Load();
                Assert.Contains("low-latency", loaded.RustNvidiaTweaksNeedsAdmin);
                Assert.Contains("vsync-off", loaded.RustNvidiaTweaksNeedsAdmin);
                Assert.DoesNotContain("power-max", loaded.RustNvidiaTweaksNeedsAdmin);
            }
            finally
            {
                if (System.IO.Directory.Exists(dir))
                    System.IO.Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Settings_RustNvidiaTweaksNeedsAdmin_DefaultIsEmpty()
        {
            // Fresh install: the field exists but is empty, mirroring
            // RustNvidiaTweaks and NvAppSupportedTweaks.
            var s = new AppSettings();
            Assert.NotNull(s.RustNvidiaTweaksNeedsAdmin);
            Assert.Empty(s.RustNvidiaTweaksNeedsAdmin);
        }
    }
}
