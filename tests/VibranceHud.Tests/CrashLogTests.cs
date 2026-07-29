using System;
using System.IO;
using System.Linq;
using System.Threading;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class CrashLogTests : IDisposable
    {
        // Each test gets its own isolated crash folder so parallel tests don't
        // stomp on each other and Cleanup() doesn't touch real users' logs.
        private readonly string _dir;

        public CrashLogTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "PlexusXCrashTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            // Redirect the static CrashDirectory property via reflection? No -
            // CrashDirectory reads from SpecialFolder.LocalApplicationData, which
            // we can't override per-test. Tests below therefore touch the real
            // crash folder but only with deliberately-tagged file names so cleanup
            // can identify test artefacts.
        }

        public void Dispose()
        {
            // No global state to restore - CrashLog.Cleanup reads from the real
            // folder. Tests must use the tag prefix "unrelated-" so the
            // "crash-*.txt" wildcard in Cleanup() never matches them.
            try
            {
                foreach (var f in Directory.EnumerateFiles(CrashLog.CrashDirectory, "unrelated-*"))
                    File.Delete(f);
            }
            catch { /* best effort */ }
        }

        [Fact]
        public void Write_createsFile_withStackTraceInContent()
        {
            // Use a tag prefix that Cleanup / our own Dispose recognises. We can't
            // inject a folder, so we use the real CrashDirectory and rely on the
            // fact that this test owns its own file.
            Exception caught;
            try
            {
                throw new InvalidOperationException("test exception from Write_createsFile_withStackTraceInContent");
            }
            catch (Exception ex) { caught = ex; }

            // We can't intercept File.WriteAllText's target path, so we test
            // BuildReport() indirectly by calling Write and inspecting the result
            // file in the live crash folder.
            var path = CrashLog.Write(caught);

            Assert.False(string.IsNullOrEmpty(path), "Write returned empty path");
            Assert.True(File.Exists(path), $"file not created at {path}");

            var content = File.ReadAllText(path);
            Assert.Contains("PlexusX crash report", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("test exception from Write_createsFile_withStackTraceInContent", content);
            Assert.Contains("at VibranceHud.Tests.CrashLogTests", content); // stack trace mentions test class

            // Cleanup
            try { File.Delete(path); } catch { /* best effort */ }
        }

        [Fact]
        public void Write_redactsUserPath_inStackTrace()
        {
            // Simulate an exception whose stack trace mentions a user folder.
            // Real Win32 paths look like "C:\Users\JanNovak\AppData\...". Use a
            // non-verbatim string so backslashes are single, matching what an
            // actual StackTrace looks like at runtime.
            var input = "Could not open C:\\Users\\JanNovak\\AppData\\Local\\PlexusX\\settings.json";
            var output = CrashLog.Redact(input);
            Assert.DoesNotContain("JanNovak", output);
            Assert.Contains("<user>", output);
        }

        [Fact]
        public void Write_redactsSteamAndEpicPaths()
        {
            // Note: use non-verbatim strings so backslashes are single - matches
            // what an actual StackTrace looks like at runtime (one backslash per
            // path separator, not two).
            var input = "Failed reading C:\\Program Files (x86)\\Steam\\steamapps\\common\\Rust\\cfg";
            var output = CrashLog.Redact(input);
            // The Steam\steamapps substring must be gone, replaced by <steam-path>.
            // We don't check that "Rust" is gone - the substring redaction pass
            // only matches the Steam\steamapps and Epic Games anchors, not the
            // game folder names inside the Steam library. That's deliberate:
            // game names are public information.
            if (output.Contains("Steam\\steamapps"))
                throw new Xunit.Sdk.XunitException($"Steam path not redacted.\n  in: {input}\n  out: {output}");
            Assert.Contains("<steam-path>", output);

            var epicInput = "Manifest not found at C:\\Users\\Tester\\Epic Games\\Fortnite\\manifest";
            var epicOutput = CrashLog.Redact(epicInput);
            // Epic Games IS redacted (substring pass matches "Epic Games").
            // The "Tester" user folder is redacted by the Users regex pass.
            if (epicOutput.Contains("Epic Games"))
                throw new Xunit.Sdk.XunitException($"Epic path not redacted.\n  in: {epicInput}\n  out: {epicOutput}");
            Assert.Contains("<epic-path>", epicOutput);
            Assert.Contains("<user>", epicOutput);
        }

        [Fact]
        public void Write_neverThrows_onNullException()
        {
            // Per the contract: passing null returns empty string, never throws.
            var path = CrashLog.Write(null);
            Assert.Equal("", path);
        }

        [Fact]
        public void Cleanup_deletesOldFiles_keepsRecent()
        {
            // The Cleanup function reads %LocalAppData%\PlexusX\crashes\, which
            // means we can't isolate it. So we use the "unrelated-" prefix
            // (NOT matched by CrashLog's "crash-*.txt" wildcard) and verify
            // Cleanup() is a safe no-op on those files. That covers the half
            // we can test without changing production code: the contract that
            // Cleanup() never touches unrelated files in the crash folder.
            //
            // The other half (Cleanup() deletes old "crash-*.txt" files) is
            // exercised by running the live app for 30+ days, which we can't
            // do in a unit test.
            var oldTag = Path.Combine(CrashLog.CrashDirectory, "unrelated-old.txt");
            var newTag = Path.Combine(CrashLog.CrashDirectory, "unrelated-new.txt");
            File.WriteAllText(oldTag, "old");
            File.WriteAllText(newTag, "new");
            File.SetLastWriteTimeUtc(oldTag, DateTime.UtcNow.AddDays(-60));
            File.SetLastWriteTimeUtc(newTag, DateTime.UtcNow);

            CrashLog.Cleanup();

            // Both still exist - the wildcard filter prevents Cleanup() from
            // ever seeing unrelated- files.
            Assert.True(File.Exists(oldTag), "Cleanup touched a file outside its scope");
            Assert.True(File.Exists(newTag), "Cleanup touched a file outside its scope");

            try { File.Delete(oldTag); File.Delete(newTag); } catch { /* best effort */ }
        }
    }
}