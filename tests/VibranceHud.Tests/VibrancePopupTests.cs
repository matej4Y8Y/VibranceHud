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
            // Drag begin/end are UI-lifecycle hooks; a fake has no state to guard.
            public void BeginDrag() { }
            public void EndDrag() { }

            // Focus-overlay hooks: no state to guard in a fake.
            public void SuspendOverlay() { }
            public void ResumeOverlay() { }

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
        public void DraggingASlider_FlipsTheManualOverrideFlag_AndAutosavesAfterDebounce()
        {
            // Post alt-tab fix: a slider drag in the popup immediately flips
            // ManualOverrideActive (so the coordinator can skip the saved profile next
            // time the same game launches) and the values are autosaved to disk via a
            // short debounce. The old behaviour was "only the Save button persists" -
            // which made closing the popup without clicking Save look like "values
            // went away" whenever anything later re-read the settings file.
            var engine = new FakeEngine();
            var store = new SettingsStore(_dir);
            var settings = new AppSettings();
            using var popup = new VibrancePopup(engine, settings, store);

            popup.SaturationSlider.Value = 160;

            // Manual override flag must flip immediately - the coordinator doesn't
            // wait on the debounce to decide whether to skip the saved profile.
            Assert.True(settings.ManualOverrideActive);

            // Wait past the autosave debounce so the timer thread writes the file.
            // 400ms is comfortably past the 250ms debounce used by the popup.
            var deadline = DateTime.UtcNow.AddMilliseconds(400);
            while (!File.Exists(Path.Combine(_dir, "settings.json")) && DateTime.UtcNow < deadline)
                System.Threading.Thread.Sleep(20);

            Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
            var reloaded = store.Load();
            Assert.Equal(160, reloaded.SaturationPercent);
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
        public void Popup_HidesWhenItLosesFocus()
        {
            var engine = new FakeEngine();
            var store = new SettingsStore(_dir);
            using var popup = new VibrancePopup(engine, new AppSettings(), store);
            popup.Show();
            System.Windows.Forms.Application.DoEvents();
            Assert.True(popup.Visible);

            typeof(VibrancePopup)
                .GetMethod("OnDeactivate", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(popup, new object[] { EventArgs.Empty });

            Assert.False(popup.Visible);
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
