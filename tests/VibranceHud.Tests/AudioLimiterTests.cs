using VibranceHud.Audio;
using Xunit;

namespace VibranceHud.Tests
{
    public class AudioLimiterTests
    {
        private const float Threshold = 0.30f; // the user's "30%" ceiling
        private const float Max = 1.0f;

        [Fact]
        public void LoudPeak_DucksTheVolumeDown()
        {
            // A gun shot: peaking way over the ceiling.
            var next = AudioLimiter.NextVolume(peak: 0.95f, Threshold, currentVolume: 1.0f, Max);

            Assert.True(next < 1.0f);
        }

        [Fact]
        public void QuietPeak_LetsTheVolumeComeBackUp()
        {
            // Footsteps: well under the ceiling, so we recover toward max.
            var next = AudioLimiter.NextVolume(peak: 0.05f, Threshold, currentVolume: 0.4f, Max);

            Assert.True(next > 0.4f);
        }

        [Fact]
        public void Attack_IsFasterThanRelease()
        {
            // Ducking a spike should move further in one tick than recovering does.
            float duckDelta = 1.0f - AudioLimiter.NextVolume(0.95f, Threshold, 1.0f, Max);
            float riseDelta = AudioLimiter.NextVolume(0.01f, Threshold, 0.5f, Max) - 0.5f;

            Assert.True(duckDelta > riseDelta);
        }

        [Fact]
        public void Volume_NeverExceedsMax()
        {
            var next = AudioLimiter.NextVolume(peak: 0f, Threshold, currentVolume: 1.0f, maxVolume: 1.0f);

            Assert.True(next <= 1.0f);
        }

        [Fact]
        public void Volume_NeverDropsBelowTheFloor()
        {
            // Even a brutal peak shouldn't mute the game entirely.
            var next = AudioLimiter.NextVolume(peak: 1.0f, threshold: 0.01f, currentVolume: 0.06f, maxVolume: Max);

            Assert.True(next >= AudioLimiter.MinVolume);
        }

        [Fact]
        public void Silence_DoesNotDivideByZero_AndRecovers()
        {
            var next = AudioLimiter.NextVolume(peak: 0f, Threshold, currentVolume: 0.3f, maxVolume: Max);

            Assert.True(float.IsFinite(next));
            Assert.True(next > 0.3f);
        }

        [Fact]
        public void SustainedLoudness_SettlesTowardTheCeiling()
        {
            // Hold a loud peak for a while; the volume should converge so output ~= threshold.
            float volume = 1.0f;
            for (int i = 0; i < 50; i++)
                volume = AudioLimiter.NextVolume(peak: 0.9f, Threshold, volume, Max);

            // 0.9 * volume should land near the 0.30 ceiling.
            Assert.InRange(0.9f * volume, 0.25f, 0.35f);
        }

        [Fact]
        public void AlreadyAtMax_AndQuiet_StaysAtMax()
        {
            var next = AudioLimiter.NextVolume(peak: 0.02f, Threshold, currentVolume: 1.0f, maxVolume: 1.0f);

            Assert.Equal(1.0f, next, 3);
        }
    }
}
