using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using VibranceHud.Games;
using VibranceHud.Rust;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Per-game optimization page for Rust (Focused v1): graphics quality + FPS limit,
    /// a grid of effect toggles, and tools. Reads the current client.cfg to seed the
    /// controls and writes changes back through <see cref="RustSettingsService"/>, which
    /// backs up the original first.
    /// </summary>
    public sealed class RustSettingsPage : GlowPage
    {
        private sealed record Tweak(string Label, string Convar, string On, string Off);

        private static readonly (string name, int value)[] QualityLevels =
        {
            ("Potato", 0), ("Low", 1), ("Medium", 2), ("High", 3), ("Very High", 4), ("Ultra", 5)
        };

        private static readonly (string name, int value)[] FpsLevels =
        {
            ("60", 60), ("120", 120), ("144", 144), ("240", 240), ("Max", 0)
        };

        // Only convars confirmed present in a real Rust client.cfg. On = effect enabled.
        private static readonly Tweak[] Tweaks =
        {
            new("Motion Blur", "effects.motionblur", "True", "False"),
            new("Bloom", "effects.bloom", "True", "False"),
            new("Ambient Occlusion", "effects.ao", "True", "False"),
            new("Lens Dirt", "effects.lensdirt", "True", "False"),
            new("Sun Shafts", "effects.shafts", "True", "False"),
            new("Sharpen", "effects.sharpen", "True", "False"),
            new("Anti-aliasing", "effects.antialiasing", "2", "0"),
        };

        private readonly RustSettingsService _service;
        private readonly List<ChipButton> _qualityChips = new();
        private readonly List<ChipButton> _fpsChips = new();
        private readonly Dictionary<string, ToggleSwitch> _tweakToggles = new();
        private int _selectedQuality;
        private int _selectedFps;
        private Label _status = null!;

        public RustSettingsPage(DetectedGame game, Action onBack)
        {
            _service = new RustSettingsService(Path.Combine(game.InstallDir, "cfg", "client.cfg"));

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            AutoScroll = true;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(0, 0, 0, 24);

            var current = _service.ReadCurrent();
            int width = 720;
            int y = 28;

            // ---- Header ----
            var back = new LinkLabel
            {
                Text = "‹ Games",
                LinkColor = Theme.TextDim,
                ActiveLinkColor = Theme.Accent,
                LinkBehavior = LinkBehavior.NeverUnderline,
                Location = new Point(40, y),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            back.Click += (s, e) => onBack();
            Controls.Add(back);

            var title = new Label
            {
                Text = "Rust",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(38, y + 22),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(title);

            var launch = SettingsPage.FlatButton("▶  Launch", width - 60, y + 24, 130);
            launch.BackColor = Theme.AccentDim;
            launch.Click += (s, e) => Shell($"steam://run/{game.Game.SteamAppId}");
            Controls.Add(launch);

            y += 84;

            // ---- Rust-running warning ----
            var warn = new Label
            {
                Text = "⚠  Rust is running. Close it before applying — it rewrites its config on exit.",
                ForeColor = Color.FromArgb(240, 180, 90),
                BackColor = Color.Transparent,
                Location = new Point(40, y),
                AutoSize = true,
                Visible = RustSettingsService.IsRustRunning()
            };
            Controls.Add(warn);
            if (warn.Visible) y += 28;

            // ---- Graphics card ----
            var gfx = new CardPanel { Location = new Point(40, y), Size = new Size(width, 150) };
            gfx.Controls.Add(UiHelpers.Caption("GRAPHICS QUALITY", 18, 16, 240));
            _selectedQuality = ReadInt(current, "graphics.quality", 3);
            BuildChipRow(gfx, QualityLevels, 18, 42, _qualityChips, _selectedQuality, v => _selectedQuality = v);

            gfx.Controls.Add(UiHelpers.Caption("FPS LIMIT", 18, 92, 240));
            _selectedFps = ReadInt(current, "fps.limit", 0);
            BuildChipRow(gfx, FpsLevels, 18, 116, _fpsChips, _selectedFps, v => _selectedFps = v);
            Controls.Add(gfx);
            y += 166;

            // ---- Tweaks card ----
            int tweakRows = (Tweaks.Length + 1) / 2;
            var tw = new CardPanel { Location = new Point(40, y), Size = new Size(width, 52 + tweakRows * 40) };
            tw.Controls.Add(UiHelpers.Caption("EFFECTS  (on / off)", 18, 16, 260));
            for (int i = 0; i < Tweaks.Length; i++)
            {
                var t = Tweaks[i];
                int col = i % 2, row = i / 2;
                int cx = 18 + col * ((width - 36) / 2);
                int cy = 46 + row * 40;

                tw.Controls.Add(new Label
                {
                    Text = t.Label,
                    ForeColor = Theme.Text,
                    BackColor = Color.Transparent,
                    Location = new Point(cx, cy + 2),
                    AutoSize = true
                });
                var toggle = new ToggleSwitch
                {
                    Location = new Point(cx + (width - 36) / 2 - 60, cy),
                    Checked = string.Equals(current.Get(t.Convar), t.On, StringComparison.OrdinalIgnoreCase)
                };
                _tweakToggles[t.Convar] = toggle;
                tw.Controls.Add(toggle);
            }
            Controls.Add(tw);
            y += tw.Height + 16;

            // ---- Tools card ----
            var tools = new CardPanel { Location = new Point(40, y), Size = new Size(width, 92) };
            tools.Controls.Add(UiHelpers.Caption("TOOLS", 18, 16, 200));
            var openFolder = SettingsPage.FlatButton("Game Folder", 18, 44, 150);
            openFolder.Click += (s, e) => Shell(Path.GetDirectoryName(Path.GetDirectoryName(_service.ClientCfgPath))!);
            tools.Controls.Add(openFolder);
            var verify = SettingsPage.FlatButton("Verify / Repair", 180, 44, 150);
            verify.Click += (s, e) => Shell($"steam://validate/{game.Game.SteamAppId}");
            tools.Controls.Add(verify);
            var restore = SettingsPage.FlatButton("Restore Backup", 342, 44, 150);
            restore.Click += (s, e) =>
            {
                if (!_service.HasBackup) { SetStatus("No backup to restore yet.", Theme.TextDim); return; }
                _service.Restore();
                ReloadFrom(_service.ReadCurrent());
                SetStatus("Restored original config.", Theme.TextDim);
            };
            tools.Controls.Add(restore);
            Controls.Add(tools);
            y += 108;

            // ---- Apply bar ----
            var apply = SettingsPage.FlatButton("Apply Changes", 40, y, 180);
            apply.BackColor = Theme.AccentDim;
            apply.Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
            apply.Height = 38;
            apply.Click += (s, e) => Apply();
            Controls.Add(apply);

            _status = new Label
            {
                Text = "Changes are written to client.cfg. A backup is saved automatically.",
                ForeColor = Theme.TextDim,
                Location = new Point(232, y + 10),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(_status);
        }

        private void Apply()
        {
            if (RustSettingsService.IsRustRunning())
            {
                var proceed = MessageBox.Show(
                    "Rust is running and will overwrite these changes when it exits.\n\n" +
                    "Apply anyway? (Recommended: close Rust first.)",
                    "PlexusX", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (proceed != DialogResult.Yes) return;
            }

            var changes = new Dictionary<string, string>
            {
                ["graphics.quality"] = _selectedQuality.ToString(),
                ["fps.limit"] = _selectedFps.ToString(),
            };
            foreach (var t in Tweaks)
                changes[t.Convar] = _tweakToggles[t.Convar].Checked ? t.On : t.Off;

            try
            {
                _service.Apply(changes);
                SetStatus("Applied ✓  (backup saved)", Theme.Accent);
            }
            catch (Exception ex)
            {
                SetStatus("Couldn't write config: " + ex.Message, Color.FromArgb(240, 130, 130));
            }
        }

        private void ReloadFrom(RustConfig cfg)
        {
            SelectChip(_qualityChips, ReadInt(cfg, "graphics.quality", 3), v => _selectedQuality = v);
            SelectChip(_fpsChips, ReadInt(cfg, "fps.limit", 0), v => _selectedFps = v);
            foreach (var t in Tweaks)
                _tweakToggles[t.Convar].Checked =
                    string.Equals(cfg.Get(t.Convar), t.On, StringComparison.OrdinalIgnoreCase);
        }

        private void BuildChipRow(Control parent, (string name, int value)[] items, int x, int y,
            List<ChipButton> group, int selected, Action<int> onSelect)
        {
            int w = 96, gap = 8;
            for (int i = 0; i < items.Length; i++)
            {
                var (name, value) = items[i];
                var chip = new ChipButton
                {
                    Text = name,
                    Level = value,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Size = new Size(w, 30),
                    Location = new Point(x + i * (w + gap), y),
                    Active = value == selected
                };
                chip.Click += (s, e) => { onSelect(value); SelectInGroup(group, chip); };
                group.Add(chip);
                parent.Controls.Add(chip);
            }
        }

        private static void SelectInGroup(List<ChipButton> group, ChipButton chosen)
        {
            foreach (var c in group) c.Active = ReferenceEquals(c, chosen);
        }

        private static void SelectChip(List<ChipButton> group, int value, Action<int> onSelect)
        {
            onSelect(value);
            foreach (var c in group) c.Active = c.Level == value;
        }

        private static int ReadInt(RustConfig cfg, string convar, int fallback) =>
            int.TryParse(cfg.Get(convar), out var v) ? v : fallback;

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
