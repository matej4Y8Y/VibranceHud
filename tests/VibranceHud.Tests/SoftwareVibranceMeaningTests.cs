using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Making the Vibrance number mean the same thing on every GPU.
    ///
    /// On NVIDIA the driver owns 0-100, and its neutral is 50 - that's where the picture is
    /// untouched. On the software path the old formula was value/100, so 50 came out at half
    /// saturation and the picture only got back to normal at 100.
    ///
    /// So an AMD or Intel user opened the app, saw the default, and their screen looked worse
    /// than before they installed it. Half our users, told the product is broken by the
    /// product. That's the bug.
    /// </summary>
    public sealed class SoftwareVibranceMeaningTests
    {
        private static float Software(int vibrance) =>
            VibranceEngine.SoftwareVibranceFactor(vibrance, driverAvailable: false);

        [Fact]
        public void Fifty_is_untouched_the_same_as_it_is_on_nvidia()
        {
            // The whole point. 50 is the default the app starts at.
            Assert.Equal(1f, Software(50), 3);
        }

        [Fact]
        public void Zero_is_still_greyscale()
        {
            Assert.Equal(0f, Software(0), 3);
        }

        [Fact]
        public void The_top_of_the_slider_still_reaches_the_same_place_as_before()
        {
            // The ceiling isn't moving - only the middle. Anyone who had the slider maxed
            // keeps exactly what they had.
            Assert.Equal(2f, Software(200), 3);
        }

        [Fact]
        public void Below_the_default_still_drains_colour_and_above_it_still_adds()
        {
            Assert.True(Software(25) < 1f, "quarter should be muted");
            Assert.True(Software(100) > 1f, "past the default should boost");
            Assert.True(Software(150) > Software(100), "should keep climbing");
        }

        [Fact]
        public void It_never_goes_backwards_anywhere_on_the_slider()
        {
            // A slider that dips as you drag it right is the kind of thing users report as
            // "it randomly gets worse".
            for (int v = 1; v <= VibranceEngine.MaxVibrance; v++)
                Assert.True(Software(v) >= Software(v - 1), $"dipped at {v}");
        }

        [Fact]
        public void An_nvidia_machine_is_completely_unaffected()
        {
            // The driver still owns 0-100 there, so none of this changes what NVIDIA users see.
            Assert.Equal(1f, VibranceEngine.SoftwareVibranceFactor(50, driverAvailable: true), 3);
            Assert.Equal(1f, VibranceEngine.SoftwareVibranceFactor(100, driverAvailable: true), 3);
        }
    }

    /// <summary>
    /// Nobody's screen changes because they updated.
    ///
    /// Saved values were written under the old meaning. Reading them back under the new one
    /// without converting would leave every existing AMD and Intel user staring at a different
    /// picture the morning after an update, with no idea why.
    /// </summary>
    public sealed class SoftwareVibranceMigrationTests
    {
        [Fact]
        public void A_saved_value_keeps_producing_the_picture_it_used_to()
        {
            foreach (int oldValue in new[] { 0, 25, 50, 80, 100, 140, 200 })
            {
                float before = oldValue / 100f;               // the old formula, verbatim
                int migrated = VibranceEngine.MigrateSoftwareVibrance(oldValue);
                float after = VibranceEngine.SoftwareVibranceFactor(migrated, driverAvailable: false);

                Assert.True(System.Math.Abs(before - after) < 0.02f,
                    $"{oldValue} used to look like {before:0.###} and now looks like {after:0.###}");
            }
        }

        [Fact]
        public void Migration_stays_inside_the_slider()
        {
            for (int v = 0; v <= VibranceEngine.MaxVibrance; v++)
            {
                int migrated = VibranceEngine.MigrateSoftwareVibrance(v);
                Assert.InRange(migrated, 0, VibranceEngine.MaxVibrance);
            }
        }

        [Fact]
        public void Someone_at_the_old_default_lands_on_something_still_muted()
        {
            // Their screen was washed out before and stays exactly as washed out - we're
            // preserving what they had, not deciding it was wrong for them.
            Assert.True(VibranceEngine.MigrateSoftwareVibrance(50) < 50);
        }
    }
}
