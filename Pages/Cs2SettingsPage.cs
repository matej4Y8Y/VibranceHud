using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using VibranceHud.Cs2;
using VibranceHud.Games;
using VibranceHud.Controls;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Per-game optimization page for Counter-Strike 2: a grid of FPS/visual tweaks written to
    /// the game's autoexec.cfg via <see cref="Cs2SettingsService"/> (backed up first), plus a
    /// copy-paste launch-options helper. We never touch Steam's own files - the launch options
    /// (which include the +exec that actually runs our autoexec) are handed to the user to paste.
    /// </summary>
    public sealed class Cs2SettingsPage : GlowPage
    {
        private const int CardW = 720;
        private const int Pad = 40;

        private readonly Cs2SettingsService _service;
        private readonly DetectedGame _game;
        private readonly Dictionary<Cs2Tweak, ToggleSwitch> _toggles = new();
        private Label _status = null!;

        public Cs2SettingsPage(DetectedGame game, Action onBack)
        {
            _game = game;
            _service = new Cs2SettingsService(Cs2SettingsService.AutoexecPathFor(game.InstallDir));
            AutoScroll = true;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(0, 0, 0, 28);

            var current = _service.ReadCurrent();
            int y = 26;

            // ---------- Header ----------
            // No back link - the chooser in the nav is how you change game now.
            Controls.Add(new Label
            {
                Text = "Counter-Strike 2",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(Pad - 2, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var launch = SettingsPage.PrimaryButton("▶  Launch CS2", Pad + CardW - 150, y + 2, 150);
            launch.Click += (s, e) => Shell($"steam://run/{_game.Game.SteamAppId}");
            Controls.Add(launch);
            y += 60;

            if (Cs2SettingsService.IsCs2Running())
            {
                Controls.Add(new Label
                {
                    Text = "⚠  CS2 is running. Close it before applying — it may rewrite configs on exit.",
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
            foreach (var preset in Cs2Presets.All)
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
            var tweaks = Cs2Tweaks.All;
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

            // ---------- Launch options ----------
            var lo = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 132) };
            lo.Controls.Add(UiHelpers.Caption("RECOMMENDED LAUNCH OPTIONS", 18, 16, 340));
            lo.Controls.Add(new Label
            {
                Text = "Steam → CS2 → Properties → Launch Options. The +exec is what makes the tweaks above run.",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8f),
                Location = new Point(18, 36),
                AutoSize = true
            });
            var box = new TextBox
            {
                Text = Cs2LaunchOptions.Recommended,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.SurfaceHover,
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 9.5f),
                Location = new Point(18, 62),
                Size = new Size(CardW - 180, 26)
            };
            lo.Controls.Add(box);
            var copy = SettingsPage.FlatButton("Copy", CardW - 150, 61, 130);
            copy.Click += (s, e) =>
            {
                try { Clipboard.SetText(Cs2LaunchOptions.Recommended); SetStatus("Launch options copied ✓", Theme.Accent); }
                catch { SetStatus("Couldn't access the clipboard.", Color.FromArgb(240, 130, 130)); }
            };
            lo.Controls.Add(copy);
            Controls.Add(lo);
            y += lo.Height + 16;

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
                SetStatus("Restored your original autoexec.cfg.", Theme.TextDim);
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
                Text = "Written to autoexec.cfg (a backup is saved). Add the launch options above to enable it.",
                ForeColor = Theme.TextDim,
                Location = new Point(Pad + 194, y + 10),
                AutoSize = true,
                // Apply() puts raw exception messages in here ("Couldn't write autoexec.cfg:
                // Access to the path ... is denied"), which are as long as Windows feels like
                // making them. Uncapped, that ran off the page instead of wrapping.
                MaximumSize = new Size(CardW - 194, 0),
                BackColor = Color.Transparent
            };
            Controls.Add(_status);
        }

        /// <summary>Set every toggle to the preset and write it in one click.</summary>
        private void ApplyPreset(Cs2Preset preset)
        {
            foreach (var toggle in _toggles.Values) toggle.Checked = preset.AllTweaksOn;
            Apply();
        }

        private void Apply()
        {
            if (Cs2SettingsService.IsCs2Running())
            {
                var proceed = GlassDialog.Show(FindForm(), "CS2 is running",
                    "CS2 may overwrite these changes when it exits.\n\n" +
                    "Close CS2 first if you want them to stick. Apply anyway?",
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
                SetStatus("Couldn't write autoexec.cfg: " + ex.Message, Color.FromArgb(240, 130, 130));
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
