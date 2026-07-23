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

            var flow = new FlowLayoutPanel
            {
                Location = new Point(40, 104),
                Size = new Size(760, 400),
                BackColor = Theme.Background,
                WrapContents = true,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            var detected = GameLibrary.DetectInstalled();
            if (detected.Count == 0)
            {
                flow.Controls.Add(new Label
                {
                    Text = "No supported games detected yet.\n" +
                           "Supported: Rust  (League, Valorant, CS2 coming soon)",
                    ForeColor = Theme.TextDim,
                    AutoSize = true,
                    BackColor = Color.Transparent
                });
            }
            else
            {
                foreach (var game in detected)
                {
                    var card = new GameCard(game);
                    card.Click += (s, e) => onConfigure(card.Game);
                    flow.Controls.Add(card);
                }
            }

            Controls.Add(flow);
        }
    }
}
