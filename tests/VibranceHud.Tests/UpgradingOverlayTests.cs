// Guards the DX11-retry behaviour.
//
// The bug these cover: TryCreateOverlay() ran ONCE at startup. If DX11 init failed for
// any reason - GPU memory tight because the game/OBS launched first, display not ready
// yet, driver mid-reload - the process was locked into the Magnification path for its
// whole lifetime. That path is invisible to OBS/Discord, so the user's saturation simply
// does not appear in their stream, with no indication beyond one line in Settings.
//
// Most of those causes are transient, so the fix is to keep trying and swap up to DX11
// the moment it becomes available. These tests pin the swap down: it must carry the
// current colour state across, must not thrash once it has DX11, and must never leave
// the user with NO overlay if the upgrade attempt throws.

using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class UpgradingOverlayTests
    {
        private sealed class FakeOverlay : ISaturationOverlay, IDisplayOverlay, IDisposable
        {
            public FakeOverlay(OverlayMode mode) => ActiveMode = mode;
            public OverlayMode ActiveMode { get; }
            public DxInitFailureKind LastFailure { get; set; } = DxInitFailureKind.None;
            public string LastFailureMessage { get; set; } = "";
            public float[]? LastMatrix { get; private set; }
            public int ApplyCalls { get; private set; }
            public int ClearCalls { get; private set; }
            public bool Disposed { get; private set; }

            public void Apply(float[] matrix) { ApplyCalls++; LastMatrix = matrix; }
            public void Clear() { ClearCalls++; LastMatrix = null; }
            public void Dispose() => Disposed = true;
        }

        private static float[] Matrix(float marker)
        {
            var m = new float[25];
            m[0] = marker;
            return m;
        }

        /// <summary>Baseline: when DX11 works at startup there is nothing to upgrade to,
        /// and the wrapper must not keep constructing throwaway devices.</summary>
        [Fact]
        public void StartingOnDx_NeverAttemptsAnUpgrade()
        {
            int attempts = 0;
            var dx = new FakeOverlay(OverlayMode.Dx);
            using var overlay = new UpgradingOverlay(dx, () => { attempts++; return new FakeOverlay(OverlayMode.Dx); });

            Assert.False(overlay.TryUpgrade());
            Assert.False(overlay.TryUpgrade());
            Assert.Equal(0, attempts);
            Assert.Equal(OverlayMode.Dx, overlay.ActiveMode);
        }

        /// <summary>While DX11 is still unavailable the user stays on Magnification -
        /// degraded, but working.</summary>
        [Fact]
        public void OnMag_WhenDxStillUnavailable_StaysOnMag()
        {
            var mag = new FakeOverlay(OverlayMode.Mag);
            using var overlay = new UpgradingOverlay(mag, () => null);

            Assert.False(overlay.TryUpgrade());
            Assert.Equal(OverlayMode.Mag, overlay.ActiveMode);
            Assert.False(mag.Disposed);
        }

        /// <summary>The core fix: once DX11 becomes available, swap to it.</summary>
        [Fact]
        public void OnMag_WhenDxBecomesAvailable_SwapsToDx()
        {
            var mag = new FakeOverlay(OverlayMode.Mag);
            var dx = new FakeOverlay(OverlayMode.Dx);
            using var overlay = new UpgradingOverlay(mag, () => dx);

            Assert.True(overlay.TryUpgrade());
            Assert.Equal(OverlayMode.Dx, overlay.ActiveMode);
        }

        /// <summary>The Magnification effect must be torn down on swap, or the screen keeps
        /// a stale colour effect layered under the new DX11 one and everything looks doubly
        /// saturated.</summary>
        [Fact]
        public void Swapping_ClearsAndDisposesTheOldOverlay()
        {
            var mag = new FakeOverlay(OverlayMode.Mag);
            using var overlay = new UpgradingOverlay(mag, () => new FakeOverlay(OverlayMode.Dx));
            overlay.Apply(Matrix(1.5f));

            Assert.True(overlay.TryUpgrade());

            Assert.True(mag.ClearCalls >= 1);
            Assert.True(mag.Disposed);
        }

        /// <summary>The colour the user had set must survive the swap - otherwise the
        /// upgrade visibly resets their saturation to neutral mid-session.</summary>
        [Fact]
        public void Swapping_ReappliesTheCurrentMatrixOnTheNewOverlay()
        {
            var dx = new FakeOverlay(OverlayMode.Dx);
            using var overlay = new UpgradingOverlay(new FakeOverlay(OverlayMode.Mag), () => dx);
            overlay.Apply(Matrix(1.75f));

            Assert.True(overlay.TryUpgrade());

            Assert.Equal(1, dx.ApplyCalls);
            Assert.NotNull(dx.LastMatrix);
            Assert.Equal(1.75f, dx.LastMatrix![0]);
        }

        /// <summary>If the user was at neutral (Clear), the swap must NOT resurrect an old
        /// matrix - that would switch the effect back on by itself.</summary>
        [Fact]
        public void Swapping_WhenClear_DoesNotReapplyAStaleMatrix()
        {
            var dx = new FakeOverlay(OverlayMode.Dx);
            using var overlay = new UpgradingOverlay(new FakeOverlay(OverlayMode.Mag), () => dx);
            overlay.Apply(Matrix(1.9f));
            overlay.Clear();

            Assert.True(overlay.TryUpgrade());

            Assert.Equal(0, dx.ApplyCalls);
        }

        /// <summary>After a successful swap it must stop attempting, so a timer left running
        /// doesn't build a new DX device every tick.</summary>
        [Fact]
        public void AfterUpgrading_StopsAttempting()
        {
            int attempts = 0;
            using var overlay = new UpgradingOverlay(new FakeOverlay(OverlayMode.Mag), () =>
            {
                attempts++;
                return new FakeOverlay(OverlayMode.Dx);
            });

            Assert.True(overlay.TryUpgrade());
            Assert.False(overlay.TryUpgrade());
            Assert.False(overlay.TryUpgrade());
            Assert.Equal(1, attempts);
        }

        /// <summary>A throwing factory must not take the app down or strand the user with no
        /// overlay at all - DX11 init is exactly the kind of thing that throws.</summary>
        [Fact]
        public void UpgradeAttemptThatThrows_LeavesTheUserOnMag()
        {
            var mag = new FakeOverlay(OverlayMode.Mag);
            using var overlay = new UpgradingOverlay(mag, () => throw new InvalidOperationException("dx boom"));

            Assert.False(overlay.TryUpgrade());
            Assert.Equal(OverlayMode.Mag, overlay.ActiveMode);
            Assert.False(mag.Disposed);
        }

        /// <summary>A factory that hands back something still on the Mag path is not an
        /// upgrade; taking it would pointlessly churn the active overlay.</summary>
        [Fact]
        public void FactoryReturningMag_IsNotTreatedAsAnUpgrade()
        {
            var mag = new FakeOverlay(OverlayMode.Mag);
            var alsoMag = new FakeOverlay(OverlayMode.Mag);
            using var overlay = new UpgradingOverlay(mag, () => alsoMag);

            Assert.False(overlay.TryUpgrade());
            Assert.Equal(OverlayMode.Mag, overlay.ActiveMode);
            Assert.False(mag.Disposed);
            // The rejected candidate must be disposed, or it leaks a Magnification session.
            Assert.True(alsoMag.Disposed);
        }

        /// <summary>Colour changes must reach whichever overlay is live at the time.</summary>
        [Fact]
        public void ApplyAndClear_ForwardToTheActiveOverlay()
        {
            var mag = new FakeOverlay(OverlayMode.Mag);
            var dx = new FakeOverlay(OverlayMode.Dx);
            using var overlay = new UpgradingOverlay(mag, () => dx);

            overlay.Apply(Matrix(1.1f));
            Assert.Equal(1, mag.ApplyCalls);

            overlay.TryUpgrade();
            overlay.Apply(Matrix(1.2f));

            Assert.Equal(1, mag.ApplyCalls);          // old one sees nothing further
            Assert.Equal(1.2f, dx.LastMatrix![0]);    // (1 from the swap re-apply, 1 here)
            Assert.Equal(2, dx.ApplyCalls);
        }

        /// <summary>
        /// The reason DX11 failed has to survive the fallback.
        ///
        /// This is the bug that made the whole feature undiagnosable: the DxOverlay that
        /// failed was disposed and dropped on the floor, and MagOverlay hardcodes
        /// LastFailure => None. So Settings showed "Fallback" with no reason attached, and
        /// its hint block - guarded on DxFailure != None - never rendered. A user asked
        /// "why isn't my colour in my stream?" had no way to answer.
        /// </summary>
        [Fact]
        public void FallbackFailureReason_SurvivesEvenThoughMagItselfReportsNone()
        {
            var mag = new FakeOverlay(OverlayMode.Mag)
            {
                LastFailure = DxInitFailureKind.None, // exactly what the real MagOverlay says
                LastFailureMessage = "",
            };
            using var overlay = new UpgradingOverlay(mag, () => null,
                DxInitFailureKind.OutOfMemory, "Not enough GPU memory");

            Assert.Equal(DxInitFailureKind.OutOfMemory, overlay.LastFailure);
            Assert.Equal("Not enough GPU memory", overlay.LastFailureMessage);
        }

        /// <summary>Once upgraded, the stale failure reason must clear - otherwise Settings
        /// keeps warning about capture invisibility after the problem is gone.</summary>
        [Fact]
        public void AfterUpgrading_FailureReasonIsCleared()
        {
            using var overlay = new UpgradingOverlay(new FakeOverlay(OverlayMode.Mag),
                () => new FakeOverlay(OverlayMode.Dx),
                DxInitFailureKind.OutOfMemory, "Not enough GPU memory");

            overlay.TryUpgrade();

            Assert.Equal(DxInitFailureKind.None, overlay.LastFailure);
            Assert.Equal("", overlay.LastFailureMessage);
        }

        [Fact]
        public void Dispose_DisposesWhicheverOverlayIsActive()
        {
            var dx = new FakeOverlay(OverlayMode.Dx);
            var overlay = new UpgradingOverlay(new FakeOverlay(OverlayMode.Mag), () => dx);
            overlay.TryUpgrade();

            overlay.Dispose();

            Assert.True(dx.Disposed);
        }
    }
}
