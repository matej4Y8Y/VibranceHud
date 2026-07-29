// Tests for the Profile Editor's pure layout math.
//
// 2026-07-29 (2nd pass): the editor was rebuilt as a real page in the app's own
// visual language - glass cards over the shared particle field instead of an
// opaque grey slab. The layout math moved out of the page into
// ProfileEditorLayout so the fit guarantees below can be asserted without a
// WinForms message pump.
//
// The guarantee these tests exist to protect: the editor NEVER scrolls, at any
// window size the app allows (900x600 minimum -> 690x548 content host).

using System.Drawing;
using VibranceHud.Pages;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class ProfileEditorPageTests
    {
        // Content host = window minus the 210px nav and the 52px title bar.
        private const int DefaultHostW = 1040 - 210;   // 830
        private const int DefaultHostH = 680 - 52;     // 628
        private const int MinHostW = 900 - 210;        // 690
        private const int MinHostH = 600 - 52;         // 548

        [Fact]
        public void DefaultWindow_UsesComfortableDensity_AndFitsWithoutScrolling()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);

            Assert.Equal(EditorDensity.Comfortable, m.Density);
            Assert.True(m.TotalHeight <= DefaultHostH,
                $"editor is {m.TotalHeight}px tall in a {DefaultHostH}px host - it would scroll.");
        }

        [Fact]
        public void MinimumWindow_FallsBackToCompact_AndStillFitsWithoutScrolling()
        {
            var m = ProfileEditorLayout.Compute(MinHostW, MinHostH);

            Assert.Equal(EditorDensity.Compact, m.Density);
            Assert.True(m.TotalHeight <= MinHostH,
                $"editor is {m.TotalHeight}px tall in a {MinHostH}px host - it would scroll.");
        }

        [Fact]
        public void Column_IsCentred_AndCappedSoRowsStayReadable()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);

            Assert.Equal(ProfileEditorLayout.MaxColumnWidth, m.ColumnWidth);
            // Centred: the gap either side of the column matches.
            Assert.Equal(DefaultHostW - m.ColumnX - m.ColumnWidth, m.ColumnX);
        }

        [Fact]
        public void Column_NeverGrowsPastTheCap_OnAnUltrawideHost()
        {
            var m = ProfileEditorLayout.Compute(2400, DefaultHostH);
            Assert.Equal(ProfileEditorLayout.MaxColumnWidth, m.ColumnWidth);
        }

        [Fact]
        public void Cards_StackInOrder_WithoutOverlapping()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);

            Assert.True(m.Header.Bottom <= m.GameCard.Top);
            Assert.True(m.GameCard.Bottom < m.VisualsCard.Top);
            Assert.True(m.VisualsCard.Bottom < m.HubCard.Top);
            Assert.True(m.HubCard.Bottom < m.Footer.Top);
        }

        [Fact]
        public void AllSections_ShareTheSameColumn()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);

            foreach (var r in new[] { m.Header, m.GameCard, m.VisualsCard, m.HubCard, m.Footer })
            {
                Assert.Equal(m.ColumnX, r.X);
                Assert.Equal(m.ColumnWidth, r.Width);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void VisualsRows_SitInsideTheirCard(int index)
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);
            var row = m.VisualsRow(index);

            Assert.True(row.Top >= m.VisualsCard.Top + m.CardPadding);
            Assert.True(row.Bottom <= m.VisualsCard.Bottom,
                $"row {index} (bottom {row.Bottom}) overflows the card (bottom {m.VisualsCard.Bottom}).");
        }

        [Fact]
        public void VisualsRows_DoNotOverlapEachOther()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);

            for (int i = 1; i < 4; i++)
                Assert.True(m.VisualsRow(i - 1).Bottom <= m.VisualsRow(i).Top);
        }

        [Fact]
        public void VisualsRow_RejectsAnIndexOutsideTheFourSliders()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => m.VisualsRow(4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => m.VisualsRow(-1));
        }

        [Fact]
        public void SplitRow_OrdersCaptionSliderValue_WithoutOverlap()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);
            var (caption, slider, value) = m.SplitRow(m.VisualsRow(0));

            Assert.True(caption.Right <= slider.Left, "caption runs into the slider track");
            Assert.True(slider.Right <= value.Left, "slider track runs into the value chip");
        }

        [Fact]
        public void SplitRow_KeepsEverythingInsideTheRow()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);
            var row = m.VisualsRow(0);
            var (caption, slider, value) = m.SplitRow(row);

            Assert.True(caption.Left >= row.Left);
            Assert.True(value.Right <= row.Right);
            // The track is vertically centred in its row.
            Assert.True(slider.Top >= row.Top && slider.Bottom <= row.Bottom);
        }

        [Fact]
        public void HubRows_SitInsideTheirCard_AndDoNotOverlap()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);

            Assert.True(m.HubChipsRow.Top >= m.HubCard.Top + m.CardPadding);
            Assert.True(m.HubChipsRow.Bottom <= m.HubFpsRow.Top);
            Assert.True(m.HubFpsRow.Bottom <= m.HubCard.Bottom);
        }

        [Fact]
        public void GameControl_SitsInsideTheGameCard()
        {
            var m = ProfileEditorLayout.Compute(DefaultHostW, DefaultHostH);

            Assert.True(m.GameControl.Top >= m.GameCard.Top + m.CardPadding);
            Assert.True(m.GameControl.Bottom <= m.GameCard.Bottom);
            Assert.Equal(m.GameCard.Width - 2 * m.CardPadding, m.GameControl.Width);
        }

        [Fact]
        public void NarrowHost_StillProducesAUsableColumn()
        {
            // Below the app's own minimum the column stops shrinking rather than
            // collapsing to nothing, so the sliders stay grabbable.
            var m = ProfileEditorLayout.Compute(320, MinHostH);
            Assert.True(m.ColumnWidth >= ProfileEditorLayout.MinColumnWidth);
        }

        // ---- Value-chip formatting (unchanged behaviour, still pinned) ----

        [Fact]
        public void FormatValue_RendersIntegersAndDecimals_ThroughCurrentCulture()
        {
            Assert.Equal("75%", ProfileEditorPage.FormatValue(75, decimals: 0, suffix: "%"));
            Assert.Equal("0%", ProfileEditorPage.FormatValue(0, decimals: 0, suffix: "%"));
            Assert.Equal("200%", ProfileEditorPage.FormatValue(200, decimals: 0, suffix: "%"));

            // Czech-locale machines render "1,1" rather than "1.1" - formatting must
            // go through the current culture, not a hard-coded separator.
            Assert.Equal((110 / 100.0).ToString("F2"),
                ProfileEditorPage.FormatValue(110, decimals: 2, suffix: ""));
            Assert.Equal((50 / 100.0).ToString("F2"),
                ProfileEditorPage.FormatValue(50, decimals: 2, suffix: ""));
        }

        [Fact]
        public void FormatFpsCap_ShowsOffAtZero()
        {
            Assert.Equal("Off", ProfileEditorPage.FormatFpsCap(0));
            Assert.Equal("144", ProfileEditorPage.FormatFpsCap(144));
        }

        // ---- Unconfigured games open neutral, not at the slider minimums ----

        [Fact]
        public void NeutralProfile_IsFullyNeutral_NotSliderMinimums()
        {
            // Regression guard. The editor used to build its sliders at Value = Minimum,
            // so opening a game with no saved profile showed 0% vibrance / 0% saturation
            // / 0.50 gamma. Pressing Save from that state wrote a black-and-white profile
            // over whatever the user had.
            var p = ProfileEditorPage.NeutralProfile();

            Assert.Equal(100, p.Vibrance);
            Assert.Equal(100, p.Saturation);
            Assert.Equal(100, p.Brightness);
            Assert.Equal(100, p.Gamma);
        }

        [Fact]
        public void NeutralProfile_CarriesNoQualityOverrideOrFrameCap()
        {
            var p = ProfileEditorPage.NeutralProfile();

            Assert.NotNull(p.GameHub);
            Assert.Equal("", p.GameHub.GraphicsQuality);
            Assert.Equal(0, p.GameHub.FpsCap);
        }

        [Fact]
        public void NeutralProfile_MatchesASavedProfileThatWasNeverTouched()
        {
            // "Never saved" and "saved immediately without changing anything" must
            // produce the same profile, or the editor would appear to change values
            // just by being opened.
            var neutral = ProfileEditorPage.NeutralProfile();
            var freshlyConstructed = new VibranceHud.GameProfile();

            Assert.Equal(freshlyConstructed.Vibrance, neutral.Vibrance);
            Assert.Equal(freshlyConstructed.Saturation, neutral.Saturation);
            Assert.Equal(freshlyConstructed.Brightness, neutral.Brightness);
            Assert.Equal(freshlyConstructed.Gamma, neutral.Gamma);
        }
    }
}
