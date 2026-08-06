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

        /// <summary>Holds the refresh-rate chips. Rebuilt on every resolution change, since
        /// which rates exist depends entirely on the mode that is live.</summary>
        private Panel? _rateRow;

        private GlassTextBox _customWidth = null!, _customHeight = null!;
        private Label _status = null!;
        private Label _ruleCaption = null!;

        public MonitorPage(AppSettings settings, SettingsStore store, GameSelection selection)
        {
            _settings = settings;
            _store = store;
            _selection = selection;

            AutoScroll = true;

            // Centre the column instead of letting it hug the left edge of a wide window.

            ContentWidth = CardW + 2 * Pad;
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
                // 40, not 30: the title above is 18pt bold and measures 38px tall, so at 30
                // the subtitle sat 8px inside it.
                Location = new Point(Pad, y + 40),
                AutoSize = true,
                BackColor = Color.Transparent,
            });
            y += 68;

            y = BuildNowCard(y);
            y = BuildPvpCard(y);
            y = BuildHdrCard(y);
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

            // ---- refresh rate ----
            //
            // The setting this app's audience cares about most, and the one the page could
            // not change: resolution chips always applied the monitor's maximum. Usually
            // right, occasionally exactly wrong - a panel that drops frames at its top rate,
            // or a second monitor being matched to the first.
            int rateTop = 68 + rows * 42 + 8;
            card.Controls.Add(UiHelpers.Caption("REFRESH RATE", Gutter, rateTop, 260));

            _rateRow = new Panel
            {
                Location = new Point(Gutter, rateTop + 22),
                Size = new Size(CardW - 2 * Gutter, 34),
                BackColor = Color.Transparent,
            };
            card.Controls.Add(_rateRow);
            card.Height = rateTop + 22 + 34 + 16;

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

        private void ApplyRate(int hz)
        {
            var current = DisplayController.Current();
            if (current is not { } now) return;

            if (DisplayController.Apply(now.Width, now.Height, hz))
                SetStatus($"Switched to {hz} Hz.", Theme.TextDim);
            else
                SetStatus($"Your monitor refused {hz} Hz at this resolution - nothing changed.",
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

            RebuildRateChips(current);
        }

        /// <summary>
        /// Rebuild the refresh-rate chips for whatever resolution is live now.
        ///
        /// Rebuilt rather than filtered: which rates exist depends entirely on the current
        /// resolution, so a fixed set of chips would offer rates the monitor cannot do at the
        /// mode it is actually in.
        /// </summary>
        private void RebuildRateChips(DisplayMode? current)
        {
            if (_rateRow == null) return;

            _rateRow.Controls.Clear();
            if (current is not { } now) return;

            var rates = DisplayModes.RefreshRatesFor(DisplayController.SupportedModes(),
                now.Width, now.Height);

            // One rate is not a choice; showing a single dead chip implies there is something
            // to pick.
            if (rates.Count < 2)
            {
                _rateRow.Controls.Add(new Label
                {
                    Text = $"{now.RefreshHz} Hz — the only rate this monitor offers at {now.Width} x {now.Height}.",
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Location = new Point(0, 8),
                    AutoSize = true,
                });
                return;
            }

            int x = 0;
            foreach (int hz in rates)
            {
                var chip = new ChipButton
                {
                    Text = $"{hz} Hz",
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Size = new Size(84, 32),
                    Location = new Point(x, 0),
                    Active = hz == now.RefreshHz,
                };
                int captured = hz;
                chip.Click += (_, _) => ApplyRate(captured);
                _rateRow.Controls.Add(chip);
                x += 92;
            }
        }

        // ---- PvP presets + custom ------------------------------------------------------

        /// <summary>
        /// The three resolutions competitive players actually use, plus a box for anything
        /// else.
        ///
        /// Each preset states its trade-off rather than being sold as a free upgrade. There
        /// is no resolution that is best in every game - the 4:3 options buy wider-looking
        /// player models and more frames by giving up horizontal field of view, and whether
        /// that is a good deal depends on the game and the player.
        /// </summary>
        private int BuildPvpCard(int y)
        {
            var card = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 10) };
            card.Controls.Add(UiHelpers.Caption("PVP PRESETS", Gutter, 16, 260));

            int rowY = 42;
            foreach (var preset in PvpResolutions.All)
            {
                var apply = new GlassButton
                {
                    Text = $"{preset.Width} x {preset.Height}",
                    Kind = GlassButtonKind.Ghost,
                    Location = new Point(Gutter, rowY),
                    Size = new Size(150, 32),
                };
                var captured = preset;
                apply.Click += (_, _) => ApplyPvp(captured);
                card.Controls.Add(apply);

                card.Controls.Add(new Label
                {
                    Text = $"{preset.Name}  ({preset.Aspect})",
                    ForeColor = Theme.Text,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                    Location = new Point(Gutter + 164, rowY),
                    AutoSize = true,
                });

                card.Controls.Add(new Label
                {
                    Text = preset.Why + "  " + preset.TradeOff,
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8f),
                    // 22, not 18: the bold name above measures 21px, so at 18 the two rows
                    // overlapped by three pixels.
                    Location = new Point(Gutter + 164, rowY + 22),
                    Size = new Size(CardW - Gutter * 2 - 164, 30),
                });

                rowY += 62;
            }

            // Said once, under all three, because it is the single most common reason
            // somebody decides these presets are broken.
            card.Controls.Add(new Label
            {
                Text = PvpResolutions.StretchNote,
                ForeColor = Color.FromArgb(240, 180, 90),
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8f),
                Location = new Point(Gutter, rowY),
                Size = new Size(CardW - Gutter * 2, 30),
            });
            rowY += 38;

            // ---- custom ----
            card.Controls.Add(UiHelpers.Caption("CUSTOM", Gutter, rowY, 200));
            rowY += 24;

            _customWidth = NumberBox(Gutter, rowY, "Width");
            _customHeight = NumberBox(Gutter + 96, rowY, "Height");
            card.Controls.Add(_customWidth);
            card.Controls.Add(_customHeight);

            var applyCustom = new GlassButton
            {
                Text = "Apply",
                Kind = GlassButtonKind.Primary,
                Location = new Point(Gutter + 192, rowY - 1),
                Size = new Size(90, 30),
            };
            applyCustom.Click += (_, _) => ApplyCustom();
            card.Controls.Add(applyCustom);

            card.Controls.Add(new Label
            {
                Text = "Anything your monitor reports. If it refuses one, nothing changes.",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8f),
                Location = new Point(Gutter + 292, rowY + 6),
                AutoSize = true,
            });

            card.Height = rowY + 48;
            Controls.Add(card);
            return y + card.Height + 20;
        }

        private static GlassTextBox NumberBox(int x, int y, string placeholder)
        {
            var box = new GlassTextBox
            {
                Location = new Point(x, y),
                Size = new Size(86, 30),
                PlaceholderText = placeholder,
                MaxLength = 5,
            };
            box.Inner.Font = new Font("Consolas", 9.5f);
            return box;
        }

        private void ApplyPvp(PvpResolution preset)
        {
            if (DisplayController.Apply(preset.Width, preset.Height))
            {
                SetStatus(preset.NeedsStretching
                    ? $"Switched to {preset.Width} x {preset.Height}. If you see black bars, "
                      + "turn on full-panel scaling in your graphics driver."
                    : $"Switched to {preset.Width} x {preset.Height}.",
                    Theme.TextDim);
            }
            else
            {
                SetStatus($"Your monitor doesn't offer {preset.Width} x {preset.Height} - nothing changed.",
                    Theme.Accent);
            }

            SyncCurrent();
        }

        /// <summary>
        /// Apply a typed resolution.
        ///
        /// Validated before it reaches the driver, and DisplayController refuses anything the
        /// monitor never reported. Applying an unreported mode is how somebody ends up staring
        /// at a black screen with no way back to change it.
        /// </summary>
        private void ApplyCustom()
        {
            if (!int.TryParse(_customWidth.Text.Trim(), out int w) ||
                !int.TryParse(_customHeight.Text.Trim(), out int h) ||
                w < 640 || h < 480 || w > 15360 || h > 8640)
            {
                SetStatus("Enter a width and height - something like 1440 and 1080.", Theme.Accent);
                return;
            }

            if (!DisplayModes.IsSupported(DisplayController.SupportedModes(), w, h))
            {
                SetStatus($"Your monitor doesn't report {w} x {h}, so PlexusX won't try it.",
                    Theme.Accent);
                return;
            }

            if (DisplayController.Apply(w, h))
                SetStatus($"Switched to {w} x {h}.", Theme.TextDim);
            else
                SetStatus($"Your monitor refused {w} x {h} - nothing changed.", Theme.Accent);

            SyncCurrent();
        }

        // ---- HDR ---------------------------------------------------------------------

        /// <summary>
        /// Turn HDR on or off.
        ///
        /// Here because the capability probe already tells the user HDR is why their advanced
        /// colour does nothing - Windows runs its own colour pipeline in HDR and ignores the
        /// gamma ramp everything tonal is built on. Detecting a dead end and offering no way
        /// out of it is half an answer; this is the other half.
        ///
        /// The card only appears on a machine that can actually do HDR. On everything else it
        /// would be a permanently disabled switch explaining a feature the monitor does not
        /// have.
        /// </summary>
        private int BuildHdrCard(int y)
        {
            var caps = Capabilities.Machine.Current;

            // Nothing measured, or nothing to measure: no card. Machine.Current is Unknown in
            // tests and in any path that skipped the probe.
            if (!caps.HdrActive && caps.GammaRamp != Capabilities.GammaSupport.Refused
                                && caps.GammaRamp != Capabilities.GammaSupport.Clamped)
                return y;

            var card = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 132) };
            card.Controls.Add(UiHelpers.Caption("HDR", Gutter, 16, 260));

            var toggle = new ToggleSwitch
            {
                Location = new Point(CardW - 62, 44),
                Checked = caps.HdrActive,
            };

            var explain = new Label
            {
                ForeColor = caps.HdrActive ? Theme.Accent : Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(Gutter, 44),
                Size = new Size(CardW - 2 * Gutter - 70, 70),
                Text = caps.HdrActive
                    ? "HDR is on, and Windows ignores screen-colour changes while it is. "
                      + "Gamma and everything under Advanced on the Display page will do "
                      + "nothing until you turn it off."
                    : "HDR is off. Your colour controls all work.",
            };

            toggle.CheckedChanged += (_, _) =>
            {
                bool wanted = toggle.Checked;

                if (!Capabilities.HdrDetection.TrySetHdr(wanted))
                {
                    // Never leave the switch showing a state the display did not take.
                    toggle.Checked = !wanted;
                    SetStatus("Windows wouldn't change HDR from here — try Display settings.",
                        Theme.Accent);
                    return;
                }

                SetStatus(wanted
                    ? "HDR on. Restart PlexusX so it re-checks what your colour controls can do."
                    : "HDR off. Restart PlexusX so it re-checks what your colour controls can do.",
                    Theme.TextDim);
            };

            card.Controls.Add(explain);
            card.Controls.Add(toggle);
            Controls.Add(card);

            return y + card.Height + 20;
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
