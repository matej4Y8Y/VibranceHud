// The app should tidy up after itself instead of telling users to go into %TEMP%.
//
// Old installers were left behind forever: the recovery scan only ever looked for versions
// NEWER than the running one, so anything older was ignored rather than removed. Each one is
// ~64MB, users accumulated several, and one of them was what silently downgraded people
// before the version guard existed.
//
// Newer files are deliberately left alone - one of those may be a legitimate pending update
// waiting to install on the next launch.

using System;
using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class InstallerCleanupTests : IDisposable
    {
        private readonly string _dir;

        public InstallerCleanupTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "plexusx-cleanup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        /// <summary>Writes a file with a valid PE header so it looks like a real installer.</summary>
        private string MakeInstaller(string version)
        {
            var path = Path.Combine(_dir, $"PlexusX-Setup-{version}.exe");
            File.WriteAllBytes(path, new byte[] { 0x4D, 0x5A, 0x90, 0x00 }); // "MZ"
            return path;
        }

        [Fact]
        public void OlderInstaller_IsDeleted()
        {
            var old = MakeInstaller("0.9.4");

            UpdateService.CleanupObsoleteInstallers(_dir, new Version(0, 9, 8));

            Assert.False(File.Exists(old), "a superseded installer should not be left on disk");
        }

        /// <summary>Reinstalling the version already running achieves nothing, so that file is
        /// dead weight too.</summary>
        [Fact]
        public void InstallerMatchingCurrentVersion_IsDeleted()
        {
            var same = MakeInstaller("0.9.8");

            UpdateService.CleanupObsoleteInstallers(_dir, new Version(0, 9, 8));

            Assert.False(File.Exists(same));
        }

        /// <summary>A newer one may be a pending update queued for the next launch - deleting it
        /// would break updating entirely.</summary>
        [Fact]
        public void NewerInstaller_IsKept()
        {
            var newer = MakeInstaller("0.9.9");

            UpdateService.CleanupObsoleteInstallers(_dir, new Version(0, 9, 8));

            Assert.True(File.Exists(newer), "a pending newer update must survive the sweep");
        }

        /// <summary>The real reported situation: several builds piled up over time.</summary>
        [Fact]
        public void SeveralOldInstallers_AreAllRemoved_KeepingOnlyTheNewer()
        {
            var a = MakeInstaller("0.9.1");
            var b = MakeInstaller("0.9.4");
            var c = MakeInstaller("0.9.7");
            var pending = MakeInstaller("1.0.0");

            UpdateService.CleanupObsoleteInstallers(_dir, new Version(0, 9, 8));

            Assert.False(File.Exists(a));
            Assert.False(File.Exists(b));
            Assert.False(File.Exists(c));
            Assert.True(File.Exists(pending));
        }

        /// <summary>Only our own files. Someone else's installer in the temp folder is none of
        /// our business.</summary>
        [Fact]
        public void UnrelatedFiles_AreNeverTouched()
        {
            var other = Path.Combine(_dir, "SomeOtherApp-Setup-1.0.exe");
            File.WriteAllBytes(other, new byte[] { 0x4D, 0x5A });
            var doc = Path.Combine(_dir, "notes.txt");
            File.WriteAllText(doc, "hello");

            UpdateService.CleanupObsoleteInstallers(_dir, new Version(0, 9, 8));

            Assert.True(File.Exists(other));
            Assert.True(File.Exists(doc));
        }

        /// <summary>A name we can't read a version out of gets left alone rather than guessed
        /// at - deleting on a guess is worse than leaving a stray file.</summary>
        [Fact]
        public void UnparseableVersionInName_IsLeftAlone()
        {
            var odd = Path.Combine(_dir, "PlexusX-Setup-beta.exe");
            File.WriteAllBytes(odd, new byte[] { 0x4D, 0x5A });

            UpdateService.CleanupObsoleteInstallers(_dir, new Version(0, 9, 8));

            Assert.True(File.Exists(odd));
        }

        /// <summary>Runs at startup, so it must never throw - a missing folder or a locked file
        /// can't be allowed to stop the app launching.</summary>
        [Fact]
        public void MissingDirectory_DoesNotThrow()
        {
            var gone = Path.Combine(_dir, "does-not-exist");
            UpdateService.CleanupObsoleteInstallers(gone, new Version(0, 9, 8));
        }

        [Fact]
        public void EmptyDirectory_DoesNotThrow()
        {
            UpdateService.CleanupObsoleteInstallers(_dir, new Version(0, 9, 8));
        }
    }
}
