// The "What's new" body is a plain WinForms TextBox, so any markdown in the notes renders
// literally - users saw "## Co je nového" and "**Datum:**" with the symbols intact. These
// cover the tidy-up that turns note markup into something readable in a plain text box.

using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class ReleaseNotesTextTests
    {
        [Fact]
        public void HeadingMarkers_AreStripped()
        {
            var text = ReleaseNotesText.ToPlainText("## What's new\nSomething");
            Assert.DoesNotContain("#", text);
            Assert.Contains("What's new", text);
        }

        [Fact]
        public void BoldMarkers_AreStripped_ButTheWordsSurvive()
        {
            var text = ReleaseNotesText.ToPlainText("**Vibrance** now works");
            Assert.DoesNotContain("*", text);
            Assert.Contains("Vibrance", text);
            Assert.Contains("now works", text);
        }

        [Fact]
        public void InlineCodeBackticks_AreStripped()
        {
            var text = ReleaseNotesText.ToPlainText("Press `PageDown` to bind");
            Assert.DoesNotContain("`", text);
            Assert.Contains("PageDown", text);
        }

        /// <summary>Bullets should read as bullets, not as raw hyphens or asterisks.</summary>
        [Fact]
        public void ListMarkers_BecomeBullets()
        {
            var text = ReleaseNotesText.ToPlainText("- first\n* second");
            Assert.Contains("•", text);
            Assert.Contains("first", text);
            Assert.Contains("second", text);
        }

        /// <summary>A TextBox needs CRLF or every line collapses onto one.</summary>
        [Fact]
        public void Newlines_AreNormalisedToCrLf()
        {
            var text = ReleaseNotesText.ToPlainText("one\ntwo");
            Assert.Contains("\r\n", text);
            Assert.DoesNotContain("\n\n", text.Replace("\r\n", "|"));
        }

        [Fact]
        public void PlainProse_IsLeftAlone()
        {
            const string plain = "Vibrance now works on AMD and Intel.";
            Assert.Equal(plain, ReleaseNotesText.ToPlainText(plain).Trim());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyInput_ComesBackEmpty(string? input)
        {
            Assert.Equal("", ReleaseNotesText.ToPlainText(input));
        }

        /// <summary>Runs of blank lines shouldn't leave a big hole mid-dialog.</summary>
        [Fact]
        public void ExcessBlankLines_AreCollapsed()
        {
            var text = ReleaseNotesText.ToPlainText("a\n\n\n\n\nb");
            Assert.DoesNotContain("\r\n\r\n\r\n", text);
        }
    }
}
