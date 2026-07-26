using System;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Games;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The Games Hub: shows every game PlexusX supports as a card, installed games first.
    /// Clicking an installed card opens that game's optimization page; not-installed cards
    /// are shown dimmed and inert.
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
                Text = "Everything PlexusX supports. Installed games are ready to configure.",
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
            var ordered = GameLibrary.OrderForHub(GameLibrary.DetectInstalled());

            const int cardW = 200, cardH = 160, gap = 16, cols = 3;
            int i = 0;
            foreach (var (game, detected) in ordered)
            {
                var card = new GameCard(game, detected)
                {
                    Location = new Point(40 + (i % cols) * (cardW + gap),
                                          104 + (i / cols) * (cardH + gap))
                };
                if (detected != null)
                    card.Click += (s, e) => onConfigure(detected);
                Controls.Add(card);
                i++;
            }
        }
    }
}
