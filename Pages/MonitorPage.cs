using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Games;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Everything about the shape of the picture: what resolution the desktop is in now, and
    /// what it should switch to when a game launches.
    ///
    /// This used to be a card buried in Rust's optimisation page, which meant a CS2 player
    /// could not reach it at all and a Rust player only found it while looking for something
    /// else. Resolution is not a Rust feature - it is a display feature, and this app's whole
    /// premise is that it owns the display. So it gets a tab.
    ///
    /// The other half of the reason: the job people actually want done is "stop making me
    /// open the NVIDIA panel to change resolution". That is only true if it is one click from
    /// anywhere, which a nested card never was.
    /// </summary>
    public sealed class MonitorPage : GlowPage
    {
        private const int Pad = 28;
        private const int CardW = 700;
        private const int Gutter = 18;

        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly GameSelection _selection;

        private readonly List<ChipButton> _modeChips = new();
        private readonly List<DisplayMode> _modes = new();
        private readonly List<ChipButton> _ruleChips = new();

        private Label _currentLabel = null!;
        private Label _status = null!;
        private Label _ruleCaption = null!;

        public MonitorPage(AppSettings settings, SettingsStore store, GameSelection selection)
        {
            _settings = settings;
            _store = store;
            _selection = selection;

            AutoScroll = true;
            Font = new Font(Theme.FontFamily, 9.5f);

            int y = Pad;

            Controls.Add(new Label
            {
                Text = "Monitor",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(Pad - 2, y),
                AutoSize = true,
                BackColor = Color.Transparent,
            });
            Controls.Add(new Label
            {
                Text = "Change resolution without leaving PlexusX.",
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 9f),
                Location = new Point(Pad, y + 30),
                AutoSize = true,
                BackColor = Color.Transparent,
            });
            y += 68;

            y = BuildNowCard(y);
            y = BuildPerGameCard(y);

            _status = new Label
            {
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(Pad, y),
                AutoSize = true,
                MaximumSize = new Size(CardW, 0),
            };
            Controls.Add(_status);

            AutoScrollMinSize = new Size(0, y + 60);

            _selection.Changed += (_, _) => RefreshRuleSection();
        }

        // ---- "right now" -------------------------------------------------------------

        private int BuildNowCard(int y)
        {
            // One mode per resolution, highest refresh for each. Offering 1920x1080 six times
            // at six refresh rates is a wall of near-identical buttons, and the refresh rate
            // is not the thing anyone is here to pick.
            var best = DisplayModes.BestPerResolution(DisplayController.SupportedModes())
                .Take(12).ToList();

            int rows = (best.Count + 3) / 4;
            var card = new CardPanel
            {
                Location = new Point(Pad, y),
                Size = new Size(CardW, 92 + rows * 42),
            };
            card.Controls.Add(UiHelpers.Caption("RESOLUTION NOW", Gutter, 16, 260));

            _currentLabel = new Label
            {
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(Gutter, 38),
                AutoSize = true,
            };
            card.Controls.Add(_currentLabel);

            int chipW = (CardW - 2 * Gutter - 3 * 10) / 4;
            for (int i = 0; i < best.Count; i++)
            {
                var mode = best[i];
                var chip = new ChipButton
                {
                    Text = $"{mode.Width} x {mode.Height}",
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Size = new Size(chipW, 32),
                    Location = new Point(Gutter + (i % 4) * (chipW + 10), 68 + (i / 4) * 42),
                };
                chip.Click += (_, _) => ApplyNow(mode);
                _modes.Add(mode);
                _modeChips.Add(chip);
                card.Controls.Add(chip);
            }

            Controls.Add(card);
            SyncCurrent();
            return y + card.Height + 20;
        }

        private void ApplyNow(DisplayMode mode)
        {
            // Switching resolution is the one action on this page that can leave someone
            // looking at a black screen, so the failure has to say so rather than appearing
            // to have worked. DisplayController tests the mode with the driver first.
            if (DisplayController.Apply(mode.Width, mode.Height))
                SetStatus($"Switched to {mode.Width} x {mode.Height}.", Theme.TextDim);
            else
                SetStatus($"Your monitor refused {mode.Width} x {mode.Height} - nothing changed.",
                    Theme.Accent);

            SyncCurrent();
        }

        private void SyncCurrent()
        {
            var current = DisplayController.Current();
            _currentLabel.Text = current is { } now
                ? $"Currently {now.Width} x {now.Height} at {now.RefreshHz} Hz"
                : "Windows didn't report a current mode.";

            for (int i = 0; i < _modeChips.Count; i++)
                _modeChips[i].Active = current is { } c
                    && _modes[i].Width == c.Width && _modes[i].Height == c.Height;
        }

        // ---- per-game rule -----------------------------------------------------------

        private int BuildPerGameCard(int y)
        {
            var best = DisplayModes.BestPerResolution(DisplayController.SupportedModes())
                .Take(11).ToList();

            int total = best.Count + 1;              // + "Don't change"
            int rows = (total + 3) / 4;
            var card = new CardPanel
            {
                Location = new Point(Pad, y),
                Size = new Size(CardW, 96 + rows * 42),
            };
            card.Controls.Add(UiHelpers.Caption("WHEN A GAME LAUNCHES", Gutter, 16, 320));

            _ruleCaption = new Label
            {
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(Gutter, 38),
                AutoSize = true,
                MaximumSize = new Size(CardW - 2 * Gutter, 0),
            };
            card.Controls.Add(_ruleCaption);

            int chipW = (CardW - 2 * Gutter - 3 * 10) / 4;

            // "Don't change" first: it is the default, and it is the way back out.
            var none = new ChipButton
            {
                Text = "Don't change",
                Font = new Font(Theme.FontFamily, 8.5f),
                Size = new Size(chipW, 32),
                Location = new Point(Gutter, 72),
            };
            none.Click += (_, _) => SetRule(0, 0);
            _ruleChips.Add(none);
            card.Controls.Add(none);

            for (int i = 0; i < best.Count; i++)
            {
                var mode = best[i];
                int slot = i + 1;
                var chip = new ChipButton
                {
                    Text = $"{mode.Width} x {mode.Height}",
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Size = new Size(chipW, 32),
                    Location = new Point(Gutter + (slot % 4) * (chipW + 10), 72 + (slot / 4) * 42),
                };
                chip.Click += (_, _) => SetRule(mode.Width, mode.Height);
                _ruleChips.Add(chip);
                card.Controls.Add(chip);
            }

            Controls.Add(card);
            RefreshRuleSection();
            return y + card.Height + 20;
        }

        private void SetRule(int width, int height)
        {
            if (_selection.Current is not { } game) return;

            _settings.MonitorRules = MonitorRules.Set(_settings.MonitorRules, game.Id, width, height);
            _store.Save(_settings);

            SetStatus(width > 0
                ? $"{game.DisplayName} will switch to {width} x {height} on launch, and back when it closes."
                : $"{game.DisplayName} will leave your resolution alone.", Theme.TextDim);

            RefreshRuleSection();
        }

        /// <summary>Re-read the rule for whichever game the app is pointed at. Called when the
        /// selection changes, so this page follows the chooser like everything else.</summary>
        private void RefreshRuleSection()
        {
            var game = _selection.Current;
            bool hasGame = game != null;

            foreach (var chip in _ruleChips) chip.Enabled = hasGame;

            if (!hasGame)
            {
                _ruleCaption.Text = "Pick a game at the bottom left to set a launch resolution for it.";
                foreach (var chip in _ruleChips) chip.Active = false;
                return;
            }

            var rule = MonitorRules.For(_settings.MonitorRules, game!.Id);
            _ruleCaption.Text = rule == null
                ? $"{game.DisplayName} currently leaves your resolution alone."
                : $"{game.DisplayName} switches to {rule.Width} x {rule.Height}, and back on exit.";

            _ruleChips[0].Active = rule == null;
            for (int i = 1; i < _ruleChips.Count; i++)
            {
                var text = _ruleChips[i].Text.Replace(" ", "");
                _ruleChips[i].Active = rule != null && text == $"{rule.Width}x{rule.Height}";
            }
        }

        private void SetStatus(string text, Color colour)
        {
            _status.ForeColor = colour;
            _status.Text = text;
        }
    }
}
