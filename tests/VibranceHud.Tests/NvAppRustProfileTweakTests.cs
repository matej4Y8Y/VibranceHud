using System;
using System.IO;
using System.Text.Json;
using VibranceHud.Nvidia;
using VibranceHud.SystemTweaks;
using Xunit;

namespace VibranceHud.Tests
{
    public class NvAppRustProfileTweakTests : IDisposable
    {
        private readonly string _baseDir;
        private readonly NvAppRustProfileTweak _tweak;
        // A stand-in LocalId so we don't depend on the real Rust LocalId; the
        // tweak's lookup-by-name logic is exercised separately by ResolveRustIdTests.
        private const string TestLocalId = "999999999";

        public NvAppRustProfileTweakTests()
        {
            // Each test gets its own scratch directory under %TEMP% so tests
            // are isolated from each other and from the real NVIDIA App state.
            _baseDir = Path.Combine(Path.GetTempPath(),
                "plexusx-nvapp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_baseDir);
            _tweak = new NvAppRustProfileTweak(_baseDir, TestLocalId);
        }

        public void Dispose()
        {
            try { Directory.Delete(_baseDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        private string ProfilePath() => Path.Combine(_baseDir, TestLocalId + ".json");

        private static NvAppRustProfile ReadRaw(string path)
        {
            using var s = File.OpenRead(path);
            return JsonSerializer.Deserialize<NvAppRustProfile>(s)!;
        }

        [Fact]
        public void Tweak_Meets_ISystemTweak_Contract()
        {
            // The catalog filters by tier, labels etc., so the tweak must
            // expose well-formed metadata that doesn't break the FpsTweaksPage.
            Assert.Equal("nvapp-rust-potato", _tweak.Id);
            Assert.Equal("Potato (NVIDIA Experience)", _tweak.Label);
            Assert.Equal("NVIDIA", _tweak.Category);
            Assert.Equal(TweakTier.Safe, _tweak.Tier);
            Assert.False(_tweak.RequiresAdmin);
            Assert.False(string.IsNullOrWhiteSpace(_tweak.Description));
        }

        [Fact]
        public void IsApplied_False_WhenFileMissing()
        {
            // Stock install: NVIDIA App has never written a Rust profile.
            Assert.False(_tweak.IsApplied());
        }

        [Fact]
        public void IsApplied_True_WhenTargetPowerModeIsPotato()
        {
            // The Target fields are what NVIDIA Experience will apply next launch.
            File.WriteAllText(ProfilePath(),
                "{\"EverChangedByGFE\":true,\"CurrentPowerMode\":1,\"CurrentDCState\":1," +
                "\"TargetPowerMode\":0,\"TargetDCState\":0}");

            Assert.True(_tweak.IsApplied());
        }

        [Fact]
        public void IsApplied_False_WhenTargetPowerModeIsNotPotato()
        {
            // Higher quality selected - not the optimised state.
            File.WriteAllText(ProfilePath(),
                "{\"EverChangedByGFE\":true,\"CurrentPowerMode\":1,\"CurrentDCState\":1," +
                "\"TargetPowerMode\":1,\"TargetDCState\":1}");

            Assert.False(_tweak.IsApplied());
        }

        [Fact]
        public void Apply_CreatesFile_WithPotatoValues()
        {
            var result = _tweak.Apply();

            Assert.True(result.Ok);
            Assert.Equal("NVIDIA Experience set to Potato", result.StatusText);
            Assert.True(_tweak.IsApplied());

            var written = ReadRaw(ProfilePath());
            Assert.True(written.EverChangedByGFE);
            Assert.Equal(0, written.CurrentPowerMode);
            Assert.Equal(0, written.CurrentDCState);
            Assert.Equal(0, written.TargetPowerMode);
            Assert.Equal(0, written.TargetDCState);
        }

        [Fact]
        public void Apply_PreservesEverChangedByGFE_WhenOverwriting()
        {
            // If NVIDIA App already wrote the file with EverChangedByGFE=true
            // and a previous PowerMode value, we should still flip the PowerMode
            // fields but not surprise the file with an EverChangedByGFE change.
            File.WriteAllText(ProfilePath(),
                "{\"EverChangedByGFE\":true,\"CurrentPowerMode\":2,\"CurrentDCState\":2," +
                "\"TargetPowerMode\":2,\"TargetDCState\":2}");

            _tweak.Apply();

            var written = ReadRaw(ProfilePath());
            Assert.True(written.EverChangedByGFE); // stays true
            Assert.Equal(0, written.TargetPowerMode);
        }

        [Fact]
        public void Apply_IsIdempotent()
        {
            _tweak.Apply();
            _tweak.Apply();
            Assert.True(_tweak.IsApplied());
            var written = ReadRaw(ProfilePath());
            Assert.Equal(0, written.TargetPowerMode);
        }

        [Fact]
        public void Revert_WritesDefaultPreset()
        {
            _tweak.Apply();
            Assert.True(_tweak.IsApplied());

            _tweak.Revert();

            Assert.False(_tweak.IsApplied());
            var written = ReadRaw(ProfilePath());
            Assert.Equal(1, written.TargetPowerMode);
            Assert.Equal(1, written.TargetDCState);
        }

        [Fact]
        public void Revert_IsNoOp_WhenFileMissing()
        {
            // If NVIDIA never wrote a profile, Revert shouldn't create one.
            _tweak.Revert();
            Assert.False(File.Exists(ProfilePath()));
        }

        [Fact]
        public void ApplyThenRevert_LeavesTweakOff()
        {
            _tweak.Apply();
            Assert.True(_tweak.IsApplied());

            _tweak.Revert();
            Assert.False(_tweak.IsApplied());
        }

        [Fact]
        public void AtomicWrite_DoesNotLeaveTempFileBehind()
        {
            _tweak.Apply();
            Assert.False(File.Exists(ProfilePath() + ".tmp"));
            Assert.True(File.Exists(ProfilePath()));
        }

        [Fact]
        public void CorruptedFile_DoesNotThrow_IsAppliedReturnsFalse()
        {
            File.WriteAllText(ProfilePath(), "{not json");
            // Reading a corrupt profile must not crash the UI on every poll;
            // the safest outcome is "not applied" until the user re-applies.
            Assert.False(_tweak.IsApplied());
        }
    }

    public class NvAppRustProfileLookupTests : IDisposable
    {
        private readonly string _baseDir;
        private const string LookupLocalId = "123456789";

        public NvAppRustProfileLookupTests()
        {
            _baseDir = Path.Combine(Path.GetTempPath(),
                "plexusx-nvapp-lookup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_baseDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_baseDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        private string StoragePath() => Path.Combine(
            Path.GetDirectoryName(_baseDir)!, "ApplicationStorage.json");

        private NvAppRustProfileTweak TweakWithLookup() =>
            // No localIdOverride -> forces the Production-path lookup against
            // ApplicationStorage.json (resolved relative to baseDir's parent).
            new NvAppRustProfileTweak(_baseDir, localIdOverride: null);

        [Fact]
        public void Lookup_FallsBackToKnownId_WhenStorageFileMissing()
        {
            // Production tweak ctor with no ApplicationStorage.json around ->
            // path resolves to <temp>/nonexistent.json and the lookup throws
            // internally, hitting the fallback branch. Verify the file path
            // uses the known Rust LocalId.
            var tweak = TweakWithLookup();
            Assert.EndsWith(NvAppRustProfileTweak.KnownRustLocalId + ".json",
                tweak.ProfilePath());
        }

        [Fact]
        public void Lookup_FindsRustByName_InStorageJson()
        {
            // ApplicationStorage.json lives next to baseDir's parent.
            File.WriteAllText(StoragePath(),
                "{\"KnownApplications\":[" +
                "{\"Name\":\"SomeOtherGame\",\"LocalId\":\"111\"}," +
                "{\"Name\":\"Rust\",\"LocalId\":\"" + LookupLocalId + "\"}," +
                "{\"Name\":\"Counter-Strike 2\",\"LocalId\":\"222\"}" +
                "]}");

            var tweak = TweakWithLookup();
            Assert.EndsWith(LookupLocalId + ".json", tweak.ProfilePath());
        }

        [Fact]
        public void Lookup_FindsRust_UnderAlternateApplicationsKey()
        {
            // Real NVIDIA App has used both "KnownApplications" and "Applications"
            // at different versions; verify we read either.
            File.WriteAllText(StoragePath(),
                "{\"Applications\":[" +
                "{\"AppName\":\"Rust\",\"LocalId\":\"" + LookupLocalId + "\"}" +
                "]}");

            var tweak = TweakWithLookup();
            Assert.EndsWith(LookupLocalId + ".json", tweak.ProfilePath());
        }

        [Fact]
        public void Lookup_FallsBackToKnownId_WhenRustNotInStorage()
        {
            File.WriteAllText(StoragePath(),
                "{\"KnownApplications\":[{\"Name\":\"Counter-Strike 2\",\"LocalId\":\"222\"}]}");

            var tweak = TweakWithLookup();
            Assert.EndsWith(NvAppRustProfileTweak.KnownRustLocalId + ".json",
                tweak.ProfilePath());
        }

        [Fact]
        public void Lookup_FallsBackToKnownId_WhenStorageIsCorrupt()
        {
            File.WriteAllText(StoragePath(), "not-json");
            var tweak = TweakWithLookup();
            Assert.EndsWith(NvAppRustProfileTweak.KnownRustLocalId + ".json",
                tweak.ProfilePath());
        }
    }

    /// <summary>
    /// The catalog-level tests instantiate <see cref="SystemTweakCatalog"/>
    /// for every-tweak metadata checks. We must inject the test-friendly
    /// NvApp tweak so the catalog ApplyThenRevert test never touches
    /// <c>%LOCALAPPDATA%\NVIDIA Corporation\NVIDIA App\</c> on a developer's
    /// machine. This test is here to lock in that contract - if anyone removes
    /// the catalog constructor's NvApp parameter, this test catches it.
    /// </summary>
    public class NvAppTweakInCatalogTests : IDisposable
    {
        private readonly string _baseDir;
        private const string TestLocalId = "888888888";

        public NvAppTweakInCatalogTests()
        {
            _baseDir = Path.Combine(Path.GetTempPath(),
                "plexusx-nvapp-catalog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_baseDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_baseDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        [Fact]
        public void Catalog_WithInjectedTestTweak_DoesNotTouchRealAppData()
        {
            var tweak = new NvAppRustProfileTweak(_baseDir, TestLocalId);
            var catalog = new SystemTweakCatalog(new FakeRegistry(), tweak);
            var nvApp = Assert.Single(catalog.All, t => t.Id == "nvapp-rust-potato");

            // Apply + Revert inside the catalog must land in the test dir, not the
            // real %LOCALAPPDATA% path. If they leaked to the real one, the file
            // at the test path would not exist.
            nvApp.Apply();
            Assert.True(File.Exists(Path.Combine(_baseDir, TestLocalId + ".json")));
            nvApp.Revert();
        }

        private sealed class FakeRegistry : IRegistryAccess
        {
            public string? GetValue(RegistryRoot r, string s, string n) => null;
            public void SetValue(RegistryRoot r, string s, string n, string v, RegistryKind k) { }
            public void DeleteValue(RegistryRoot r, string s, string n) { }
        }
    }
}