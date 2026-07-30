// Guards the "one bad monitor must not cost the user every monitor" fix.
//
// DxOverlay built a shader + desktop-duplication capture for every output inside a single
// try/catch. One monitor that couldn't be duplicated threw, the catch tore down every
// monitor already built, and the entire DX11 path was abandoned for the Magnification
// fallback - which no screen-capture tool can see. Multi-monitor users, and anyone with
// OBS already duplicating a display, lost the effect in their stream because of one output.

using System;
using System.Collections.Generic;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class TolerantOutputBuilderTests
    {
        [Fact]
        public void AllOutputsSucceeding_ReturnsEveryIndex()
        {
            var built = new List<int>();
            var ok = TolerantOutputBuilder.Build(3, i => built.Add(i));

            Assert.Equal(new[] { 0, 1, 2 }, ok);
            Assert.Equal(new[] { 0, 1, 2 }, built);
        }

        /// <summary>The core fix: a monitor in the middle failing must not discard the
        /// working ones on either side of it.</summary>
        [Fact]
        public void OneOutputFailing_KeepsTheOthers()
        {
            var ok = TolerantOutputBuilder.Build(3, i =>
            {
                if (i == 1) throw new InvalidOperationException("duplication unavailable");
            });

            Assert.Equal(new[] { 0, 2 }, ok);
        }

        /// <summary>The first monitor failing must not abort the loop - a phantom display is
        /// often enumerated before the real one.</summary>
        [Fact]
        public void FirstOutputFailing_StillBuildsTheRest()
        {
            var ok = TolerantOutputBuilder.Build(3, i =>
            {
                if (i == 0) throw new InvalidOperationException("virtual display");
            });

            Assert.Equal(new[] { 1, 2 }, ok);
        }

        /// <summary>Every output failing returns empty, which is the caller's signal to fall
        /// back to the Magnification path - the one case where falling back is correct.</summary>
        [Fact]
        public void AllOutputsFailing_ReturnsEmpty()
        {
            var ok = TolerantOutputBuilder.Build(2, i => throw new InvalidOperationException("no dx"));

            Assert.Empty(ok);
        }

        /// <summary>Failures are reported so the reason can reach the Settings page rather
        /// than vanishing.</summary>
        [Fact]
        public void Failures_AreReportedWithTheirIndexAndException()
        {
            var errors = new List<(int Index, string Message)>();

            TolerantOutputBuilder.Build(3,
                i => { if (i != 2) throw new InvalidOperationException($"boom {i}"); },
                (i, ex) => errors.Add((i, ex.Message)));

            Assert.Equal(2, errors.Count);
            Assert.Equal((0, "boom 0"), errors[0]);
            Assert.Equal((1, "boom 1"), errors[1]);
        }

        /// <summary>A throwing error-reporter must not turn a tolerated failure into a
        /// crash - the whole point is that this method never throws.</summary>
        [Fact]
        public void ThrowingErrorReporter_DoesNotEscape()
        {
            var ok = TolerantOutputBuilder.Build(2,
                i => { if (i == 0) throw new InvalidOperationException("build failed"); },
                (i, ex) => throw new InvalidOperationException("reporter also failed"));

            Assert.Equal(new[] { 1 }, ok);
        }

        [Fact]
        public void ZeroOutputs_ReturnsEmptyWithoutCallingBuild()
        {
            int calls = 0;
            var ok = TolerantOutputBuilder.Build(0, i => calls++);

            Assert.Empty(ok);
            Assert.Equal(0, calls);
        }
    }
}
