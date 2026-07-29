// Regression tests for the v0.9.0 update-pipeline rewrite.
// These pin down the bug Claude found: RecoverStrandedInstaller used to install
// ANY PlexusX-Setup-X.Y.Z.exe it found in %TEMP%, even if that file was older
// than the user's current version (or older than what GitHub currently serves).
// The friend who reported "downloaded 0.9, ended up on 0.7" hit exactly that:
// a stale 0.7.0 installer was sitting in temp, the fresh 0.9.0 download
// failed/deleted, and recovery picked up the 0.7.0 leftover.
//
// The fix lives in UpdateService.RunPendingUpdateIfAnyAsync: it queries GitHub
// releases/latest and refuses to launch an installer whose version is older than
// what's currently published. The legacy RecoverStrandedInstallerPublic is still
// used to FIND the candidate file, but only as a path resolver - the decision
// to launch it lives in RunPendingUpdateIfAnyAsync.

using System;
using System.IO;
using System.Threading.Tasks;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class UpdatePipelineRegressionTests
    {
        [Fact]
        public async Task RunPendingUpdateIfAnyAsync_DeletesAndRefuses_OlderRecoveredInstaller()
        {
            // Set up an isolated temp directory with an installer whose version is
            // intentionally older than the running version. RunPendingUpdateIfAnyAsync
            // would normally reach out to GitHub, but for this test we set the
            // PendingUpdateVersion in settings to an OLD value so the new logic path
            // that compares pending < current triggers and deletes the file.
            var tempDir = Path.Combine(Path.GetTempPath(), $"plexusx-old-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var stalePath = Path.Combine(tempDir, "PlexusX-Setup-0.5.0.exe");
                File.WriteAllBytes(stalePath, new byte[] { 0x4D, 0x5A, 0x00, 0x00 });

                var settings = new AppSettings();
                settings.PendingUpdateInstaller = stalePath;
                settings.PendingUpdateVersion = "0.5.0";

                // We can't hit GitHub in a unit test, but the synchronous pre-check
                // inside ResolvePendingInstaller already verifies the file is a valid
                // PE; the async GitHub re-check only matters when the file is a
                // NEWER version than what's running. So for an OLDER version the
                // synchronous path handles it: it deletes the file and returns false.
                //
                // The async path would still be the right entry point in production,
                // but the sync wrapper is the one we can call from a non-async test
                // without a mock. Both must reject the stale installer.
                var ranSync = UpdateService.RunPendingUpdateIfAny(settings);
                Assert.False(ranSync, "Sync wrapper must refuse to install an older version.");
                Assert.False(File.Exists(stalePath), "Stale installer must be deleted, not just skipped.");
                Assert.Equal("", settings.PendingUpdateInstaller);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void RecoverStrandedInstallerPublic_DoesNotReturn_OlderFiles()
        {
            // Sanity check that the recovery scan still respects version order: a file
            // with an older version in the filename must not be returned even if it's
            // the only PlexusX-Setup-*.exe in the directory.
            var tempDir = Path.Combine(Path.GetTempPath(), $"plexusx-older-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllBytes(Path.Combine(tempDir, "PlexusX-Setup-0.0.1.exe"),
                    new byte[] { 0x4D, 0x5A, 0x00, 0x00 });
                var picked = UpdateService.RecoverStrandedInstallerPublic(tempDir);
                // 0.0.1 < whatever CurrentVersion is, so we expect null.
                Assert.Null(picked);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void ResolvePendingInstaller_PrefersExplicitPending_OverTempScan()
        {
            // If AppSettings.PendingUpdateInstaller points at a real file, that's the
            // one we use - the temp scan is only a legacy fallback for pre-v0.8.0
            // installs that never recorded the pointer.
            var tempDir = Path.Combine(Path.GetTempPath(), $"plexusx-pref-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var explicitPath = Path.Combine(tempDir, "explicit.exe");
                File.WriteAllBytes(explicitPath, new byte[] { 0x4D, 0x5A, 0x00, 0x00 });

                var legacyPath = Path.Combine(tempDir, "PlexusX-Setup-9.9.9.exe");
                File.WriteAllBytes(legacyPath, new byte[] { 0x4D, 0x5A, 0x00, 0x00 });

                var settings = new AppSettings
                {
                    PendingUpdateInstaller = explicitPath,
                    PendingUpdateVersion = "9.9.9",
                };

                var resolved = UpdateService.ResolvePendingInstaller(settings);
                Assert.Equal(explicitPath, resolved);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
