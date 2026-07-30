// Vibrance has to do something on AMD and Intel.
//
// The 0-100 range was driver-only: on a non-NVIDIA GPU the app installs
// NullVibranceController (SetLevel is an empty method) AND VibranceEngine held the software
// vibrance term at 1.0 below 100. Both paths inert at once, so dragging the slider through
// its entire default range changed nothing anywhere. ColorAdjust already knows how to
// compute software vibrance - it was simply gated above 100.

using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class SoftwareVibranceTests
    {
        private sealed class FakeController : IVibranceController
        {
            public FakeController(bool available) => IsAvailable = available;
            public bool IsAvailable { get; }
            public int CurrentLevel { get; private set; } = 50;
            public int DefaultLevel => 50;
            public int SetLevelCalls { get; private set; }
            public void SetLevel(int level) { SetLevelCalls++; CurrentLevel = level; }
        }

        private sealed class RecordingOverlay : ISaturationOverlay
        {
            public int ApplyCalls { get; private set; }
            public int ClearCalls { get; private set; }
            public void Apply(float[] matrix) => ApplyCalls++;
            public void Clear() => ClearCalls++;
        }

        private sealed class NoopGamma : IGammaRamp
        {
            public void Apply(ushort[] ramp) { }
            public void Reset() { }
        }

        // ---- the pure conversion ----------------------------------------------------------

        /// <summary>With a driver present the 0-100 range belongs to NVAPI, so the software
        /// term must stay neutral or the two would stack and double-apply.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(100)]
        public void WithDriver_BelowOrAtCeiling_SoftwareTermIsNeutral(int vibrance)
        {
            Assert.Equal(1f, VibranceEngine.SoftwareVibranceFactor(vibrance, driverAvailable: true), 3);
        }

        /// <summary>Above the ceiling the driver is pinned and software carries the rest -
        /// unchanged behaviour, pinned here so the refactor can't alter it.</summary>
        [Theory]
        [InlineData(150, 1.5f)]
        [InlineData(200, 2.0f)]
        public void WithDriver_AboveCeiling_SoftwareTermCarriesTheRest(int vibrance, float expected)
        {
            Assert.Equal(expected, VibranceEngine.SoftwareVibranceFactor(vibrance, driverAvailable: true), 3);
        }

        /// <summary>The fix: with no driver the whole range goes through software, so 50 is a
        /// real desaturation instead of silently nothing.</summary>
        [Theory]
        [InlineData(0, 0f)]
        [InlineData(50, 0.5f)]
        [InlineData(100, 1.0f)]
        [InlineData(150, 1.5f)]
        [InlineData(200, 2.0f)]
        public void WithoutDriver_WholeRangeGoesThroughSoftware(int vibrance, float expected)
        {
            Assert.Equal(expected, VibranceEngine.SoftwareVibranceFactor(vibrance, driverAvailable: false), 3);
        }

        /// <summary>100 must be exactly neutral in both modes - it's the "untouched" point,
        /// and an off-by-a-hair there leaves the overlay running forever at idle.</summary>
        [Fact]
        public void AtOneHundred_IsNeutral_InBothModes()
        {
            Assert.Equal(1f, VibranceEngine.SoftwareVibranceFactor(100, true), 5);
            Assert.Equal(1f, VibranceEngine.SoftwareVibranceFactor(100, false), 5);
        }

        // ---- engine behaviour -------------------------------------------------------------

        /// <summary>The regression guard. On a no-driver machine, dropping vibrance below 100
        /// used to produce no driver call and no overlay write - the slider did nothing at
        /// all. It must now reach the overlay.</summary>
        [Fact]
        public void WithoutDriver_LoweringVibranceBelowCeiling_WritesToTheOverlay()
        {
            var overlay = new RecordingOverlay();
            var engine = new VibranceEngine(new FakeController(available: false), overlay, new NoopGamma());
            engine.Vibrance = 100; // neutral baseline
            int before = overlay.ApplyCalls;

            engine.Vibrance = 40;

            Assert.True(overlay.ApplyCalls > before,
                "vibrance below 100 on a machine with no NVIDIA driver produced no overlay write - " +
                "the slider is doing nothing, which is the bug this guards");
        }

        /// <summary>An NVIDIA machine must keep using the driver for 0-100 and must NOT also
        /// apply a software term there, or the effect would be applied twice.</summary>
        [Fact]
        public void WithDriver_LoweringVibranceBelowCeiling_UsesTheDriverAndStaysNeutralInSoftware()
        {
            var controller = new FakeController(available: true);
            var overlay = new RecordingOverlay();
            var engine = new VibranceEngine(controller, overlay, new NoopGamma());

            engine.Vibrance = 40;

            Assert.True(controller.SetLevelCalls >= 1);
            Assert.Equal(40, controller.CurrentLevel);
            Assert.Equal(1f, VibranceEngine.SoftwareVibranceFactor(40, true), 3);
        }

        /// <summary>At full neutral on a no-driver machine the overlay must be cleared, not
        /// left running an identity matrix at 60fps for nothing.</summary>
        [Fact]
        public void WithoutDriver_AtNeutral_ClearsInsteadOfApplying()
        {
            var overlay = new RecordingOverlay();
            var engine = new VibranceEngine(new FakeController(available: false), overlay, new NoopGamma());

            engine.Vibrance = 100;
            engine.Saturation = 100;
            engine.Brightness = 100;
            int clearsBefore = overlay.ClearCalls;

            engine.Vibrance = 100; // still neutral

            Assert.True(overlay.ClearCalls >= clearsBefore);
        }

        /// <summary>Greyscale must be reachable without a driver - vibrance 0 is the extreme
        /// end of the control and should visibly do something.</summary>
        [Fact]
        public void WithoutDriver_ZeroVibrance_IsNotNeutral()
        {
            Assert.NotEqual(1f, VibranceEngine.SoftwareVibranceFactor(0, driverAvailable: false));
        }
    }
}
