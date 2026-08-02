using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The reason the driver is missing has to reach the screen the user is looking at. Working
    /// it out correctly and then throwing it away on the way to the UI fixes nothing.
    /// </summary>
    public sealed class VibranceDriverStatePassthroughTests
    {
        private sealed class StatedController : IVibranceController
        {
            public int CurrentLevel { get; set; }
            public int DefaultLevel { get; set; } = 50;
            public bool IsAvailable { get; set; }
            public VibranceDriverState DriverState { get; set; }
            public void SetLevel(int level) { CurrentLevel = level; }
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

        private static VibranceEngine Engine(VibranceDriverState state, bool available) =>
            new(new StatedController { DriverState = state, IsAvailable = available },
                new SilentOverlay(), new SilentGamma());

        [Fact]
        public void The_engine_reports_why_the_driver_is_missing_not_just_that_it_is()
        {
            Assert.Equal(VibranceDriverState.DisplayNotOnNvidia,
                Engine(VibranceDriverState.DisplayNotOnNvidia, available: false).DriverState);

            Assert.Equal(VibranceDriverState.NoNvidiaCard,
                Engine(VibranceDriverState.NoNvidiaCard, available: false).DriverState);
        }

        [Fact]
        public void A_laptop_user_sees_the_laptop_message_end_to_end()
        {
            // The whole chain in one assertion: state in, honest sentence out.
            var engine = Engine(VibranceDriverState.DisplayNotOnNvidia, available: false);

            var text = VibranceStatus.Readout(engine.DriverState, engine.Vibrance);

            Assert.DoesNotContain("no NVIDIA GPU", text);
        }

        [Fact]
        public void A_working_nvidia_pc_still_just_shows_its_number()
        {
            var engine = Engine(VibranceDriverState.Available, available: true);
            engine.Vibrance = 64;

            Assert.Equal("64%", VibranceStatus.Readout(engine.DriverState, engine.Vibrance));
        }
    }
}
