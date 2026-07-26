using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Fortnite;
using VibranceHud.Games;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Per-game optimization page for Fortnite: a grid of FPS/visual tweaks written to the
    /// game's GameUserSettings.ini via <see cref="FortniteSettingsService"/> (backed up
    /// first). Fortnite reads this file directly at launch - no launch-options helper needed
    /// - and Epic has no verify/repair URL, so the tools card only offers folder + restore.
    /// </summary>
    public sealed class FortniteSettingsPage : GlowPage
    {
        private const int CardW = 720;
        private const int Pad = 40;

        private readonly FortniteSettingsService _service;
        private readonly DetectedGame _game;
        private readonly Dictionary<FortniteTweak, ToggleSwitch> _toggles = new();
        private Label _status = null!;

        public FortniteSettingsPage(DetectedGame game, Action onBack)
        {
            _game = game;
            _service = new FortniteSettingsService(FortniteSettingsService.DefaultIniPath());
            AutoScroll = true;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(0, 0, 0, 28);

            var current = _service.ReadCurrent();
            int y = 26;

            // ---------- Header ----------
            var back = new LinkLabel
            {
                Text = "‹ Games",
                LinkColor = Theme.TextDim,
                ActiveLinkColor = Theme.Accent,
                LinkBehavior = LinkBehavior.NeverUnderline,
                Location = new Point(Pad, y),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            back.Click += (s, e) => onBack();
            Controls.Add(back);

            Controls.Add(new Label
            {
                Text = "Fortnite",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(Pad - 2, y + 22),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var launch = SettingsPage.FlatButton("▶  Launch Fortnite", Pad + CardW - 170, y + 24, 170);
            launch.BackColor = Theme.AccentDim;
            launch.Click += (s, e) => Shell("com.epicgames.launcher://apps/Fortnite?action=launch&silent=true");
            Controls.Add(launch);
            y += 82;

            if (FortniteSettingsService.IsFortniteRunning())
            {
                Controls.Add(new Label
                {
                    Text = "⚠  Fortnite is running. Close it before applying — it may rewrite configs on exit.",
                    ForeColor = Color.FromArgb(240, 180, 90),
                    BackColor = Color.Transparent,
                    Location = new Point(Pad, y),
                    AutoSize = true
                });
                y += 28;
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
            foreach (var preset in FortnitePresets.All)
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
            var tweaks = FortniteTweaks.All;
            var tw = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 60 + tweaks.Count * 60) };
            tw.Controls.Add(UiHelpers.Caption("FPS & VISUAL TWEAKS", 18, 16, 300));
            int ty = 48;
            foreach (var tweak in tweaks)
            {
                tw.Controls.Add(new Label
                {
                    Text = tweak.Label,
                    ForeColor = Theme.Text,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                    Location = new Point(18, ty),
                    Size = new Size(CardW - 90, 18)
                });
                tw.Controls.Add(new Label
                {
                    Text = tweak.Description,
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Location = new Point(18, ty + 20),
                    Size = new Size(CardW - 90, 18)
                });
                var toggle = new ToggleSwitch { Location = new Point(CardW - 62, ty + 6), Checked = tweak.IsOn(current) };
                _toggles[tweak] = toggle;
                tw.Controls.Add(toggle);
                ty += 60;
            }
            Controls.Add(tw);
            y += tw.Height + 16;

            // ---------- Tools ----------
            var tools = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 92) };
            tools.Controls.Add(UiHelpers.Caption("TOOLS", 18, 16, 200));
            var openFolder = SettingsPage.FlatButton("Game Folder", 18, 44, 150);
            openFolder.Click += (s, e) => Shell(_game.InstallDir);
            tools.Controls.Add(openFolder);
            var restore = SettingsPage.FlatButton("Restore Backup", 180, 44, 150);
            restore.Click += (s, e) =>
            {
                if (!_service.HasBackup) { SetStatus("No backup to restore yet.", Theme.TextDim); return; }
                _service.Restore();
                var cfg = _service.ReadCurrent();
                foreach (var (tweak, toggle) in _toggles) toggle.Checked = tweak.IsOn(cfg);
                SetStatus("Restored your original GameUserSettings.ini.", Theme.TextDim);
            };
            tools.Controls.Add(restore);
            Controls.Add(tools);
            y += 108;

            // ---------- Apply ----------
            var apply = SettingsPage.FlatButton("Apply Changes", Pad, y, 180);
            apply.BackColor = Theme.AccentDim;
            apply.Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
            apply.Height = 38;
            apply.Click += (s, e) => Apply();
            Controls.Add(apply);

            _status = new Label
            {
                Text = "Written to GameUserSettings.ini (a backup is saved). Fortnite reads it directly at launch.",
                ForeColor = Theme.TextDim,
                Location = new Point(Pad + 194, y + 10),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(_status);
        }

        /// <summary>Set every toggle to the preset and write it in one click.</summary>
        private void ApplyPreset(FortnitePreset preset)
        {
            foreach (var toggle in _toggles.Values) toggle.Checked = preset.AllTweaksOn;
            Apply();
        }

        private void Apply()
        {
            if (FortniteSettingsService.IsFortniteRunning())
            {
                var proceed = MessageBox.Show(
                    "Fortnite is running and may overwrite these changes when it exits.\n\n" +
                    "Apply anyway? (Recommended: close Fortnite first.)",
                    "PlexusX", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (proceed != DialogResult.Yes) return;
            }

            var edits = new List<FortniteConfigEdit>();
            foreach (var (tweak, toggle) in _toggles)
                tweak.Write(edits, toggle.Checked);

            try
            {
                _service.Apply(edits);
                SetStatus($"Applied ✓  {edits.Count} settings written (backup saved)", Theme.Accent);
            }
            catch (Exception ex)
            {
                SetStatus("Couldn't write GameUserSettings.ini: " + ex.Message, Color.FromArgb(240, 130, 130));
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
