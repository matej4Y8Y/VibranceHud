using VibranceHud.Audio;
using Xunit;

namespace VibranceHud.Tests
{
    public class AudioEdgeServiceTests
    {
        private sealed class FakeOutput : IAudioOutput
        {
            public float PeakValue;
            public float Volume { get; set; } = 1.0f;
            public float Peak => PeakValue;
        }

        [Fact]
        public void Tick_DucksTheVolume_WhenTheOutputIsTooLoud()
        {
            var output = new FakeOutput { PeakValue = 0.95f, Volume = 1.0f };
            var service = new AudioEdgeService(output) { Threshold = 0.30f };
            service.Start();

            service.Tick();

            Assert.True(output.Volume < 1.0f);
            service.Stop();
        }

        [Fact]
        public void Stop_AlwaysRestoresTheOriginalVolume()
        {
            // The user was at 80% before turning Audio Edge on.
            var output = new FakeOutput { Volume = 0.8f, PeakValue = 0.99f };
            var service = new AudioEdgeService(output) { Threshold = 0.20f };
            service.Start();

            for (int i = 0; i < 10; i++) service.Tick(); // duck it right down
            Assert.True(output.Volume < 0.8f);

            service.Stop();

            Assert.Equal(0.8f, output.Volume, 3);
        }

        [Fact]
        public void TheCeiling_IsNeverLouderThanTheUsersOwnVolume()
        {
            var output = new FakeOutput { Volume = 0.5f, PeakValue = 0f };
            var service = new AudioEdgeService(output);
            service.Start();

            for (int i = 0; i < 100; i++) service.Tick(); // silence: recover as far as allowed

            Assert.True(output.Volume <= 0.5f);
            service.Stop();
        }

        [Fact]
        public void Start_IsIdempotent_AndDoesNotLoseTheRestorePoint()
        {
            var output = new FakeOutput { Volume = 0.7f, PeakValue = 0.9f };
            var service = new AudioEdgeService(output);

            service.Start();
            service.Tick();          // volume is now ducked
            service.Start();         // a second Start must not capture the ducked value

            Assert.Equal(0.7f, service.RestoreVolume, 3);
            service.Stop();
            Assert.Equal(0.7f, output.Volume, 3);
        }

        [Fact]
        public void IsRunning_ReflectsStartAndStop()
        {
            var service = new AudioEdgeService(new FakeOutput());

            Assert.False(service.IsRunning);
            service.Start();
            Assert.True(service.IsRunning);
            service.Stop();
            Assert.False(service.IsRunning);
        }
    }
}
