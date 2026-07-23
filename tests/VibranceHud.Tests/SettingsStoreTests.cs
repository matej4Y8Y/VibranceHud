using System;
using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class SettingsStoreTests : IDisposable
    {
        private readonly string _dir;

        public SettingsStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "VibranceHudTests_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [Fact]
        public void Load_NoFile_ReturnsDefaults()
        {
            var store = new SettingsStore(_dir);

            var s = store.Load();

            Assert.Equal(100, s.Level);
            Assert.False(s.StartWithWindows);
            Assert.Equal(85, s.OpacityPercent);
        }

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            var store = new SettingsStore(_dir);

            store.Save(new AppSettings { Level = 175, StartWithWindows = true, OpacityPercent = 60 });
            var s = store.Load();

            Assert.Equal(175, s.Level);
            Assert.True(s.StartWithWindows);
            Assert.Equal(60, s.OpacityPercent);
        }

        [Fact]
        public void Load_CorruptFile_ReturnsDefaults()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "settings.json"), "{not valid json!!");
            var store = new SettingsStore(_dir);

            var s = store.Load();

            Assert.Equal(100, s.Level);
            Assert.Equal(85, s.OpacityPercent);
        }
    }
}
