using System.Linq;
using System.Windows.Forms;
using VibranceHud.Controls;
using Xunit;

namespace VibranceHud.Tests
{
    [Collection("Theme serial")]
    public sealed class GlassDialogTests
    {
        [Theory]
        [InlineData(GlassDialogButtons.Ok, 1)]
        [InlineData(GlassDialogButtons.OkCancel, 2)]
        [InlineData(GlassDialogButtons.YesNo, 2)]
        public void ButtonCountMatchesTheSet(GlassDialogButtons buttons, int expected)
        {
            Assert.Equal(expected, GlassDialog.ButtonCount(buttons));
        }

        [Fact]
        public void HeightGrowsWithBodyLength()
        {
            int shortBody = GlassDialog.MeasureHeight("Short.", 440);
            int longBody = GlassDialog.MeasureHeight(string.Join(" ", new string[60].Select(_ => "word")), 440);

            Assert.True(longBody > shortBody, "a longer message must produce a taller dialog");
        }

        [Fact]
        public void HeightHasAFloorForAnEmptyBody()
        {
            Assert.True(GlassDialog.MeasureHeight("", 440) >= 140);
        }

        /// <summary>
        /// An enormous body must not produce a dialog taller than a screen. The text area
        /// is capped; past that the message is simply too long for a dialog.
        /// </summary>
        [Fact]
        public void HeightIsCapped()
        {
            var huge = string.Join(" ", new string[4000].Select(_ => "word"));
            Assert.True(GlassDialog.MeasureHeight(huge, 440) <= 600);
        }

        [Theory]
        [InlineData(GlassDialogButtons.Ok)]
        [InlineData(GlassDialogButtons.OkCancel)]
        [InlineData(GlassDialogButtons.YesNo)]
        public void ButtonsSitInsideTheDialogAndDoNotOverlap(GlassDialogButtons buttons)
        {
            Theme.Apply("Violet");
            using var dialog = new GlassDialog("Title", "A message of ordinary length.", buttons,
                GlassDialogTone.Info);
            dialog.CreateControl();

            var glassButtons = dialog.Controls.OfType<GlassButton>().ToList();
            Assert.Equal(GlassDialog.ButtonCount(buttons), glassButtons.Count);

            foreach (var b in glassButtons)
            {
                Assert.True(b.Right <= dialog.ClientSize.Width,
                    $"{b.Text} runs past the right edge");
                Assert.True(b.Bottom <= dialog.ClientSize.Height,
                    $"{b.Text} runs past the bottom edge");
                Assert.True(b.Left >= 0 && b.Top >= 0, $"{b.Text} starts outside the dialog");
            }

            if (glassButtons.Count == 2)
            {
                var a = glassButtons[0].Bounds;
                a.Intersect(glassButtons[1].Bounds);
                Assert.True(a.IsEmpty, "the two buttons overlap each other");
            }
        }

        /// <summary>
        /// The buttons are owner-drawn Controls, not IButtonControl, so AcceptButton and
        /// CancelButton cannot reach them. Assigning those would silently do nothing and the
        /// dialog would swallow Enter and Escape - which is why both are handled by hand.
        /// </summary>
        [Fact]
        public void TheDialogDoesNotRelyOnAcceptOrCancelButton()
        {
            Theme.Apply("Violet");
            using var dialog = new GlassDialog("Title", "Body", GlassDialogButtons.OkCancel,
                GlassDialogTone.Info);

            Assert.Null(dialog.AcceptButton);
            Assert.Null(dialog.CancelButton);
            Assert.True(dialog.KeyPreview, "KeyPreview is what lets the form see Enter/Escape first");
        }
    }
}
