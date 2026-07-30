// The error code a user's own streaming setup produces.
//
// DXGI_ERROR_NOT_CURRENTLY_AVAILABLE means Desktop Duplication is already taken on that
// display. OBS "Display Capture", ShadowPlay and various overlay tools use the same API,
// so this is one of the most likely failures for the people PlexusX is aimed at. It used
// to fall through to Unknown, which told the user to "report the code" instead of the one
// thing that actually fixes it.

using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class DxInitFailureMapperCaptureTests
    {
        private const int DxgiErrorNotCurrentlyAvailable = unchecked((int)0x887A0022);

        [Fact]
        public void DuplicationAlreadyInUse_IsCategorisedAsCaptureInUse()
        {
            var (kind, label, hint) = DxInitFailureMapper.Map(DxgiErrorNotCurrentlyAvailable);

            Assert.Equal(DxInitFailureKind.CaptureInUse, kind);
            Assert.NotEmpty(label);
            Assert.NotEmpty(hint);
        }

        /// <summary>The hint has to name the actual remedy - closing the other capture -
        /// rather than sending the user to update a driver.</summary>
        [Fact]
        public void CaptureInUseHint_PointsAtOtherCaptureApps()
        {
            var hint = DxInitFailureMapper.Map(DxgiErrorNotCurrentlyAvailable).Hint;

            Assert.Contains("capture", hint, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("driver", hint, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Settings only persists the kind, so the hint must be recoverable from it
        /// alone - otherwise the warning renders with no advice attached.</summary>
        [Fact]
        public void HintForKind_ResolvesCaptureInUse()
        {
            var hint = DxInitFailureMapper.HintForKind(DxInitFailureKind.CaptureInUse);

            Assert.Contains("capture", hint, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
