using System;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Games;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Fail-closed placeholder shown when <c>MainWindow.OnConfigureGame</c> gets a game id
    /// it doesn't have an explicit optimization page for. Games Hub only ever detects ids
    /// from <see cref="Games.SupportedGames.All"/>, but the per-game pages (especially
    /// Rust's, which edits <c>client.cfg</c> directly) must never run against a game they
    /// weren't written for - a future catalog addition without a matching page here would
    /// otherwise silently fall through to whichever page happened to be the switch default.
    /// </summary>
    public sealed class UnsupportedGamePage : GlowPage
    {
        public UnsupportedGamePage(SupportedGame game, Action onBack)
        {
            AutoScroll = true;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(0, 0, 0, 28);

            // No back link - the chooser in the nav is how you change game now.
            Controls.Add(new Label
            {
                Text = game.DisplayName,
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(38, 26),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            Controls.Add(new Label
            {
                Text = "Optimization for this game isn't available yet.",
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 9.5f),
                Location = new Point(40, 72),
                AutoSize = true,
                BackColor = Color.Transparent
            });
        }
    }
}
