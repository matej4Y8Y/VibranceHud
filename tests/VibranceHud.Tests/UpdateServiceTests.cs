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
    }
}
