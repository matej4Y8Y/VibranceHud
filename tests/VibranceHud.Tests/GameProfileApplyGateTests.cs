using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class GameProfileApplyGateTests
    {
        [Fact]
        public void ShouldAutoApply_DefaultsToTrue_WhenNoManualOverride()
        {
            var settings = new AppSettings();
            var gate = new GameProfileApplyGate(settings);
            Assert.True(gate.ShouldAutoApply("rust"));
        }

        [Fact]
        public void ShouldAutoApply_ReturnsFalse_WhileManualOverrideIsActive()
        {
            // Post alt-tab fix: a popup tweak sets ManualOverrideActive so the next
            // launch of the same game does NOT clobber the user's last manual tweak.
            // Verified here at the gate level (the coordinator is exercised end-to-end
            // in the manual QA pass).
            var settings = new AppSettings { ManualOverrideActive = true };
            var gate = new GameProfileApplyGate(settings);
            Assert.False(gate.ShouldAutoApply("rust"));
        }

        [Fact]
        public void ManualOverrideActive_ClearsOnShutdown_SoFreshLaunchesAlwaysStartFromSavedProfile()
        {
            // TrayApplicationContext.ExitThreadCore resets the flag (verified by
            // inspection: _settings.ManualOverrideActive = false before save). At the
            // gate level, flipping the flag off must re-enable auto-apply.
            var settings = new AppSettings { ManualOverrideActive = true };
            var gate = new GameProfileApplyGate(settings);

            Assert.False(gate.ShouldAutoApply("rust"));

            settings.ManualOverrideActive = false;

            Assert.True(gate.ShouldAutoApply("rust"));
        }
    }
}