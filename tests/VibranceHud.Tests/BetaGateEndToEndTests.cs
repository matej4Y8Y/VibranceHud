// End-to-end proof of the beta kill switch, exercised the way the app actually uses it:
// current version vs the published requirement, through the real Parse/Resolve path.

using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class BetaGateEndToEndTests
    {
        private static Version V(string s) => Version.Parse(s);

        /// <summary>Day-to-day: the file names the beta itself, everything runs.</summary>
        [Fact]
        public void WhileTheFileNamesTheBeta_TheBetaRuns()
        {
            var status = VersionGate.Parse("""{"minimumVersion":"0.9.9"}""");
            var effective = VersionGate.Resolve(status.MinimumVersion, cached: null);

            Assert.False(VersionGate.IsBlocked(V("0.9.9"), effective));
        }

        /// <summary>The switch: bump it to 1.0.0 and every beta install locks.</summary>
        [Fact]
        public void RaisingTheMinimum_LocksTheBeta()
        {
            var status = VersionGate.Parse("""{"minimumVersion":"1.0.0"}""");
            var effective = VersionGate.Resolve(status.MinimumVersion, cached: V("0.9.9"));

            Assert.True(VersionGate.IsBlocked(V("0.9.9"), effective));
            Assert.False(VersionGate.IsBlocked(V("1.0.0"), effective));  // the full version is fine
        }

        /// <summary>Every older beta locks too, not just the newest one.</summary>
        [Theory]
        [InlineData("0.9.5")]
        [InlineData("0.9.6")]
        [InlineData("0.9.7")]
        [InlineData("0.9.8")]
        [InlineData("0.9.9")]
        public void EveryBetaBuild_LocksOnTheSameSwitch(string betaVersion)
        {
            var effective = VersionGate.Parse("""{"minimumVersion":"1.0.0"}""").MinimumVersion;
            Assert.True(VersionGate.IsBlocked(V(betaVersion), effective));
        }

        /// <summary>Pulling the network after being locked must not hand the beta back.</summary>
        [Fact]
        public void GoingOfflineAfterTheSwitch_StaysLocked()
        {
            var effective = VersionGate.Resolve(fetched: null, cached: V("1.0.0"));
            Assert.True(VersionGate.IsBlocked(V("0.9.9"), effective));
        }

        /// <summary>Replaying an older status file must not unlock either.</summary>
        [Fact]
        public void ServingAnOlderStatusFile_DoesNotUnlock()
        {
            var stale = VersionGate.Parse("""{"minimumVersion":"0.9.0"}""").MinimumVersion;
            var effective = VersionGate.Resolve(stale, cached: V("1.0.0"));

            Assert.True(VersionGate.IsBlocked(V("0.9.9"), effective));
        }

        /// <summary>A user who has never reached the file keeps working - the switch must not
        /// lock people simply for being offline.</summary>
        [Fact]
        public void NeverHavingSeenTheFile_KeepsWorking()
        {
            var effective = VersionGate.Resolve(fetched: null, cached: null);
            Assert.False(VersionGate.IsBlocked(V("0.9.9"), effective));
        }

        /// <summary>A truncated or hijacked response must never read as "lock everyone".</summary>
        [Theory]
        [InlineData("<html>Captive portal login</html>")]
        [InlineData("""{"minimumVersion":""}""")]
        [InlineData("{ truncated")]
        public void BrokenResponses_NeverCauseALockout(string body)
        {
            var effective = VersionGate.Resolve(VersionGate.Parse(body).MinimumVersion, cached: null);
            Assert.False(VersionGate.IsBlocked(V("0.9.9"), effective));
        }
    }
}
