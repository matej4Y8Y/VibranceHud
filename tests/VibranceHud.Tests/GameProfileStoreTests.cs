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
    }
}