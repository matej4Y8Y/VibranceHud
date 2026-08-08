using System;
using VibranceHud.Display;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The before/after toggle.
    ///
    /// Rate-limited on purpose. The gamma ramp misbehaves when it is written repeatedly in
    /// quick succession - rapid toggling is the known failure mode - so the control has to
    /// refuse rather than queue. Refusing is also the honest behaviour for a button somebody
    /// is mashing: a queue would keep flipping the screen after they stopped.
    /// </summary>
    public sealed class AbCompareTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; private set; } = new(2026, 8, 8, 3, 0, 0, DateTimeKind.Utc);
            public void Advance(TimeSpan by) => UtcNow += by;
        }

        private static (AbCompare Ab, FakeClock Clock) Build(int cooldownMs = 250)
        {
            var clock = new FakeClock();
            return (new AbCompare(clock, TimeSpan.FromMilliseconds(cooldownMs)), clock);
        }

        [Fact]
        public void ItStartsShowingTheUsersOwnLook()
        {
            var (ab, _) = Build();
            Assert.False(ab.ShowingNeutral);
        }

        [Fact]
        public void TogglingFlipsToNeutralAndBack()
        {
            var (ab, clock) = Build();

            Assert.True(ab.TryToggle());
            Assert.True(ab.ShowingNeutral);

            clock.Advance(TimeSpan.FromMilliseconds(300));

            Assert.True(ab.TryToggle());
            Assert.False(ab.ShowingNeutral);
        }

        [Fact]
        public void ASecondToggleInsideTheCooldownIsRefused()
        {
            var (ab, _) = Build();

            Assert.True(ab.TryToggle());
            Assert.False(ab.TryToggle());
        }

        [Fact]
        public void ARefusedToggleDoesNotChangeWhatIsShowing()
        {
            var (ab, _) = Build();

            ab.TryToggle();
            bool after = ab.ShowingNeutral;

            ab.TryToggle();

            Assert.Equal(after, ab.ShowingNeutral);
        }

        [Fact]
        public void TheCooldownExpiresRatherThanLatching()
        {
            var (ab, clock) = Build();

            ab.TryToggle();
            Assert.False(ab.TryToggle());

            clock.Advance(TimeSpan.FromMilliseconds(251));

            Assert.True(ab.TryToggle());
        }

        /// <summary>
        /// Mashing it must not leave the screen on neutral. Someone who clicks ten times fast
        /// gets one flip, and the state they end on is the one they can see.
        /// </summary>
        [Fact]
        public void MashingItProducesExactlyOneFlip()
        {
            var (ab, _) = Build();

            int accepted = 0;
            for (int i = 0; i < 10; i++)
                if (ab.TryToggle()) accepted++;

            Assert.Equal(1, accepted);
            Assert.True(ab.ShowingNeutral);
        }

        /// <summary>
        /// Leaving the page while comparing must not strand the user on neutral - their own
        /// settings would be gone with nothing on screen explaining why.
        /// </summary>
        [Fact]
        public void ResetAlwaysReturnsToTheUsersOwnLook()
        {
            var (ab, _) = Build();

            ab.TryToggle();
            Assert.True(ab.ShowingNeutral);

            ab.Reset();

            Assert.False(ab.ShowingNeutral);
        }

        /// <summary>Reset ignores the cooldown. It is not a user gesture - it is the app
        /// putting things back, and it must never be refused.</summary>
        [Fact]
        public void ResetIsNotRateLimited()
        {
            var (ab, _) = Build();

            ab.TryToggle();
            ab.Reset();

            Assert.False(ab.ShowingNeutral);
        }
    }
}
