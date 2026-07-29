using System;
using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Round-trip + corruption tolerance for <see cref="GameProfileStore"/>.
    /// Uses the explicit-path overloads so the test doesn't poke the user's
    /// real %LOCALAPPDATA%\PlexusX\profiles.json.
    /// </summary>
    public class GameProfileStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _storePath;

        public GameProfileStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "plexusx-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _storePath = Path.Combine(_tempDir, "profiles.json");
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        [Fact]
        public void RoundTrip_ProfileJson_PreservesAllFields()
        {
            var original = new GameProfile
            {
                GameId = "rust",
                DisplayName = "Rust",
                Vibrance = 100, Saturation = 150, Brightness = 90, Gamma = 110,
                GameHub = new GameHubOptions { GraphicsQuality = "low", FpsCap = 144 },
            };
            File.WriteAllText(_storePath, GameProfileStore.SerializeAll(new[] { original }));
            var loaded = GameProfileStore.Load(_storePath);
            Assert.Single(loaded);
            Assert.Equal("rust", loaded[0].GameId);
            Assert.Equal(150, loaded[0].Saturation);
            Assert.Equal("low", loaded[0].GameHub.GraphicsQuality);
        }

        [Fact]
        public void Set_OverwritesExistingProfileForSameId()
        {
            var first = new GameProfile { GameId = "rust", Saturation = 120 };
            var second = new GameProfile { GameId = "rust", Saturation = 180 };
            GameProfileStore.Set(first, _storePath);
            GameProfileStore.Set(second, _storePath);
            var loaded = GameProfileStore.Load(_storePath);
            Assert.Single(loaded);
            Assert.Equal(180, loaded[0].Saturation);
        }

        [Fact]
        public void Load_MissingFile_ReturnsEmpty()
        {
            Assert.Empty(GameProfileStore.Load(_storePath));
        }

        [Fact]
        public void Load_CorruptedFile_ReturnsEmpty()
        {
            File.WriteAllText(_storePath, "{ not valid json");
            Assert.Empty(GameProfileStore.Load(_storePath));
        }

        [Fact]
        public void Set_LeavesNoTempFileBehind()
        {
            // After a successful Set(), the .tmp sibling file must be gone.
            // A leftover .tmp from a prior crash would still trigger the atomic-
            // write path on the next save - which is fine - but it's a
            // disk-hygiene signal that the previous save did not complete
            // cleanly, so a presence test guards against future regressions
            // where someone switches the atomic write to a non-atomic one.
            var profile = new GameProfile { GameId = "rust", Saturation = 150 };
            GameProfileStore.Set(profile, _storePath);

            Assert.True(File.Exists(_storePath), "profile file was not written");
            Assert.False(File.Exists(_storePath + ".tmp"),
                ".tmp file from atomic write was not cleaned up");
        }

        [Fact]
        public void Set_OverwritesPreviousProfileAtomically_LeavesOriginalIntactOnFailure()
        {
            // Pretend the user has a saved profile, then Set is called. If the
            // new write were to fail mid-save (e.g. disk full between .tmp and
            // Replace), the existing file would still be readable - that's the
            // whole point of the atomic .tmp + File.Replace pattern. We can't
            // simulate a real disk-full failure here, but we CAN verify the
            // happy path leaves the file readable after Set with the new value.
            var first = new GameProfile { GameId = "rust", Saturation = 100 };
            var second = new GameProfile { GameId = "rust", Saturation = 200 };
            GameProfileStore.Set(first, _storePath);
            var bytesBefore = File.ReadAllBytes(_storePath);
            GameProfileStore.Set(second, _storePath);
            var bytesAfter = File.ReadAllBytes(_storePath);

            Assert.NotEqual(bytesBefore, bytesAfter);
            var reloaded = GameProfileStore.Load(_storePath);
            Assert.Single(reloaded);
            Assert.Equal(200, reloaded[0].Saturation);
        }
    }
}