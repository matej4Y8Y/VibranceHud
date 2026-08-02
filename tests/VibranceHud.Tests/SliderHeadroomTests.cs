using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// More headroom where the hardware allows it, and none where it doesn't.
    ///
    /// The trap this guards: the software vibrance curve was written against the slider's
    /// maximum, so raising the maximum would have quietly re-scaled it and changed the colours
    /// of every person already using the app. The curve's slope is fixed now, and the cap just
    /// extends the same line further.
    /// </summary>
    public sealed class SliderHeadroomTests
    {
        // ---- the regression that matters ---------------------------------------------------

        [Theory]
        [InlineData(0, 0f)]
        [InlineData(25, 0.5f)]
        [InlineData(50, 1.0f)]
        [InlineData(125, 1.5f)]
        [InlineData(200, 2.0f)]
        public void Every_value_people_already_use_looks_exactly_as_it_did(int vibrance, float expected)
        {
            // If any of these move, everyone's screen changed because we raised a ceiling they
            // never asked us to touch.
            Assert.Equal(expected, VibranceEngine.SoftwareVibranceFactor(
                vibrance, driverAvailable: false), 3);
        }

        [Fact]
        public void The_curve_carries_straight_on_past_where_it_used_to_stop()
        {
            // Same slope, just more of it: 200 -> 2.0, so 350 -> 3.0.
            Assert.Equal(3.0f, VibranceEngine.SoftwareVibranceFactor(
                350, driverAvailable: false), 3);
        }

        // ---- the new ceilings --------------------------------------------------------------

        [Fact]
        public void Saturation_and_vibrance_reach_the_point_where_colour_flattens_out()
        {
            // Past roughly 3x, colours hit the edge of what the monitor can show and stop
            // being colours - reds become one solid red. That's the ceiling, and both controls
            // now reach it even though their scales differ.
            Assert.Equal(300, VibranceEngine.MaxSaturation);
            Assert.Equal(350, VibranceEngine.MaxVibrance);

            Assert.Equal(3.0f, VibranceEngine.MaxSaturation / 100f, 3);
            Assert.Equal(3.0f, VibranceEngine.SoftwareVibranceFactor(
                VibranceEngine.MaxVibrance, driverAvailable: false), 3);
        }

        [Fact]
        public void Brightness_stops_well_short_of_double()
        {
            // It multiplies pixel values, so anything already bright clips to white and stays
            // there. At 2x half the screen is blown out - that isn't headroom, it's damage.
            Assert.Equal(170, VibranceEngine.MaxBrightness);
            Assert.True(VibranceEngine.MaxBrightness < 200);
        }

        [Fact]
        public void Gamma_is_left_alone_because_windows_would_refuse_anyway()
        {
            // Windows validates gamma ramps and rejects ones that stray too far from normal.
            // A bigger number here would move the slider and change nothing on screen, which
            // is worse than not offering it.
            Assert.Equal(150, VibranceEngine.MaxGamma);
        }
    }

    /// <summary>Share codes have to survive the wider range - they pack the values, and a
    /// silently truncated 350 would hand someone a different screen from the one shared.</summary>
    public sealed class ProfileCodeHeadroomTests
    {
        [Fact]
        public void A_maxed_out_profile_round_trips()
        {
            var maxed = new ProfileCode(
                VibranceEngine.MaxVibrance,
                VibranceEngine.MaxSaturation,
                VibranceEngine.MaxBrightness,
                VibranceEngine.MaxGamma);

            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(maxed), out var back));
            Assert.Equal(maxed, back);
        }

        [Theory]
        [InlineData(201)]
        [InlineData(255)]
        [InlineData(256)]
        [InlineData(300)]
        [InlineData(350)]
        public void Values_past_the_old_byte_sized_ceiling_survive(int vibrance)
        {
            var profile = new ProfileCode(vibrance, 100, 100, 100);
            Assert.True(ProfileCode.TryDecode(ProfileCode.Encode(profile), out var back));
            Assert.Equal(vibrance, back.Vibrance);
        }
    }
}
