using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Streaming Mode: make the whole effect visible to capture.
    ///
    /// The colour matrix is applied while the desktop is composited, so OBS Display Capture
    /// sees it. Driver vibrance and the gamma ramp are applied after that, on the way out to
    /// the cable, so nothing can ever capture them. That's why the effect shows up for some
    /// people and not others - it depends which slider they used.
    ///
    /// Streaming Mode moves everything into the matrix.
    /// </summary>
    public sealed class StreamingModeTests
    {
        private sealed class RecordingController : IVibranceController
        {
            public int LastSet = -1;
            public int CurrentLevel { get; set; }
            public int DefaultLevel => 50;
            public bool IsAvailable => true;
            public void SetLevel(int level) { LastSet = level; CurrentLevel = level; }
        }

        private sealed class SilentOverlay : ISaturationOverlay
        {
            public void Apply(float[] matrix) { }
            public void Clear() { }
        }

        private sealed class SilentGamma : IGammaRamp
        {
            public void Apply(ushort[] ramp) { }
            public void Reset() { }
        }

        private static VibranceEngine Engine(out RecordingController controller)
        {
            controller = new RecordingController();
            return new VibranceEngine(controller, new SilentOverlay(), new SilentGamma());
        }

        // ---- the factor -------------------------------------------------------------------

        [Fact]
        public void Normally_the_driver_owns_the_first_hundred_and_software_stays_out_of_it()
        {
            // Existing behaviour, pinned so Streaming Mode can't quietly change it.
            Assert.Equal(1f, VibranceEngine.SoftwareVibranceFactor(
                70, driverAvailable: true, streaming: false), 3);
        }

        [Fact]
        public void In_streaming_mode_software_carries_the_whole_range()
        {
            // The driver's contribution is invisible to capture, so software has to do all of
            // it - even on a machine where the driver works perfectly.
            Assert.Equal(0.7f, VibranceEngine.SoftwareVibranceFactor(
                70, driverAvailable: true, streaming: true), 3);
        }

        [Fact]
        public void Streaming_mode_changes_nothing_for_a_pc_that_had_no_driver_anyway()
        {
            Assert.Equal(
                VibranceEngine.SoftwareVibranceFactor(70, driverAvailable: false, streaming: false),
                VibranceEngine.SoftwareVibranceFactor(70, driverAvailable: false, streaming: true), 3);
        }

        // ---- the trap ---------------------------------------------------------------------

        [Fact]
        public void Streaming_mode_parks_the_driver_at_neutral_not_at_zero()
        {
            // The one that bites. Driver vibrance 0 is not "off" - it is fully grey. Handing
            // the driver a 0 while software does the work would drain every colour from the
            // screen and look like the app had broken.
            var engine = Engine(out var controller);
            engine.Vibrance = 150;

            engine.StreamingMode = true;

            Assert.Equal(controller.DefaultLevel, controller.LastSet);
            Assert.NotEqual(0, controller.LastSet);
        }

        [Fact]
        public void Turning_streaming_mode_off_gives_the_driver_its_value_back()
        {
            var engine = Engine(out var controller);
            engine.Vibrance = 80;
            engine.StreamingMode = true;

            engine.StreamingMode = false;

            Assert.Equal(80, controller.LastSet);
        }

        [Fact]
        public void Changing_vibrance_while_streaming_keeps_the_driver_neutral()
        {
            // Otherwise the next slider move quietly re-arms the invisible path.
            var engine = Engine(out var controller);
            engine.StreamingMode = true;

            engine.Vibrance = 120;

            Assert.Equal(controller.DefaultLevel, controller.LastSet);
        }

        [Fact]
        public void It_starts_off()
        {
            // Streaming Mode trades image quality for capture visibility. Nobody who isn't
            // recording should be paying that without asking for it.
            Assert.False(Engine(out _).StreamingMode);
        }
    }
}
