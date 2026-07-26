using System.Threading;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class DebouncedActionTests
    {
        [Fact]
        public void Trigger_DoesNotRunImmediately()
        {
            int calls = 0;
            using var debounced = new DebouncedAction(() => Interlocked.Increment(ref calls), 50);

            debounced.Trigger();

            Assert.Equal(0, calls);
        }

        [Fact]
        public void RapidTriggers_OnlyRunOnce_AfterTheDelayElapses()
        {
            int calls = 0;
            using var debounced = new DebouncedAction(() => Interlocked.Increment(ref calls), 60);

            // Simulate a slider being dragged: many triggers in quick succession, each one
            // arriving before the previous delay window would have elapsed.
            for (int i = 0; i < 10; i++)
            {
                debounced.Trigger();
                Thread.Sleep(10);
            }

            Assert.Equal(0, calls); // still within the window - no save yet

            Thread.Sleep(150); // let the final window elapse

            Assert.Equal(1, calls); // exactly one save, not one per event
        }

        [Fact]
        public void Trigger_RunsAfterTheDelay()
        {
            int calls = 0;
            using var debounced = new DebouncedAction(() => Interlocked.Increment(ref calls), 30);

            debounced.Trigger();
            Thread.Sleep(150);

            Assert.Equal(1, calls);
        }
    }
}
