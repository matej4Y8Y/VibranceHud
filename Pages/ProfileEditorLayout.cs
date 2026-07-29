using System;
using System.Drawing;

namespace VibranceHud.Pages
{
    /// <summary>
    /// How tightly the editor packs itself. The page is a full tab now (830x628 in the
    /// default 1040x680 window, but only 690x548 at the 900x600 minimum), so a single
    /// fixed layout either wastes space on the big size or clips on the small one.
    /// Comfortable is the design target; Compact is the fallback that still fits the
    /// smallest window the app allows without a scrollbar.
    /// </summary>
    public enum EditorDensity
    {
        Comfortable,
        Compact,
    }

    /// <summary>
    /// Every rectangle the Profile Editor draws, resolved from the host size. Pure
    /// geometry - no WinForms, no painting - so the fit guarantees ("never scrolls at
    /// the minimum window size") are unit-testable without a message pump.
    /// </summary>
    public sealed record EditorMetrics
    {
        public required EditorDensity Density { get; init; }

        /// <summary>Centered content column - everything lives inside this x-range.</summary>
        public required int ColumnX { get; init; }
        public required int ColumnWidth { get; init; }

        public required Rectangle Header { get; init; }
        public required Rectangle GameCard { get; init; }
        public required Rectangle VisualsCard { get; init; }
        public required Rectangle HubCard { get; init; }
        public required Rectangle Footer { get; init; }

        public required int CardPadding { get; init; }
        public required int CaptionHeight { get; init; }
        public required int CaptionGap { get; init; }
        public required int RowHeight { get; init; }
        public required int ControlHeight { get; init; }

        /// <summary>Total height the layout consumes; compared against the host to
        /// pick the density.</summary>
        public int TotalHeight => Footer.Bottom;

        /// <summary>The interactive control inside the GAME card (the game dropdown).</summary>
        public Rectangle GameControl => new(
            GameCard.X + CardPadding,
            GameCard.Y + CardPadding + CaptionHeight + CaptionGap,
            GameCard.Width - 2 * CardPadding,
            ControlHeight);

        /// <summary>Row <paramref name="index"/> (0-3) of the VISUALS card.</summary>
        public Rectangle VisualsRow(int index)
        {
            if (index < 0 || index > 3)
                throw new ArgumentOutOfRangeException(nameof(index), index, "VISUALS has exactly 4 rows.");
            return new Rectangle(
                VisualsCard.X + CardPadding,
                VisualsCard.Y + CardPadding + CaptionHeight + CaptionGap + index * RowHeight,
                VisualsCard.Width - 2 * CardPadding,
                RowHeight);
        }

        /// <summary>The quality-preset chip strip in the GAME HUB card.</summary>
        public Rectangle HubChipsRow => new(
            HubCard.X + CardPadding,
            HubCard.Y + CardPadding + CaptionHeight + CaptionGap,
            HubCard.Width - 2 * CardPadding,
            ControlHeight);

        /// <summary>The FPS-cap slider row, directly under the chip strip.</summary>
        public Rectangle HubFpsRow => new(
            HubCard.X + CardPadding,
            HubChipsRow.Bottom + CaptionGap,
            HubCard.Width - 2 * CardPadding,
            RowHeight);

        /// <summary>
        /// Split a slider row into caption | track | value. Inline rather than stacked:
        /// at 700 px of column there is room for all three on one line, which halves the
        /// vertical cost of the VISUALS card and keeps the value chip on the same optical
        /// line as the control it belongs to.
        /// </summary>
        public (Rectangle Caption, Rectangle Slider, Rectangle Value) SplitRow(Rectangle row)
        {
            int captionW = Density == EditorDensity.Comfortable ? 132 : 112;
            const int valueW = 58;
            const int gap = 12;
            int sliderW = Math.Max(40, row.Width - captionW - valueW - 2 * gap);

            var caption = new Rectangle(row.X, row.Y, captionW, row.Height);
            var slider = new Rectangle(row.X + captionW + gap, row.Y + (row.Height - SliderHeight) / 2,
                                       sliderW, SliderHeight);
            var value = new Rectangle(row.Right - valueW, row.Y, valueW, row.Height);
            return (caption, slider, value);
        }

        /// <summary>Track height of the sliders inside a row.</summary>
        public int SliderHeight => Density == EditorDensity.Comfortable ? 28 : 24;
    }

    /// <summary>Resolves <see cref="EditorMetrics"/> for a given host size.</summary>
    public static class ProfileEditorLayout
    {
        /// <summary>Widest the content column ever grows. Past this the rows get so long
        /// that the caption and its value chip stop reading as one line.</summary>
        public const int MaxColumnWidth = 700;

        /// <summary>Minimum breathing room either side of the column.</summary>
        public const int SideMargin = 28;

        /// <summary>Narrowest column we will lay out; below this the caller should scroll
        /// rather than keep squeezing.</summary>
        public const int MinColumnWidth = 420;

        private readonly record struct Density(
            int CardPadding, int CardGap, int RowHeight, int HeaderHeight,
            int FooterHeight, int CaptionHeight, int CaptionGap, int ControlHeight);

        private static readonly Density Comfortable = new(
            CardPadding: 16, CardGap: 14, RowHeight: 40, HeaderHeight: 60,
            FooterHeight: 54, CaptionHeight: 18, CaptionGap: 12, ControlHeight: 34);

        private static readonly Density CompactD = new(
            CardPadding: 11, CardGap: 9, RowHeight: 35, HeaderHeight: 50,
            FooterHeight: 46, CaptionHeight: 16, CaptionGap: 8, ControlHeight: 30);

        /// <summary>
        /// Lay the editor out for a host of the given size. Tries the comfortable
        /// density first and falls back to compact only when the comfortable layout
        /// would not fit - so the default window gets the roomy design and the
        /// 900x600 minimum still avoids a scrollbar.
        /// </summary>
        public static EditorMetrics Compute(int hostWidth, int hostHeight)
        {
            var roomy = Build(hostWidth, hostHeight, Comfortable, EditorDensity.Comfortable);
            return roomy.TotalHeight <= hostHeight
                ? roomy
                : Build(hostWidth, hostHeight, CompactD, EditorDensity.Compact);
        }

        private static EditorMetrics Build(int hostWidth, int hostHeight, Density d, EditorDensity kind)
        {
            int column = Math.Clamp(hostWidth - 2 * SideMargin, MinColumnWidth, MaxColumnWidth);
            int x = Math.Max(SideMargin, (hostWidth - column) / 2);

            int gameH = 2 * d.CardPadding + d.CaptionHeight + d.CaptionGap + d.ControlHeight;
            int visualsH = 2 * d.CardPadding + d.CaptionHeight + d.CaptionGap + 4 * d.RowHeight;
            int hubH = 2 * d.CardPadding + d.CaptionHeight + d.CaptionGap
                     + d.ControlHeight + d.CaptionGap + d.RowHeight;

            int y = 0;
            var header = new Rectangle(x, y, column, d.HeaderHeight);
            y = header.Bottom;

            var game = new Rectangle(x, y, column, gameH);
            y = game.Bottom + d.CardGap;

            var visuals = new Rectangle(x, y, column, visualsH);
            y = visuals.Bottom + d.CardGap;

            var hub = new Rectangle(x, y, column, hubH);
            y = hub.Bottom + d.CardGap;

            var footer = new Rectangle(x, y, column, d.FooterHeight);

            return new EditorMetrics
            {
                Density = kind,
                ColumnX = x,
                ColumnWidth = column,
                Header = header,
                GameCard = game,
                VisualsCard = visuals,
                HubCard = hub,
                Footer = footer,
                CardPadding = d.CardPadding,
                CaptionHeight = d.CaptionHeight,
                CaptionGap = d.CaptionGap,
                RowHeight = d.RowHeight,
                ControlHeight = d.ControlHeight,
            };
        }
    }
}
