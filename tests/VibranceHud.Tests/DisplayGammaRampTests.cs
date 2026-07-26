using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class DisplayGammaRampTests
    {
        [Fact]
        public void Apply_DriverRefuses_SetsLastApplyFailed()
        {
            // Fakes SetDeviceGammaRamp returning false - a real driver refusal (e.g. a
            // non-monotonic or otherwise rejected ramp), which used to be silently discarded.
            using var ramp = new DisplayGammaRamp((_, _) => false);

            ramp.Apply(GammaCurve.Identity());

            Assert.True(ramp.LastApplyFailed);
        }

        [Fact]
        public void Apply_DriverAccepts_ClearsLastApplyFailed()
        {
            using var ramp = new DisplayGammaRamp((_, _) => true);

            ramp.Apply(GammaCurve.Identity());
            Assert.False(ramp.LastApplyFailed);
        }

        [Fact]
        public void Apply_PassesTheRampThroughToTheNativeCall()
        {
            ushort[]? seen = null;
            using var ramp = new DisplayGammaRamp((_, r) => { seen = r; return true; });

            var expected = GammaCurve.Build(1.3f);
            ramp.Apply(expected);

            Assert.Same(expected, seen);
        }

        [Fact]
        public void LastApplyFailed_ReflectsTheMostRecentCallOnly()
        {
            bool fail = true;
            using var ramp = new DisplayGammaRamp((_, _) => !fail);

            ramp.Apply(GammaCurve.Identity());
            Assert.True(ramp.LastApplyFailed);

            fail = false;
            ramp.Apply(GammaCurve.Identity());
            Assert.False(ramp.LastApplyFailed);
        }
    }
}
