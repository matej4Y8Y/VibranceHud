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
    }
}
