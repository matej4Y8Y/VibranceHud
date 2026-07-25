using System;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Games;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The Games Hub: detects supported games installed on this PC and shows them as cards.
    /// Clicking a card opens that game's optimization page. v1 supports Rust; more games
    /// slot in as the catalog grows.
    /// </summary>
    public sealed class GamesHubPage : GlowPage
    {
        public GamesHubPage(Action<DetectedGame> onConfigure)
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(40, 32, 40, 32);

            var header = new Label
            {
                Text = "Games Hub",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 16f, FontStyle.Bold),
                Location = new Point(40, 32),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(header);

            var sub = new Label
            {
                Text = "Optimize the supported games installed on your PC.",
                ForeColor = Theme.TextDim,
                Location = new Point(42, 66),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(sub);

            // Cards sit directly on the page rather than inside a FlowLayoutPanel: a
            // transparent control nested inside ANOTHER transparent control is where
            // WinForms' see-through trick actually breaks down - each GameCard already
            // shows the animated backdrop correctly on its own (same one-level pattern
            // CardPanel uses everywhere else), but wrapping them in a second transparent
            // layer left a stale, un-synced snapshot of the background showing through,
            // which is what looked like a twitching leftover block.
            var detected = GameLibrary.DetectInstalled();
            if (detected.Count == 0)
            {
                Controls.Add(new Label
                {
                    Text = "No supported games detected yet.\n" +
                           "Supported: Rust  (League, Valorant, CS2 coming soon)",
                    ForeColor = Theme.TextDim,
                    Location = new Point(40, 104),
                    AutoSize = true,
                    BackColor = Color.Transparent
                });
            }
            else
            {
                const int cardW = 200, cardH = 160, gap = 16, cols = 3;
                int i = 0;
                foreach (var game in detected)
                {
                    var card = new GameCard(game)
                    {
                        Location = new Point(40 + (i % cols) * (cardW + gap),
                                              104 + (i / cols) * (cardH + gap))
                    };
                    card.Click += (s, e) => onConfigure(card.Game);
                    Controls.Add(card);
                    i++;
                }
            }
        }
    }
}
