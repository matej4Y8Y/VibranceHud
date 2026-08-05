using System;
using VibranceHud;
using VibranceHud.Capabilities;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The gamma probe is the part that actually matters, so it is the part that is tested.
    ///
    /// The whole reason it exists: SetDeviceGammaRamp returning true only means Windows
    /// accepted the call, not that it applied the curve. Windows limits how far a ramp may
    /// deviate from linear, so on a restricted machine - HDR on, or a driver policy - a write
    /// that changes nothing looks identical to one that worked. Reading the ramp back is the
    /// only honest test, and telling "applied", "flattened" and "discarded" apart is what
    /// lets the app say something useful instead of nothing.
    /// </summary>
    public sealed class CapabilityProbeTests
    {
        private static ushort[] Identity => GammaCurve.Identity();

        [Fact]
        public void AnExactReadBackMeansItWorked()
        {
            var written = CapabilityProbe.TestRamp();
            Assert.Equal(GammaSupport.Working,
                CapabilityProbe.Classify(written, written, Identity));
        }

        [Fact]
        public void SmallDriverRoundingStillCountsAsWorking()
        {
            var written = CapabilityProbe.TestRamp();
            var readback = (ushort[])written.Clone();
            for (int i = 0; i < readback.Length; i++)
                readback[i] = (ushort)Math.Clamp(readback[i] + 64, 0, 65535);

            Assert.Equal(GammaSupport.Working,
                CapabilityProbe.Classify(written, readback, Identity));
        }

        /// <summary>The dangerous case: the call succeeded and the screen did not change.</summary>
        [Fact]
        public void ALinearReadBackMeansItWasDiscarded()
        {
            var written = CapabilityProbe.TestRamp();
            Assert.Equal(GammaSupport.Refused,
                CapabilityProbe.Classify(written, Identity, Identity));
        }

        /// <summary>Halfway between what we asked for and linear - Windows kept some of it.</summary>
        [Fact]
        public void APartlyFlattenedReadBackIsClamped()
        {
            var written = CapabilityProbe.TestRamp();
            var identity = Identity;
            var halfway = new ushort[written.Length];
            for (int i = 0; i < written.Length; i++)
                halfway[i] = (ushort)((written[i] + identity[i]) / 2);

            Assert.Equal(GammaSupport.Clamped,
                CapabilityProbe.Classify(written, halfway, identity));
        }

        [Fact]
        public void NoReadBackAtAllIsUntestedNotAFailure()
        {
            // Can't read the ramp is a different thing from the ramp not working, and
            // reporting it as Refused would blame a machine we know nothing about.
            Assert.Equal(GammaSupport.Untested,
                CapabilityProbe.Classify(CapabilityProbe.TestRamp(), null, Identity));
        }

        [Fact]
        public void AMismatchedReadBackLengthIsUntested()
        {
            Assert.Equal(GammaSupport.Untested,
                CapabilityProbe.Classify(CapabilityProbe.TestRamp(), new ushort[10], Identity));
        }

        /// <summary>
        /// The test curve has to be strong enough that a clamp shows up. A gentle one sits
        /// inside what a restricted machine still permits, so it would come back intact and
        /// report Working on a PC where the user's real settings get flattened.
        /// </summary>
        [Fact]
        public void TheTestRampIsFarEnoughFromLinearToDetectAClamp()
        {
            var written = CapabilityProbe.TestRamp();
            var identity = Identity;

            long total = 0;
            for (int i = 0; i < written.Length; i++) total += Math.Abs(written[i] - identity[i]);
            double mean = total / (double)written.Length;

            Assert.True(mean > CapabilityProbe.SameCurveTolerance * 4,
                $"test curve is only {mean:F0} from linear; a clamp could hide inside the tolerance");
        }

        // ---- the probe's own behaviour --------------------------------------------------

        [Fact]
        public void ProbeRestoresWhateverWasThereBefore()
        {
            var target = new FakeGamma(GammaCurve.Build(1.2f), respectWrites: true);

            CapabilityProbe.ProbeGamma(target);

            Assert.Equal(GammaCurve.Build(1.2f), target.Current);
        }

        [Fact]
        public void ProbeRestoresEvenWhenTheScreenCannotBeRead()
        {
            var target = new FakeGamma(null, respectWrites: true);

            CapabilityProbe.ProbeGamma(target);

            // Nothing to put back, so it leaves the screen linear rather than on the test curve.
            Assert.Equal(GammaCurve.Identity(), target.Current);
        }

        [Fact]
        public void ARefusedWriteIsReportedAsRefused()
        {
            var target = new FakeGamma(GammaCurve.Identity(), respectWrites: false);
            Assert.Equal(GammaSupport.Refused, CapabilityProbe.ProbeGamma(target));
        }

        [Fact]
        public void AThrowingTargetDoesNotTakeTheProbeDown()
        {
            Assert.Equal(GammaSupport.Untested, CapabilityProbe.ProbeGamma(new ThrowingGamma()));
        }

        [Fact]
        public void ANullTargetIsSafe()
        {
            Assert.Equal(GammaSupport.Untested, CapabilityProbe.ProbeGamma(null!));
        }

        // ---- what the app then says -----------------------------------------------------

        [Fact]
        public void ToneControlsAreLiveWhenTheRampWorksOrIsMerelyClamped()
        {
            Assert.True(new MachineCapabilities(GammaRamp: GammaSupport.Working).ToneControlsWork);
            Assert.True(new MachineCapabilities(GammaRamp: GammaSupport.Clamped).ToneControlsWork);
            Assert.False(new MachineCapabilities(GammaRamp: GammaSupport.Refused).ToneControlsWork);
        }

        /// <summary>
        /// HDR is the most likely reason a real user's tone controls die, so it has to be
        /// named. "Your driver refused it" sends someone hunting through driver settings for
        /// a switch that is actually in Windows display settings.
        /// </summary>
        [Fact]
        public void HdrIsNamedAsTheCauseWhenItIsTheCause()
        {
            var hdr = new MachineCapabilities(GammaRamp: GammaSupport.Refused, HdrActive: true);
            Assert.Contains("HDR", hdr.ToneLimitation);

            var noHdr = new MachineCapabilities(GammaRamp: GammaSupport.Refused, HdrActive: false);
            Assert.DoesNotContain("HDR", noHdr.ToneLimitation);
            Assert.Contains("driver", noHdr.ToneLimitation);
        }

        [Fact]
        public void AWorkingMachineIsToldNothingAtAll()
        {
            Assert.Equal("", new MachineCapabilities(GammaRamp: GammaSupport.Working).ToneLimitation);
            Assert.Equal("", new MachineCapabilities(GammaRamp: GammaSupport.Untested).ToneLimitation);
        }

        [Fact]
        public void AClampedMachineIsToldItStillWorksJustWeaker()
        {
            var reason = new MachineCapabilities(GammaRamp: GammaSupport.Clamped).ToneLimitation;

            Assert.Contains("weaker", reason);
            // Must not read as "unsupported" - these controls do still do something.
            Assert.DoesNotContain("won't do anything", reason);
        }

        // ---- fakes ----------------------------------------------------------------------

        private sealed class FakeGamma : IGammaProbeTarget
        {
            private readonly bool _respectWrites;
            public ushort[]? Current;

            public FakeGamma(ushort[]? initial, bool respectWrites)
            {
                Current = initial;
                _respectWrites = respectWrites;
            }

            public bool TrySet(ushort[] ramp)
            {
                if (!_respectWrites) return false;
                Current = (ushort[])ramp.Clone();
                return true;
            }

            public ushort[]? TryGet() => Current == null ? null : (ushort[])Current.Clone();
        }

        private sealed class ThrowingGamma : IGammaProbeTarget
        {
            public bool TrySet(ushort[] ramp) => throw new InvalidOperationException("driver exploded");
            public ushort[]? TryGet() => throw new InvalidOperationException("driver exploded");
        }
    }
}
