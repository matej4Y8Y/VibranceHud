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
    }
}
