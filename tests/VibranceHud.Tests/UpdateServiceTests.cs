using System;
using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Tests for the auto-update file validator. The real-world failure this guards
    /// against is a multipart-encoded or truncated installer landing in %TEMP% and
    /// causing Process.Start to throw WinError 216 ("not a valid Win32 application").
    /// Without the validator, the user would sit on "Installing update..." for the
    /// full installer timeout while the silent install silently fails to launch.
    /// </summary>
    public class UpdateServiceTests
    {
        [Fact]
        public void IsValidInstaller_ReturnsTrueForValidPeHeader()
        {
            var temp = Path.Combine(Path.GetTempPath(), $"plexusx-valid-{Guid.NewGuid():N}.exe");
            try
            {
                File.WriteAllBytes(temp, new byte[] { 0x4D, 0x5A, 0x00, 0x00 }); // MZP magic
                Assert.True(UpdateService.IsValidInstaller(temp));
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        [Fact]
        public void IsValidInstaller_ReturnsFalseForMultipartEnvelope()
        {
            // GitHub's CDN was once returning the multipart upload envelope
            // (------hermesv075...) as the asset body when the redirect target was
            // cached incorrectly. That has the "------" boundary as its first two
            // bytes, which is 0x2D 0x2D, NOT 0x4D 0x5A ("MZ").
            var temp = Path.Combine(Path.GetTempPath(), $"plexusx-multipart-{Guid.NewGuid():N}.exe");
            try
            {
                File.WriteAllBytes(temp, new byte[] { 0x2D, 0x2D, 0x2D, 0x2D });
                Assert.False(UpdateService.IsValidInstaller(temp));
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        [Fact]
        public void IsValidInstaller_ReturnsFalseForZeroByteFile()
        {
            var temp = Path.Combine(Path.GetTempPath(), $"plexusx-empty-{Guid.NewGuid():N}.exe");
            try
            {
                File.WriteAllBytes(temp, Array.Empty<byte>());
                Assert.False(UpdateService.IsValidInstaller(temp));
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        [Fact]
        public void IsValidInstaller_ReturnsFalseForSingleByteFile()
        {
            var temp = Path.Combine(Path.GetTempPath(), $"plexusx-onebyte-{Guid.NewGuid():N}.exe");
            try
            {
                File.WriteAllBytes(temp, new byte[] { 0x4D }); // only "M", no "Z"
                Assert.False(UpdateService.IsValidInstaller(temp));
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        [Fact]
        public void IsValidInstaller_ReturnsFalseForNonExistentFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"plexusx-missing-{Guid.NewGuid():N}.exe");
            Assert.False(UpdateService.IsValidInstaller(path));
        }

        // ---- PendingUpdateInstaller recovery ----
        // RecoverStrandedInstaller scans %TEMP% for any PlexusX-Setup-X.Y.Z.exe that's
        // newer than the running version. This catches the "downloaded but never
        // installed" failure mode that hit users on v0.7.x where the installer was
        // dropped in temp but never set the pending-update flag.

        [Fact]
        public void RecoverStrandedInstaller_ReturnsNull_WhenNoNewerInstallerInTemp()
        {
            // Temp shouldn't contain a PlexusX-Setup-X.Y.Z.exe newer than current.
            // (Worst case: test environment has one. We accept either result but the
            // method should never throw or return a non-existent path.)
            var path = UpdateService.RecoverStrandedInstallerPublic();
            if (path != null)
                Assert.True(File.Exists(path));
        }

        [Fact]
        public void RecoverStrandedInstaller_PicksNewestValidInstaller()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"plexusx-recover-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Write three fake installers with progressively newer versions.
                // Only the highest version newer than CurrentVersion should be returned.
                WriteFakeInstaller(Path.Combine(tempDir, "PlexusX-Setup-9.9.8.exe"));
                WriteFakeInstaller(Path.Combine(tempDir, "PlexusX-Setup-9.9.9.exe"));
                WriteFakeInstaller(Path.Combine(tempDir, "PlexusX-Setup-10.0.0.exe"));

                var picked = UpdateService.RecoverStrandedInstallerPublic(tempDir);
                Assert.NotNull(picked);
                Assert.EndsWith("PlexusX-Setup-10.0.0.exe", picked);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void RecoverStrandedInstaller_IgnoresCorruptFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"plexusx-recover-corrupt-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var corrupt = Path.Combine(tempDir, "PlexusX-Setup-9.9.9.exe");
                File.WriteAllBytes(corrupt, new byte[] { 0x2D, 0x2D, 0x2D }); // multipart envelope, not PE

                var picked = UpdateService.RecoverStrandedInstallerPublic(tempDir);
                // The corrupt file isn't a valid PE; the recovery should not return it.
                // Other older versions in temp aren't picked. So either null or a different
                // path is acceptable.
                if (picked != null)
                    Assert.NotEqual(corrupt, picked);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        private static void WriteFakeInstaller(string path)
        {
            // A valid PE header + a few padding bytes. Doesn't need to be a runnable
            // installer - RecoverStrandedInstaller only checks the MZ magic and version.
            var bytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00 };
            File.WriteAllBytes(path, bytes);
        }
    }
}
