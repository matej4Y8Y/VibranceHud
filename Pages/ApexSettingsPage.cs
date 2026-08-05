using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Apex;
using VibranceHud.Games;
using VibranceHud.Controls;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Per-game optimization page for Apex Legends: a grid of FPS/visual tweaks written to
    /// the game's videoconfig.txt via <see cref="ApexSettingsService"/> (backed up first).
    /// Unlike CS2, Apex reads this file directly at launch - no launch-options helper needed.
    /// </summary>
    public sealed class ApexSettingsPage : GlowPage
    {
        private const int CardW = 720;
        private const int Pad = 40;

        private readonly ApexSettingsService _service;
        private readonly DetectedGame _game;
        private readonly Dictionary<ApexTweak, ToggleSwitch> _toggles = new();
        private Label _status = null!;

        public ApexSettingsPage(DetectedGame game, Action onBack, IVibranceEngine? engine = null)
        {
            _game = game;
            _service = new ApexSettingsService(ApexSettingsService.DefaultVideoConfigPath());
            AutoScroll = true;
            // Centre the column instead of letting it hug the left edge of a wide window.
            ContentWidth = CardW + 2 * Pad;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(0, 0, 0, 28);

            var current = _service.ReadCurrent();
            int y = 26;

            // ---------- Header ----------
            // No back link - the chooser in the nav is how you change game now.
            Controls.Add(new Label
            {
                Text = "Apex Legends",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(Pad - 2, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var launch = SettingsPage.PrimaryButton("▶  Launch Apex", Pad + CardW - 150, y + 2, 150);
            launch.Click += (s, e) => Shell($"steam://run/{_game.Game.SteamAppId}");
            Controls.Add(launch);
            y += 60;

            if (ApexSettingsService.IsApexRunning())
            {
                Controls.Add(new Label
                {
                    Text = "⚠  Apex is running. Close it before applying — it may rewrite configs on exit.",
                    ForeColor = Color.FromArgb(240, 180, 90),
                    BackColor = Color.Transparent,
                    Location = new Point(Pad, y),
                    AutoSize = true
                });
                y += 28;
            }

            // ---------- Profile ----------
            //
            // Replaces the Profile Editor page. What you set on Display is what gets saved,
            // so there are no sliders here - configuring the same look twice is exactly why
            // the old editor went unused.
            if (engine != null)
            {
                var profileCard = GameProfileSection.BuildCard(
                    _game.Game.Id, _game.Game.DisplayName, engine, Pad, y, CardW);
                Controls.Add(profileCard);
                y += profileCard.Height + 16;
            }

            // ---------- Quick presets ----------
            var presets = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 96) };
            presets.Controls.Add(UiHelpers.Caption("QUICK PRESETS", 18, 16, 260));
            presets.Controls.Add(new Label
            {
                Text = "One click to set the tweaks below - then Apply.",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8f),
                Location = new Point(18, 34),
                AutoSize = true
            });
            int px = 18;
            foreach (var preset in ApexPresets.All)
            {
                var btn = SettingsPage.FlatButton(preset.Name, px, 56, 160);
                var p = preset;
                btn.Click += (s, e) => ApplyPreset(p);
                presets.Controls.Add(btn);
                px += 172;
            }
            Controls.Add(presets);
            y += presets.Height + 16;

            // ---------- Tweaks ----------
            var tweaks = ApexTweaks.All;
            // Height comes from the finished rows, and each row's description wraps inside
            // its own column instead of sitting in a fixed 18px box it can overflow.
            var tw = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 60) };
            tw.Controls.Add(UiHelpers.Caption("FPS & VISUAL TWEAKS", 18, 16, 300));
            int ty = 48;
            foreach (var tweak in tweaks)
            {
                var toggle = new ToggleSwitch { Location = new Point(CardW - 62, ty + 6), Checked = tweak.IsOn(current) };
                _toggles[tweak] = toggle;
                tw.Controls.Add(toggle);
                ty = TweakRow.Add(tw, tweak.Label, tweak.Description, ty, CardW, toggle) + TweakRow.Gap;
            }
            tw.Height = ty - TweakRow.Gap + 16;
            Controls.Add(tw);
            y += tw.Height + 16;

            // ---------- Tools ----------
            var tools = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 92) };
            tools.Controls.Add(UiHelpers.Caption("TOOLS", 18, 16, 200));
            var openFolder = SettingsPage.FlatButton("Game Folder", 18, 44, 150);
            openFolder.Click += (s, e) => Shell(_game.InstallDir);
            tools.Controls.Add(openFolder);
            var verify = SettingsPage.FlatButton("Verify / Repair", 180, 44, 150);
            verify.Click += (s, e) => Shell($"steam://validate/{_game.Game.SteamAppId}");
            tools.Controls.Add(verify);
            var restore = SettingsPage.FlatButton("Restore Backup", 342, 44, 150);
            restore.Click += (s, e) =>
            {
                if (!_service.HasBackup) { SetStatus("No backup to restore yet.", Theme.TextDim); return; }
                _service.Restore();
                var cfg = _service.ReadCurrent();
                foreach (var (tweak, toggle) in _toggles) toggle.Checked = tweak.IsOn(cfg);
                SetStatus("Restored your original videoconfig.txt.", Theme.TextDim);
            };
            tools.Controls.Add(restore);
            Controls.Add(tools);
            y += 108;

            // ---------- Apply ----------
            var apply = SettingsPage.PrimaryButton("Apply Changes", Pad, y, 180, height: 38);
            apply.Click += (s, e) => Apply();
            Controls.Add(apply);

            _status = new Label
            {
                Text = "Written to videoconfig.txt (a backup is saved). Apex reads it directly at launch.",
                ForeColor = Theme.TextDim,
                Location = new Point(Pad + 194, y + 10),
                AutoSize = true,
                // Holds raw exception text on failure - uncapped it ran off the page.
                MaximumSize = new Size(CardW - 194, 0),
                BackColor = Color.Transparent
            };
            Controls.Add(_status);
        }

        /// <summary>Set every toggle to the preset and write it in one click.</summary>
        private void ApplyPreset(ApexPreset preset)
        {
            foreach (var toggle in _toggles.Values) toggle.Checked = preset.AllTweaksOn;
            Apply();
        }

        private void Apply()
        {
            if (ApexSettingsService.IsApexRunning())
            {
                var proceed = GlassDialog.Show(FindForm(), "Apex is running",
                    "Apex may overwrite these changes when it exits.\n\n" +
                    "Close Apex first if you want them to stick. Apply anyway?",
                    GlassDialogButtons.YesNo, GlassDialogTone.Warning);
                if (proceed != DialogResult.Yes) return;
            }

            var changes = new Dictionary<string, string>();
            foreach (var (tweak, toggle) in _toggles)
                tweak.Write(changes, toggle.Checked);

            try
            {
                _service.Apply(changes);
                SetStatus($"Applied ✓  {changes.Count} settings written (backup saved)", Theme.Accent);
            }
            catch (Exception ex)
            {
                SetStatus("Couldn't write videoconfig.txt: " + ex.Message, Color.FromArgb(240, 130, 130));
            }
        }

        private void SetStatus(string text, Color color)
        {
            _status.ForeColor = color;
            _status.Text = text;
        }

        private static void Shell(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch { /* nothing critical if the shell can't open it */ }
        }
    }
}
