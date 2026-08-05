using System;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud.Pages
{
    /// <summary>
    /// One "title + description + toggle" row, shared by the per-game optimization pages.
    ///
    /// All three pages carried their own copy of this layout, and every copy put the
    /// description in a fixed 18px-tall box. A Label wraps but then clips at its own bounds,
    /// so any description too long for the column simply lost its second line, silently and
    /// only on the machines where the font measured a little wider. Fortnite's "Drops view
    /// distance, shadows, anti-aliasing, textures, effects and post-processing to their
    /// lowest settings for max FPS." sits right on that edge.
    ///
    /// Rows are now as tall as their own text, and the caller stacks them from the returned
    /// bottom rather than a fixed stride.
    /// </summary>
    internal static class TweakRow
    {
        /// <summary>Vertical space between two rows.</summary>
        public const int Gap = 16;

        /// <summary>
        /// Add a row to <paramref name="card"/> starting at <paramref name="y"/>; returns the
        /// y its content ends at.
        /// </summary>
        /// <param name="trailing">The toggle (or whatever sits in the right-hand column), so
        /// a one-line row is still at least as tall as its own control.</param>
        public static int Add(Control card, string label, string description, int y, int cardW,
            Control? trailing = null)
        {
            const int gutter = 18;
            int textW = cardW - 90;   // leaves the right-hand column for the toggle

            var title = new Label
            {
                Text = label,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                Location = new Point(gutter, y),
                AutoSize = true,
                MaximumSize = new Size(textW, 0),
            };
            card.Controls.Add(title);

            var desc = new Label
            {
                Text = description,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(gutter, title.Bottom + 2),
                AutoSize = true,
                MaximumSize = new Size(textW, 0),
            };
            card.Controls.Add(desc);

            return Math.Max(desc.Bottom, trailing?.Bottom ?? 0);
        }
    }
}
