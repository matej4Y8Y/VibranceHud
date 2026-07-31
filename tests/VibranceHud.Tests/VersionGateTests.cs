// The switch that ends the beta.
//
// A small published file names the minimum version allowed to run. While it names the beta,
// everything works. The day the full version ships, it's changed to 1.0.0 and every beta
// install locks itself on the next check - no server, no fixed date that could strand people
// if the release slips.
//
// The rules that matter here are the failure ones. Getting them wrong either locks out the
// entire userbase over a wifi blip, or lets anyone dodge the lockout by pulling their network
// cable at the right moment.

using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class VersionGateTests
    {
        private static Version V(string s) => Version.Parse(s);

        // ---- the decision ------------------------------------------------------------------

        [Fact]
        public void BelowMinimum_IsBlocked()
        {
            Assert.True(VersionGate.IsBlocked(current: V("0.9.9"), minimum: V("1.0.0")));
        }

        [Fact]
        public void AtMinimum_RunsNormally()
        {
            Assert.False(VersionGate.IsBlocked(current: V("1.0.0"), minimum: V("1.0.0")));
        }

        [Fact]
        public void AboveMinimum_RunsNormally()
        {
            Assert.False(VersionGate.IsBlocked(current: V("1.0.1"), minimum: V("1.0.0")));
        }

        /// <summary>Build numbers must not create a false lockout: 0.9.9.0 is 0.9.9.</summary>
        [Fact]
        public void DifferingPrecision_IsNotABlock()
        {
            Assert.False(VersionGate.IsBlocked(current: V("0.9.9.0"), minimum: V("0.9.9")));
        }

        /// <summary>No signal received yet - never lock on the absence of information.</summary>
        [Fact]
        public void NoMinimumKnown_RunsNormally()
        {
            Assert.False(VersionGate.IsBlocked(current: V("0.9.9"), minimum: null));
        }

        // ---- parsing the published file ----------------------------------------------------

        [Fact]
        public void ValidStatus_YieldsTheMinimumVersion()
        {
            var status = VersionGate.Parse("""{"minimumVersion":"1.0.0"}""");
            Assert.Equal(V("1.0.0"), status.MinimumVersion);
        }

        [Fact]
        public void StatusCanCarryAMessageForTheUser()
        {
            var status = VersionGate.Parse(
                """{"minimumVersion":"1.0.0","message":"PlexusX 1.0 is out."}""");
            Assert.Equal("PlexusX 1.0 is out.", status.Message);
        }

        /// <summary>Malformed or truncated content must never be read as "block everyone" - a
        /// half-written file or an ISP error page would otherwise take the userbase down.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("""{"minimumVersion":"banana"}""")]
        [InlineData("<html>404</html>")]
        public void UnusableContent_YieldsNoMinimum(string json)
        {
            Assert.Null(VersionGate.Parse(json).MinimumVersion);
        }

        // ---- which signal wins -------------------------------------------------------------

        /// <summary>Offline after the signal arrived: the cached answer still applies. Pulling
        /// the network must not undo a lockout that already happened.</summary>
        [Fact]
        public void CachedMinimum_AppliesWhenTheFetchFails()
        {
            var effective = VersionGate.Resolve(fetched: null, cached: V("1.0.0"));
            Assert.Equal(V("1.0.0"), effective);
        }

        /// <summary>A fresh answer replaces the cache.</summary>
        [Fact]
        public void FreshMinimum_ReplacesTheCachedOne()
        {
            var effective = VersionGate.Resolve(fetched: V("1.1.0"), cached: V("1.0.0"));
            Assert.Equal(V("1.1.0"), effective);
        }

        /// <summary>The important one: the highest requirement ever seen wins. Otherwise
        /// serving an older file - or an attacker replaying one - would unlock everyone who
        /// had already been locked.</summary>
        [Fact]
        public void ALowerFetchedMinimum_CannotUndoAHigherCachedOne()
        {
            var effective = VersionGate.Resolve(fetched: V("0.9.0"), cached: V("1.0.0"));
            Assert.Equal(V("1.0.0"), effective);
        }

        [Fact]
        public void NothingFetchedAndNothingCached_MeansNoRestriction()
        {
            Assert.Null(VersionGate.Resolve(fetched: null, cached: null));
        }
    }
}
