// The auto-updater must never install something older than what's already running.
//
// Reported twice: a user installed 0.9.6, the app announced it was downloading, and came back
// up as 0.9.4. Earlier, another user was put back on 0.7.x the same way.
//
// Cause: before launching a pending installer, the only check was "does something NEWER exist
// online?" - which says nothing about whether the pending file is older than the installed
// build. Worse, that check needs the network, so offline or rate-limited it was skipped
// entirely and whatever installer happened to be sitting in %TEMP% ran unconditionally.
//
// The rule is simple and doesn't depend on the network: never run an installer whose version
// is at or below the running version.

using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class UpdateDowngradeTests
    {
        private static Version V(string s) => Version.Parse(s);

        /// <summary>The exact reported case: 0.9.6 installed, a stale 0.9.4 left over.</summary>
        [Fact]
        public void StalePendingInstaller_OlderThanInstalled_IsRefused()
        {
            Assert.False(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: V("0.9.4"), currentVersion: V("0.9.6"), latestOnline: null));
        }

        /// <summary>The 0.7.x case - a much older installer must be refused just as firmly.</summary>
        [Fact]
        public void MuchOlderPendingInstaller_IsRefused()
        {
            Assert.False(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: V("0.7.1"), currentVersion: V("0.9.6"), latestOnline: null));
        }

        /// <summary>Re-running the version already installed achieves nothing and still
        /// restarts the user's app.</summary>
        [Fact]
        public void PendingInstaller_SameVersionAsInstalled_IsRefused()
        {
            Assert.False(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: V("0.9.6"), currentVersion: V("0.9.6"), latestOnline: null));
        }

        /// <summary>A genuine update still has to go through.</summary>
        [Fact]
        public void NewerPendingInstaller_IsAllowed()
        {
            Assert.True(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: V("0.9.7"), currentVersion: V("0.9.6"), latestOnline: null));
        }

        /// <summary>The network is not required to prevent a downgrade. This is the case that
        /// actually bit: with the online check unavailable, the old code ran the installer
        /// regardless.</summary>
        [Fact]
        public void Offline_StillRefusesADowngrade()
        {
            Assert.False(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: V("0.9.4"), currentVersion: V("0.9.6"), latestOnline: null));
        }

        /// <summary>If something newer than the pending file is already published, skip the
        /// pending one and let the normal update path fetch the newer build.</summary>
        [Fact]
        public void SupersededPendingInstaller_IsSkipped()
        {
            Assert.False(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: V("0.9.7"), currentVersion: V("0.9.6"), latestOnline: V("0.9.8")));
        }

        /// <summary>Pending matches the newest published build - run it.</summary>
        [Fact]
        public void PendingInstaller_MatchingLatestOnline_IsAllowed()
        {
            Assert.True(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: V("0.9.7"), currentVersion: V("0.9.6"), latestOnline: V("0.9.7")));
        }

        /// <summary>An installer whose version can't be read is not trustworthy enough to run
        /// over a working install - fail closed.</summary>
        [Fact]
        public void UnreadableInstallerVersion_IsRefused()
        {
            Assert.False(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: null, currentVersion: V("0.9.6"), latestOnline: null));
        }

        /// <summary>Build numbers count: 0.9.6.0 vs 0.9.6 must not read as an upgrade.</summary>
        [Fact]
        public void EqualVersionsWithDifferentPrecision_AreNotAnUpgrade()
        {
            Assert.False(UpdateService.ShouldRunPendingInstaller(
                pendingVersion: V("0.9.6.0"), currentVersion: V("0.9.6"), latestOnline: null));
        }
    }
}
