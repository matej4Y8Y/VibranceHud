// Short-lived keys need expiry finer than a calendar month.
//
// Issued was stored as "yyyy-MM" and expiry computed with AddMonths, so the shortest
// possible licence was one month. A 6-hour demo key was simply not expressible. Issued is
// now a full UTC timestamp and durations are TimeSpans - but old licence files carrying the
// "yyyy-MM" form still have to keep working, or every existing install would read as
// tampered/expired after an update.

using System;
using VibranceHud.License;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class LicenseDurationTests
    {
        [Fact]
        public void TempTier_LastsSixHours()
        {
            Assert.Equal(TimeSpan.FromHours(6), LicenseService.DurationForTier("temp"));
        }

        [Theory]
        [InlineData("trial", 30)]
        [InlineData("paid", 730)]
        [InlineData("free", 365)]
        public void LongerTiers_KeepTheirDurations(string tier, int expectedDays)
        {
            Assert.Equal(TimeSpan.FromDays(expectedDays), LicenseService.DurationForTier(tier));
        }

        /// <summary>An unknown tier must not become unlimited - default to the shortest
        /// sensible window rather than granting more than intended.</summary>
        [Fact]
        public void UnknownTier_DoesNotGrantForever()
        {
            var d = LicenseService.DurationForTier("something-new");
            Assert.True(d <= TimeSpan.FromDays(365));
        }

        // ---- issue-time parsing ------------------------------------------------------------

        /// <summary>The new full-precision form round-trips to the exact instant, which is
        /// what makes a 6-hour window possible at all.</summary>
        [Fact]
        public void FullTimestamp_ParsesToTheExactInstant()
        {
            var when = new DateTime(2026, 7, 30, 14, 25, 11, DateTimeKind.Utc);
            var parsed = LicenseService.ParseIssued(LicenseService.FormatIssued(when));

            Assert.NotNull(parsed);
            Assert.Equal(when, parsed!.Value, TimeSpan.FromSeconds(1));
        }

        /// <summary>Licences written before this change carry "yyyy-MM". They must keep
        /// working, treated as issued at the start of that month.</summary>
        [Fact]
        public void LegacyYearMonth_StillParses()
        {
            var parsed = LicenseService.ParseIssued("2026-07");

            Assert.NotNull(parsed);
            Assert.Equal(2026, parsed!.Value.Year);
            Assert.Equal(7, parsed.Value.Month);
            Assert.Equal(1, parsed.Value.Day);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-date")]
        [InlineData("2026-13")]
        public void GarbageIssueDate_ReturnsNull(string issued)
        {
            Assert.Null(LicenseService.ParseIssued(issued));
        }

        // ---- expiry ------------------------------------------------------------------------

        /// <summary>A temp key issued now must still be valid a few hours in.</summary>
        [Fact]
        public void TempKey_IsStillValid_FiveHoursIn()
        {
            var issued = DateTime.UtcNow.AddHours(-5);
            Assert.False(LicenseService.IsExpiredAt("temp", issued, DateTime.UtcNow));
        }

        /// <summary>...and must be expired past six.</summary>
        [Fact]
        public void TempKey_IsExpired_AfterSixHours()
        {
            var issued = DateTime.UtcNow.AddHours(-6).AddMinutes(-1);
            Assert.True(LicenseService.IsExpiredAt("temp", issued, DateTime.UtcNow));
        }

        /// <summary>The boundary itself: at exactly six hours it's done.</summary>
        [Fact]
        public void TempKey_AtExactlySixHours_IsExpired()
        {
            var now = DateTime.UtcNow;
            Assert.True(LicenseService.IsExpiredAt("temp", now.AddHours(-6), now));
        }

        /// <summary>A freshly issued key of any tier must never read as already expired -
        /// that would lock out every user on day one.</summary>
        [Theory]
        [InlineData("temp")]
        [InlineData("trial")]
        [InlineData("paid")]
        [InlineData("free")]
        public void FreshlyIssued_IsNeverAlreadyExpired(string tier)
        {
            var now = DateTime.UtcNow;
            Assert.False(LicenseService.IsExpiredAt(tier, now, now));
        }

        /// <summary>An unparseable issue date must fail closed, not open.</summary>
        [Fact]
        public void UnparseableIssueDate_CountsAsExpired()
        {
            Assert.True(LicenseService.IsExpiredAt("paid", null, DateTime.UtcNow));
        }

        /// <summary>A legacy monthly licence must not suddenly expire because durations
        /// switched from months to days - a paid key from last month stays valid.</summary>
        [Fact]
        public void LegacyPaidLicence_FromLastMonth_IsStillValid()
        {
            var issued = LicenseService.ParseIssued(DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM"));
            Assert.NotNull(issued);
            Assert.False(LicenseService.IsExpiredAt("paid", issued, DateTime.UtcNow));
        }
    }
}
