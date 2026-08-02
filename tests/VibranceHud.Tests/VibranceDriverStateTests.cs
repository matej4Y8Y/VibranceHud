using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Telling apart the two reasons the NVIDIA path can be missing.
    ///
    /// The app used to collapse them into one and say "no NVIDIA GPU" for both. On a gaming
    /// laptop that is simply false: the machine has an NVIDIA card, it just isn't wired to the
    /// built-in screen - that panel runs off the Intel or AMD chip, and NVIDIA only drives the
    /// external ports. The user reads "no NVIDIA GPU", knows they paid for one, and concludes
    /// the app is broken.
    /// </summary>
    public sealed class VibranceDriverStateTests
    {
        [Fact]
        public void An_nvidia_card_driving_a_screen_is_simply_available()
        {
            Assert.Equal(VibranceDriverState.Available,
                VibranceStatus.Determine(nvidiaCardPresent: true, nvidiaDisplayCount: 1));
        }

        [Fact]
        public void An_nvidia_card_driving_no_screen_is_not_the_same_as_having_no_card()
        {
            // The laptop case. This is the whole point of the change.
            Assert.Equal(VibranceDriverState.DisplayNotOnNvidia,
                VibranceStatus.Determine(nvidiaCardPresent: true, nvidiaDisplayCount: 0));
        }

        [Fact]
        public void A_pc_with_no_nvidia_hardware_reports_exactly_that()
        {
            Assert.Equal(VibranceDriverState.NoNvidiaCard,
                VibranceStatus.Determine(nvidiaCardPresent: false, nvidiaDisplayCount: 0));
        }

        [Fact]
        public void No_card_wins_even_if_a_display_count_comes_back_nonzero()
        {
            // Can't happen through the real driver, but the two inputs arrive from different
            // calls and a contradiction must not resolve to "everything's fine".
            Assert.Equal(VibranceDriverState.NoNvidiaCard,
                VibranceStatus.Determine(nvidiaCardPresent: false, nvidiaDisplayCount: 2));
        }

        // ---- what the user is told -------------------------------------------------------

        [Fact]
        public void When_the_driver_is_working_the_readout_is_just_the_number()
        {
            Assert.Equal("78%", VibranceStatus.Readout(VibranceDriverState.Available, 78));
        }

        [Fact]
        public void A_laptop_screen_is_never_told_it_has_no_nvidia_gpu()
        {
            // The exact false statement this change exists to delete.
            var text = VibranceStatus.Readout(VibranceDriverState.DisplayNotOnNvidia, 78);

            Assert.DoesNotContain("no NVIDIA GPU", text);
            Assert.Contains("laptop", text, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void A_laptop_screen_is_pointed_at_the_slider_that_does_work()
        {
            // Being told what's wrong without being told what to do next is still a dead end -
            // Saturation works on any GPU and is right there under the Vibrance slider.
            Assert.Contains("saturation",
                VibranceStatus.Readout(VibranceDriverState.DisplayNotOnNvidia, 50),
                System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void An_amd_or_intel_pc_is_told_the_truth_about_itself()
        {
            // Here "no NVIDIA GPU" is accurate, so it stays - and it must not claim anything
            // about laptop screens, which would be the same mistake in reverse.
            var text = VibranceStatus.Readout(VibranceDriverState.NoNvidiaCard, 50);

            Assert.Contains("NVIDIA", text);
            Assert.DoesNotContain("laptop", text, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Every_state_says_something()
        {
            // A blank readout under the slider looks like a rendering bug.
            foreach (VibranceDriverState state in System.Enum.GetValues<VibranceDriverState>())
                Assert.False(string.IsNullOrWhiteSpace(VibranceStatus.Readout(state, 50)));
        }
    }
}
