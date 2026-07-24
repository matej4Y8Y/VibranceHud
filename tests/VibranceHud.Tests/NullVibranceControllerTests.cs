using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class NullVibranceControllerTests
    {
        [Fact]
        public void IsAvailable_IsFalse()
        {
            Assert.False(new NullVibranceController().IsAvailable);
        }

        [Fact]
        public void SetLevel_IsANoOp_AndLevelsStayAtNeutral()
        {
            var ctrl = new NullVibranceController();

            ctrl.SetLevel(30);

            Assert.Equal(100, ctrl.CurrentLevel);
            Assert.Equal(100, ctrl.DefaultLevel);
        }
    }
}
