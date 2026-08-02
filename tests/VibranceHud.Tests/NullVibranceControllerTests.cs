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

        /// <summary>
        /// The stand-in stands in for two different situations - a PC with no NVIDIA card, and
        /// a laptop whose screen runs off the integrated chip. It has to carry which one it is,
        /// or the UI can only guess and guesses wrong half the time.
        /// </summary>
        [Fact]
        public void ItRemembersWhyThereIsNoDriver()
        {
            Assert.Equal(VibranceDriverState.DisplayNotOnNvidia,
                new NullVibranceController(VibranceDriverState.DisplayNotOnNvidia).DriverState);

            Assert.Equal(VibranceDriverState.NoNvidiaCard,
                new NullVibranceController(VibranceDriverState.NoNvidiaCard).DriverState);
        }

        /// <summary>Nothing that constructs it without a reason should silently claim the
        /// driver works.</summary>
        [Fact]
        public void WithoutAReasonItAssumesNoCardRatherThanWorking()
        {
            Assert.Equal(VibranceDriverState.NoNvidiaCard, new NullVibranceController().DriverState);
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
