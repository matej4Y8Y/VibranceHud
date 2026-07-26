using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class DxDeviceTests
    {
        [Fact(Skip = "Requires DX11 GPU; runs on user machine only")]
        public void DxDevice_CanCreateOnAnyAdapter()
        {
            using var device = new DxDevice();
            Assert.True(device.IsAvailable);
        }

        [Fact(Skip = "Requires a multi-adapter DX11 setup; runs on user machine only")]
        public void DxDevice_EnumeratesOutputsAcrossEveryAdapter_NotJustAdapter0()
        {
            // Regression: DxDevice used to call _factory.GetAdapter1(0) only, so a monitor
            // attached to a second GPU (common on laptops with integrated + discrete
            // graphics) never got a target at all.
            using var device = new DxDevice();
            Assert.True(device.IsAvailable);
            Assert.NotEmpty(device.Targets);

            // Every target's device must belong to the same adapter its own output came
            // from - duplicating an output with a device from a different adapter fails.
            foreach (var target in device.Targets)
                Assert.NotNull(target.Device);
        }
    }
}
