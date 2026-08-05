using System.Windows.Forms;
using VibranceHud;
using VibranceHud.Controls;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Reachable by keyboard, and visible when you get there.
    ///
    /// Three controls out of roughly twenty drew a focus ring before this, which meant
    /// tabbing through PlexusX was invisible - the focus existed, you just could not see
    /// where it was. A screen reader saw nothing at all: no roles, no names, no values.
    /// </summary>
    [Collection("Theme serial")]
    public sealed class FocusAndKeyboardTests
    {
        [Fact]
        public void EveryInteractiveControlCanBeTabbedTo()
        {
            Theme.Apply("Violet");

            Assert.True(new GlassButton().TabStop);
            Assert.True(new ToggleSwitch().TabStop);
            Assert.True(new ChipButton().TabStop);
            Assert.True(new NavButton().TabStop);
            Assert.True(new FlatSlider().TabStop);
        }

        [Fact]
        public void ControlsReportTheRoleTheyActuallyAre()
        {
            Theme.Apply("Violet");

            Assert.Equal(AccessibleRole.PushButton, new GlassButton().AccessibleRole);
            Assert.Equal(AccessibleRole.CheckButton, new ToggleSwitch().AccessibleRole);
            Assert.Equal(AccessibleRole.PushButton, new ChipButton().AccessibleRole);
            Assert.Equal(AccessibleRole.PageTab, new NavButton().AccessibleRole);
            Assert.Equal(AccessibleRole.Slider, new FlatSlider().AccessibleRole);
        }

        [Fact]
        public void ALabelledControlAnnouncesItsLabel()
        {
            Theme.Apply("Violet");

            Assert.Equal("Apply", new GlassButton { Text = "Apply" }.AccessibleName);
            Assert.Equal("Display", new NavButton { Text = "Display" }.AccessibleName);
            Assert.Equal("Balanced", new ChipButton { Text = "Balanced" }.AccessibleName);
        }

        // ---- keyboard activation ---------------------------------------------------------

        [Theory]
        [InlineData(Keys.Space)]
        [InlineData(Keys.Enter)]
        public void SpaceAndEnterPressAButton(Keys key)
        {
            Theme.Apply("Violet");
            using var button = new GlassButton { Text = "Apply" };

            int clicks = 0;
            button.Click += (_, _) => clicks++;
            button.TestPressKey(key);

            Assert.Equal(1, clicks);
        }

        [Fact]
        public void SpaceFlipsAToggle()
        {
            Theme.Apply("Violet");
            using var toggle = new ToggleSwitch { Checked = false };

            toggle.TestPressKey(Keys.Space);

            Assert.True(toggle.Checked);
        }

        [Fact]
        public void AnUnrelatedKeyDoesNothing()
        {
            Theme.Apply("Violet");
            using var button = new GlassButton();

            int clicks = 0;
            button.Click += (_, _) => clicks++;
            button.TestPressKey(Keys.A);

            Assert.Equal(0, clicks);
        }

        // ---- sliders ---------------------------------------------------------------------

        /// <summary>
        /// A slider that can be focused but not operated is worse than one that cannot be
        /// reached: the focus ring promises something the control does not do.
        /// </summary>
        [Fact]
        public void ArrowsNudgeASliderAndPageKeysMoveItFurther()
        {
            Theme.Apply("Violet");
            using var slider = new FlatSlider { Minimum = 0, Maximum = 100, Value = 50 };

            slider.TestPressKey(Keys.Right);
            Assert.Equal(51, slider.Value);

            slider.TestPressKey(Keys.Left);
            slider.TestPressKey(Keys.Left);
            Assert.Equal(49, slider.Value);

            slider.TestPressKey(Keys.PageUp);
            Assert.Equal(59, slider.Value);

            slider.TestPressKey(Keys.PageDown);
            Assert.Equal(49, slider.Value);
        }

        [Fact]
        public void HomeAndEndGoToTheLimits()
        {
            Theme.Apply("Violet");
            using var slider = new FlatSlider { Minimum = 20, Maximum = 80, Value = 50 };

            slider.TestPressKey(Keys.Home);
            Assert.Equal(20, slider.Value);

            slider.TestPressKey(Keys.End);
            Assert.Equal(80, slider.Value);
        }

        [Fact]
        public void ASliderNeverLeavesItsRange()
        {
            Theme.Apply("Violet");
            using var slider = new FlatSlider { Minimum = 0, Maximum = 10, Value = 10 };

            slider.TestPressKey(Keys.PageUp);
            Assert.Equal(10, slider.Value);

            slider.Value = 0;
            slider.TestPressKey(Keys.PageDown);
            Assert.Equal(0, slider.Value);
        }

        // ---- shell navigation ------------------------------------------------------------

        [Theory]
        [InlineData(0, 8, true, 1)]
        [InlineData(7, 8, true, 0)]
        [InlineData(0, 8, false, 7)]
        public void CtrlTabWrapsAroundTheNav(int current, int count, bool forward, int expected)
        {
            Assert.Equal(expected, MainWindow.NextNavIndex(current, count, forward));
        }
    }
}
