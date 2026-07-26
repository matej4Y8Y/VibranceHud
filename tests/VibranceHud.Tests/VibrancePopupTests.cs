using System;
using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class VibrancePopupTests : IDisposable
    {
        private readonly string _dir;

        public VibrancePopupTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "VibranceHudTests_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private sealed class FakeEngine : IVibranceEngine
        {
            public int Vibrance { get; set; } = 100;
            public int Saturation { get; set; } = 100;
            public int Brightness { get; set; } = 100;
            public int Gamma { get; set; } = 100;
        }

        [Fact]
        public void SlidersStartAtTheEnginesCurrentValues()
        {
            var engine = new FakeEngine { Vibrance = 70, Saturation = 130, Brightness = 90, Gamma = 110 };
            var store = new SettingsStore(_dir);
            using var popup = new VibrancePopup(engine, new AppSettings(), store);

            Assert.Equal(70, popup.VibranceSlider.Value);
            Assert.Equal(130, popup.SaturationSlider.Value);
            Assert.Equal(90, popup.BrightnessSlider.Value);
            Assert.Equal(110, popup.GammaSlider.Value);
        }

        [Fact]
        public void DraggingASlider_UpdatesTheEngineImmediately()
        {
            var engine = new FakeEngine();
            var store = new SettingsStore(_dir);
            using var popup = new VibrancePopup(engine, new AppSettings(), store);

            popup.VibranceSlider.Value = 55;
            popup.SaturationSlider.Value = 160;
            popup.BrightnessSlider.Value = 80;
            popup.GammaSlider.Value = 120;

            Assert.Equal(55, engine.Vibrance);
            Assert.Equal(160, engine.Saturation);
            Assert.Equal(80, engine.Brightness);
            Assert.Equal(120, engine.Gamma);
        }

        [Fact]
        public void DraggingASlider_DoesNotPersistUntilSaveIsClicked()
        {
            var engine = new FakeEngine();
            var store = new SettingsStore(_dir);
            var settings = new AppSettings();
            using var popup = new VibrancePopup(engine, settings, store);

            popup.VibranceSlider.Value = 55;

            // A live slider drag must never itself write to disk - only the explicit Save
            // button does. Confirmed by nothing existing on disk yet.
            Assert.False(File.Exists(Path.Combine(_dir, "settings.json")));
        }

        [Fact]
        public void Save_WritesTheCurrentSliderValues_ToAppSettings()
        {
            var engine = new FakeEngine();
            var store = new SettingsStore(_dir);
            var settings = new AppSettings();
            using var popup = new VibrancePopup(engine, settings, store);

            popup.VibranceSlider.Value = 65;
            popup.SaturationSlider.Value = 175;
            popup.BrightnessSlider.Value = 85;
            popup.GammaSlider.Value = 115;

            popup.Save();

            var reloaded = store.Load();
            Assert.Equal(65, reloaded.VibrancePercent);
            Assert.Equal(175, reloaded.SaturationPercent);
            Assert.Equal(85, reloaded.BrightnessPercent);
            Assert.Equal(115, reloaded.GammaPercent);
        }

        [Fact]
        public void ThePopupNeverReferencesTheAutoApplyPath()
        {
            // Structural guard for "must not trigger auto-apply": VibrancePopup should have
            // no dependency at all on the game-profile auto-apply machinery, so a manual
            // tweak here can never register as, or be silently overwritten by, a game
            // profile. If this ever needs to change, that's a sign the constraint broke.
            var asm = typeof(VibrancePopup).Assembly;
            var popupType = typeof(VibrancePopup);

            foreach (var field in popupType.GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
            {
                Assert.DoesNotContain("ProfileApplyEngine", field.FieldType.FullName ?? "");
                Assert.DoesNotContain("ProfileEngineCoordinator", field.FieldType.FullName ?? "");
                Assert.DoesNotContain("GameProfileStore", field.FieldType.FullName ?? "");
            }
        }
    }
}
