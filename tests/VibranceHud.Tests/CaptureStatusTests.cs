using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// What recordings actually show, and what the app is allowed to claim about it.
    ///
    /// The bug these pin down: the app used to tell people on the Magnification fallback to
    /// turn on Streaming Mode "for recordings". That advice cannot work - the Magnification
    /// layer is invisible to capture with the switch on or off - so it cost them image
    /// quality and delivered nothing. The same switch was offered to AMD/Intel users, where
    /// it does nothing at all.
    /// </summary>
    public sealed class CaptureStatusTests
    {
        // ---- the state ----------------------------------------------------------------

        /// <summary>
        /// The Magnification path DOES reach recording software - it just doesn't reach every
        /// screen-share path. Two machines proved the old "impossible" claim wrong: an AMD
        /// RX 6800 with no driver vibrance at all, where every bit of the colour is the
        /// software matrix and OBS records it in full, and an NVIDIA 1660 where it also
        /// records. Streaming Mode still cannot change this either way.
        /// </summary>
        [Fact]
        public void The_magnification_fallback_reaches_recording_but_not_every_share_path()
        {
            // With the switch off AND on - the switch is irrelevant here, which was the one
            // thing the old version got right.
            Assert.Equal(CaptureState.DependsOnCaptureMethod,
                CaptureStatus.Resolve(OverlayMode.Mag, driverVibranceAvailable: true, streamingMode: false));
            Assert.Equal(CaptureState.DependsOnCaptureMethod,
                CaptureStatus.Resolve(OverlayMode.Mag, driverVibranceAvailable: true, streamingMode: true));

            // And the same for a machine with no driver at all - the AMD case that disproved
            // the old claim most directly.
            Assert.Equal(CaptureState.DependsOnCaptureMethod,
                CaptureStatus.Resolve(OverlayMode.Mag, driverVibranceAvailable: false, streamingMode: false));
        }

        [Fact]
        public void On_dx11_with_a_driver_the_switch_is_what_makes_vibrance_visible()
        {
            Assert.Equal(CaptureState.NeedsStreamingMode,
                CaptureStatus.Resolve(OverlayMode.Dx, driverVibranceAvailable: true, streamingMode: false));
            Assert.Equal(CaptureState.Visible,
                CaptureStatus.Resolve(OverlayMode.Dx, driverVibranceAvailable: true, streamingMode: true));
        }

        [Fact]
        public void With_no_driver_vibrance_it_is_already_visible_either_way()
        {
            // Software carries the whole range on AMD/Intel, and software is the part capture
            // can see. There is nothing left in the invisible path to move.
            Assert.Equal(CaptureState.Visible,
                CaptureStatus.Resolve(OverlayMode.Dx, driverVibranceAvailable: false, streamingMode: false));
            Assert.Equal(CaptureState.Visible,
                CaptureStatus.Resolve(OverlayMode.Dx, driverVibranceAvailable: false, streamingMode: true));
        }

        // ---- whether to even offer the switch -------------------------------------------

        [Fact]
        public void The_switch_is_only_offered_where_it_can_do_something()
        {
            Assert.True(CaptureStatus.ToggleCanHelp(OverlayMode.Dx, driverVibranceAvailable: true));

            // Both of the cases the old UI offered it in regardless.
            Assert.False(CaptureStatus.ToggleCanHelp(OverlayMode.Mag, driverVibranceAvailable: true));
            Assert.False(CaptureStatus.ToggleCanHelp(OverlayMode.Dx, driverVibranceAvailable: false));
        }

        // ---- what we say ----------------------------------------------------------------

        [Fact]
        public void The_fallback_never_tells_anyone_the_switch_will_fix_it()
        {
            // The original regression: advice to flip a switch that cannot help.
            var reason = CaptureStatus.Reason(CaptureState.DependsOnCaptureMethod, driverVibranceAvailable: true);
            Assert.DoesNotContain("Turn this on", reason);
        }

        /// <summary>
        /// The regression that matters most commercially: the app used to tell everyone on the
        /// Magnification path that recording their colours was impossible on any PC. It is
        /// not, and saying so talks people out of a feature that works.
        /// </summary>
        [Fact]
        public void The_fallback_never_claims_recording_is_impossible()
        {
            foreach (var driver in new[] { true, false })
            {
                var reason = CaptureStatus.Reason(CaptureState.DependsOnCaptureMethod, driver);
                var headline = CaptureStatus.Headline(CaptureState.DependsOnCaptureMethod);

                Assert.DoesNotContain("can't show your colours", headline);
                Assert.DoesNotContain("doesn't work on any PC", reason);
                Assert.DoesNotContain("Not your PC", reason);

                // It has to say the thing that is true and useful instead.
                Assert.Contains("OBS", reason);
            }
        }

        /// <summary>The one instruction that changes the outcome for someone whose colours
        /// aren't showing up. Without it the message is just bad news.</summary>
        [Fact]
        public void Someone_with_no_driver_is_told_which_capture_method_to_use()
        {
            var reason = CaptureStatus.Reason(CaptureState.DependsOnCaptureMethod, driverVibranceAvailable: false);

            Assert.Contains("Capture Method", reason);
            Assert.Contains("1903", reason);
            // And is honest about the one place it genuinely doesn't work.
            Assert.Contains("Discord", reason);
        }

        /// <summary>
        /// An NVIDIA user's colour is mostly applied by the driver, and that reaches every
        /// capture path including Discord screen share - measured on a GTX 1660. Telling them
        /// screen share won't work repeats the same mistake as the message this replaced:
        /// talking somebody out of a feature that already works for them.
        /// </summary>
        [Fact]
        public void Someone_with_a_driver_is_not_told_their_screen_share_is_broken()
        {
            var reason = CaptureStatus.Reason(CaptureState.DependsOnCaptureMethod, driverVibranceAvailable: true);

            Assert.DoesNotContain("won't show them", reason);
            Assert.Contains("screen share", reason);

            // Still honest that the software part above the driver's ceiling is different.
            Assert.Contains("software", reason);
        }

        [Fact]
        public void Someone_with_no_driver_is_told_the_switch_is_not_for_them()
        {
            var reason = CaptureStatus.Reason(CaptureState.Visible, driverVibranceAvailable: false);
            Assert.Contains("never anything to move", reason);
        }

        [Fact]
        public void Every_state_has_something_to_say()
        {
            foreach (var state in new[] { CaptureState.Visible, CaptureState.NeedsStreamingMode, CaptureState.DependsOnCaptureMethod })
            foreach (var driver in new[] { true, false })
            {
                Assert.False(string.IsNullOrWhiteSpace(CaptureStatus.Headline(state)));
                Assert.False(string.IsNullOrWhiteSpace(CaptureStatus.Reason(state, driver)));
            }
        }

        [Fact]
        public void The_two_things_that_are_always_true_are_always_said()
        {
            // Game Capture is the one that makes people think the feature is broken, and it
            // was the sentence being clipped off the bottom of the card.
            Assert.Contains("Game Capture", CaptureStatus.AlwaysTrue);
            Assert.Contains("Gamma", CaptureStatus.AlwaysTrue);
        }
    }
}
