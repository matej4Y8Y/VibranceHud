using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Verifies the HRESULT -> (Kind, Short, Hint) mapping. Pure logic, no
    /// DxDevice / DirectX runtime required, so these tests run on any
    /// developer machine.
    /// </summary>
    public sealed class DxInitFailureMapperTests
    {
        [Fact]
        public void Map_DXGI_ERROR_NOT_FOUND_returns_NoOutputs_kind()
        {
            var (kind, short_, hint) = DxInitFailureMapper.Map(unchecked((int)0x887A0002));
            Assert.Equal(DxInitFailureKind.NoOutputs, kind);
            Assert.Equal("No display found", short_);
            Assert.Contains("monitor", hint, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Map_DXGI_ERROR_UNSUPPORTED_returns_DriverIssue_kind()
        {
            var (kind, short_, hint) = DxInitFailureMapper.Map(unchecked((int)0x887A0004));
            Assert.Equal(DxInitFailureKind.DriverIssue, kind);
            Assert.Contains("driver", hint, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Map_DXGI_ERROR_DEVICE_REMOVED_returns_DriverIssue_with_retry_hint()
        {
            var (kind, short_, hint) = DxInitFailureMapper.Map(unchecked((int)0x887A0005));
            Assert.Equal(DxInitFailureKind.DriverIssue, kind);
            // The hint must mention retrying - that's the actionable next step
            // for the most common "GPU was just reset" case.
            Assert.Contains("retry", hint, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Map_E_OUTOFMEMORY_returns_OutOfMemory_kind()
        {
            var (kind, short_, hint) = DxInitFailureMapper.Map(unchecked((int)0x8007000E));
            Assert.Equal(DxInitFailureKind.OutOfMemory, kind);
            // The hint must point at the practical fix - "close other 3D apps" -
            // not just the literal word "memory" which is jargon.
            Assert.Contains("Close other 3D apps", hint);
        }

        [Fact]
        public void Map_unknown_HRESULT_returns_Unknown_kind_with_hex_in_hint()
        {
            // Pick a made-up HRESULT that shouldn't map to any known kind.
            var (kind, short_, hint) = DxInitFailureMapper.Map(unchecked((int)0xDEADBEEF));
            Assert.Equal(DxInitFailureKind.Unknown, kind);
            // The hint must include the hex so support can correlate the user's
            // screenshot with our internal tables.
            Assert.Contains("0xDEADBEEF", hint, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MapGeneric_with_null_returns_Unknown_kind()
        {
            var (kind, short_, _) = DxInitFailureMapper.MapGeneric(null);
            Assert.Equal(DxInitFailureKind.Unknown, kind);
        }

        [Fact]
        public void HintForKind_DriverIssue_returns_update_driver_hint()
        {
            var hint = DxInitFailureMapper.HintForKind(DxInitFailureKind.DriverIssue);
            Assert.Contains("driver", hint, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HintForKind_NoOutputs_returns_check_display_hint()
        {
            var hint = DxInitFailureMapper.HintForKind(DxInitFailureKind.NoOutputs);
            Assert.Contains("monitor", hint, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HintForKind_None_returns_fallback_hint()
        {
            // No category - shouldn't happen in practice, but must not throw.
            var hint = DxInitFailureMapper.HintForKind(DxInitFailureKind.None);
            Assert.NotNull(hint);
        }
    }
}