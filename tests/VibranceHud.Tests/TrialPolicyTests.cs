using System;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class TrialPolicyTests
    {
        private static readonly DateTime Start = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact] public void TrialIsFourDays() => Assert.Equal(TimeSpan.FromDays(4), TrialPolicy.Length);

        [Fact] public void FreshTrialIsNotExpired() => Assert.False(TrialPolicy.IsExpired(Start, Start));

        [Fact] public void TrialIsLiveOnDayThree() =>
            Assert.False(TrialPolicy.IsExpired(Start, Start.AddDays(3)));

        [Fact] public void TrialEndsAtExactlyFourDays() =>
            Assert.True(TrialPolicy.IsExpired(Start, Start.AddDays(4)));

        [Fact] public void TrialIsExpiredAfterFourDays() =>
            Assert.True(TrialPolicy.IsExpired(Start, Start.AddDays(4).AddSeconds(1)));

        /// <summary>Winding the system clock back must not be a one-click trial reset.</summary>
        [Fact]
        public void ClockMovedBackwardsDoesNotExtendTheTrial()
        {
            Assert.Equal(TimeSpan.Zero, TrialPolicy.Remaining(Start, Start.AddDays(-10)));
            Assert.True(TrialPolicy.IsExpired(Start, Start.AddDays(-10)));
        }

        [Fact]
        public void RemainingCountsDown()
        {
            Assert.Equal(TimeSpan.FromDays(4), TrialPolicy.Remaining(Start, Start));
            Assert.Equal(TimeSpan.FromDays(1), TrialPolicy.Remaining(Start, Start.AddDays(3)));
        }

        [Fact] public void RemainingNeverGoesNegative() =>
            Assert.Equal(TimeSpan.Zero, TrialPolicy.Remaining(Start, Start.AddDays(99)));
    }
}
