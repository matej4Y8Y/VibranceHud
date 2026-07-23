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
    /// Per-game optimization page for Rust: graphics quality, FPS limit, field of view, a
    /// grid of one-click optimization tweaks, and tools. Everything is written to Rust's
    /// own client.cfg through <see cref="RustSettingsService"/>, which backs up the
    /// original first.
    /// </summary>
    public sealed class RustSettingsPage : GlowPage
    {
        private const int CardW = 720;
        private const int Pad = 40;

        private static readonly (string name, int value)[] QualityLevels =
        {
            ("Potato", 0), ("Low", 1), ("Medium", 2), ("High", 3), ("Very High", 4), ("Ultra", 5)
        };

        private static readonly (string name, int value)[] FpsLevels =
        {
            ("60", 60), ("120", 120), ("144", 144), ("240", 240), ("Max", 0)
        };

        private readonly RustSettingsService _service;
        private readonly DetectedGame _game;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly List<ChipButton> _qualityChips = new();
        private readonly List<ChipButton> _fpsChips = new();
        private readonly List<ChipButton> _ramChips = new();
        private readonly List<ChipButton> _resChips = new();
        private int _selectedResW, _selectedResH;
        private readonly Dictionary<Tweak, ChipButton> _tweakChips = new();
        private readonly FlatSlider _fov;
        private int _selectedQuality;
        private int _selectedFps;
        private int _selectedRamTier;
        private Label _fovValue = null!;
        private Label _status = null!;

        public RustSettingsPage(DetectedGame game, AppSettings settings, SettingsStore store, Action onBack)
        {
            _game = game;
            _settings = settings;
            _store = store;
            _service = new RustSettingsService(Path.Combine(game.InstallDir, "cfg", "client.cfg"));
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
                Text = "Rust",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(Pad - 2, y + 22),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var launch = SettingsPage.FlatButton("▶  Launch Rust", Pad + CardW - 150, y + 24, 150);
            launch.BackColor = Theme.AccentDim;
            launch.Click += (s, e) => LaunchRust();
            Controls.Add(launch);
            y += 82;

            if (RustSettingsService.IsRustRunning())
            {
                Controls.Add(new Label
                {
                    Text = "⚠  Rust is running. Close it before applying — it rewrites its config on exit.",
                    ForeColor = Color.FromArgb(240, 180, 90),
                    BackColor = Color.Transparent,
                    Location = new Point(Pad, y),
                    AutoSize = true
                });
                y += 28;
            }

            // ---------- Monitor resolution ----------
            // Built from what this monitor actually reports, so it can never offer a mode
            // that would black-screen someone.
            var supported = DisplayModes.BestPerResolution(DisplayController.SupportedModes());
            var offered = supported.Take(11).ToList();
            int resCols = 3, resW = (CardW - 36 - (resCols - 1) * 10) / resCols, resH = 32;
            int resRows = (offered.Count + 1 + resCols - 1) / resCols;
            var resCard = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 74 + resRows * (resH + 10)) };
            resCard.Controls.Add(UiHelpers.Caption("MONITOR RESOLUTION", 18, 16, 300));
            resCard.Controls.Add(new Label
            {
                Text = "Switches your desktop to this on launch, at your monitor's highest refresh rate. Restored when Rust closes.",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8f),
                Location = new Point(18, 36),
                AutoSize = true
            });

            _selectedResW = _settings.RustResolutionWidth;
            _selectedResH = _settings.RustResolutionHeight;

            // "Native" first, then the monitor's resolutions.
            var choices = new List<(string label, int w, int h)> { ("Native", 0, 0) };
            foreach (var m in offered) choices.Add(($"{m.Width}x{m.Height}", m.Width, m.Height));

            for (int i = 0; i < choices.Count; i++)
            {
                var (label, cw, ch) = choices[i];
                var chip = new ChipButton
                {
                    Text = label,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Size = new Size(resW, resH),
                    Location = new Point(18 + (i % resCols) * (resW + 10), 62 + (i / resCols) * (resH + 10)),
                    Active = cw == _selectedResW && ch == _selectedResH
                };
                chip.Click += (s, e) =>
                {
                    _selectedResW = cw;
                    _selectedResH = ch;
                    _settings.RustResolutionWidth = cw;
                    _settings.RustResolutionHeight = ch;
                    _store.Save(_settings);
                    foreach (var c in _resChips) c.Active = ReferenceEquals(c, chip);
                };
                _resChips.Add(chip);
                resCard.Controls.Add(chip);
            }
            Controls.Add(resCard);
            y += resCard.Height + 16;

            // ---------- Graphics ----------
            var gfx = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 214) };
            gfx.Controls.Add(UiHelpers.Caption("GRAPHICS QUALITY", 18, 16, 260));
            _selectedQuality = ReadInt(current, "graphics.quality", 3);
            BuildChipRow(gfx, QualityLevels, 18, 42, _qualityChips, _selectedQuality, v => _selectedQuality = v);

            gfx.Controls.Add(UiHelpers.Caption("FPS LIMIT", 18, 92, 240));
            _selectedFps = ReadInt(current, "fps.limit", 0);
            BuildChipRow(gfx, FpsLevels, 18, 118, _fpsChips, _selectedFps, v => _selectedFps = v);

            gfx.Controls.Add(UiHelpers.Caption("FIELD OF VIEW", 18, 164, 240));
            _fovValue = new Label
            {
                Text = ReadInt(current, "graphics.fov", 90).ToString(),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(CardW - 60, 164),
                Size = new Size(42, 16),
                TextAlign = ContentAlignment.MiddleRight
            };
            gfx.Controls.Add(_fovValue);
            // Rust clamps FOV to 60-90; offering more would be a setting the game ignores
            // (and a competitive advantage, which is not what this tool is for).
            _fov = new FlatSlider
            {
                Minimum = 60,
                Maximum = 90,
                Location = new Point(16, 182),
                Width = CardW - 32,
                Value = Math.Clamp(ReadInt(current, "graphics.fov", 90), 60, 90)
            };
            _fov.ValueChanged += (s, e) => _fovValue.Text = _fov.Value.ToString();
            gfx.Controls.Add(_fov);
            Controls.Add(gfx);
            y += 230;

            // ---------- Optimization & tweaks ----------
            var tweaks = RustTweaks.All;
            int cols = 3, chipW = (CardW - 36 - (cols - 1) * 10) / cols, chipH = 34;
            int rows = (tweaks.Count + cols - 1) / cols;
            var tw = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 56 + rows * (chipH + 10)) };
            tw.Controls.Add(UiHelpers.Caption("OPTIMIZATION & TWEAKS", 18, 16, 300));
            for (int i = 0; i < tweaks.Count; i++)
            {
                var tweak = tweaks[i];
                var chip = new ChipButton
                {
                    Text = tweak.Label,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Size = new Size(chipW, chipH),
                    Location = new Point(18 + (i % cols) * (chipW + 10), 46 + (i / cols) * (chipH + 10)),
                    Active = tweak.IsOn(current)
                };
                chip.Click += (s, e) => chip.Active = !chip.Active;
                _tweakChips[tweak] = chip;
                tw.Controls.Add(chip);
            }
            Controls.Add(tw);
            y += tw.Height + 16;

            // ---------- System boost ----------
            var sys = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 210) };
            sys.Controls.Add(UiHelpers.Caption("SYSTEM BOOST", 18, 16, 260));

            sys.Controls.Add(RowLabel("Auto High CPU Priority", 18, 46));
            sys.Controls.Add(RowHint("Raises Rust's scheduling priority once it starts, to reduce micro-stutter.", 18, 64));
            var prio = new ToggleSwitch { Location = new Point(CardW - 62, 48), Checked = _settings.RustHighPriority };
            prio.CheckedChanged += (s, e) => { _settings.RustHighPriority = prio.Checked; _store.Save(_settings); };
            sys.Controls.Add(prio);

            sys.Controls.Add(RowLabel("Auto RAM Cleaner", 18, 96));
            sys.Controls.Add(RowHint("Releases PlexusX's own memory back to Windows before the game starts.", 18, 114));
            var ram = new ToggleSwitch { Location = new Point(CardW - 62, 98), Checked = _settings.RustTrimLauncher };
            ram.CheckedChanged += (s, e) => { _settings.RustTrimLauncher = ram.Checked; _store.Save(_settings); };
            sys.Controls.Add(ram);

            sys.Controls.Add(UiHelpers.Caption("GC OPTIMIZER  (SYSTEM RAM)", 18, 146, 320));
            _selectedRamTier = RustSystemBoost.TierIndexForBuffer(ReadInt(current, "gc.buffer", 2048));
            var tiers = RustSystemBoost.RamTiers;
            for (int i = 0; i < tiers.Length; i++)
            {
                int index = i;
                var chip = new ChipButton
                {
                    Text = tiers[i].Label,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Size = new Size(110, 30),
                    Location = new Point(18 + i * 118, 168),
                    Active = i == _selectedRamTier
                };
                chip.Click += (s, e) =>
                {
                    _selectedRamTier = index;
                    foreach (var c in _ramChips) c.Active = ReferenceEquals(c, chip);
                };
                _ramChips.Add(chip);
                sys.Controls.Add(chip);
            }
            Controls.Add(sys);
            y += 226;

            // ---------- Tools ----------
            var tools = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 92) };
            tools.Controls.Add(UiHelpers.Caption("TOOLS", 18, 16, 200));
            var openFolder = SettingsPage.FlatButton("Game Folder", 18, 44, 150);
            openFolder.Click += (s, e) => Shell(_game.InstallDir);
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
                SetStatus("Restored your original config.", Theme.TextDim);
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
                Text = "Changes are written to client.cfg. A backup is saved automatically.",
                ForeColor = Theme.TextDim,
                Location = new Point(Pad + 194, y + 10),
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
                ["graphics.fov"] = _fov.Value.ToString(),
                ["gc.buffer"] = RustSystemBoost.RamTiers[_selectedRamTier].GcBuffer.ToString(),
            };
            foreach (var (tweak, chip) in _tweakChips)
                tweak.Write(changes, chip.Active);

            try
            {
                _service.Apply(changes);
                SetStatus($"Applied ✓  {changes.Count} settings written (backup saved)", Theme.Accent);
            }
            catch (Exception ex)
            {
                SetStatus("Couldn't write config: " + ex.Message, Color.FromArgb(240, 130, 130));
            }
        }

        /// <summary>Launch Rust, applying whichever system boosts are switched on.</summary>
        private void LaunchRust()
        {
            if (_settings.RustTrimLauncher)
                RustSystemBoost.TrimLauncherMemory();

            string extra = "";
            if (_selectedResW > 0 && _selectedResH > 0 && DisplayController.Current() is DisplayMode original)
            {
                if (original.Width != _selectedResW || original.Height != _selectedResH)
                {
                    if (DisplayController.Apply(_selectedResW, _selectedResH))
                    {
                        // Put the desktop back once Rust closes so nobody is left stretched.
                        DisplayController.RestoreWhenRustExits(original, TimeSpan.FromMinutes(5));
                        extra = $"  ({_selectedResW}x{_selectedResH})";
                    }
                    else
                    {
                        SetStatus($"Your monitor didn't accept {_selectedResW}x{_selectedResH}.",
                            Color.FromArgb(240, 180, 90));
                    }
                }
            }

            Shell($"steam://run/{_game.Game.SteamAppId}");

            if (_settings.RustHighPriority)
                RustSystemBoost.RaisePriorityWhenRustStarts(TimeSpan.FromMinutes(3));

            if (extra.Length > 0 || _status.ForeColor != Color.FromArgb(240, 180, 90))
                SetStatus("Launching Rust…" + extra, Theme.TextDim);
        }

        private static Label RowLabel(string text, int x, int y) => new()
        {
            Text = text,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Location = new Point(x, y),
            AutoSize = true
        };

        private static Label RowHint(string text, int x, int y) => new()
        {
            Text = text,
            ForeColor = Theme.TextDim,
            BackColor = Color.Transparent,
            Font = new Font(Theme.FontFamily, 8f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private void ReloadFrom(RustConfig cfg)
        {
            SelectChip(_qualityChips, ReadInt(cfg, "graphics.quality", 3), v => _selectedQuality = v);
            SelectChip(_fpsChips, ReadInt(cfg, "fps.limit", 0), v => _selectedFps = v);
            _fov.Value = Math.Clamp(ReadInt(cfg, "graphics.fov", 90), 60, 90);
            foreach (var (tweak, chip) in _tweakChips) chip.Active = tweak.IsOn(cfg);

            _selectedRamTier = RustSystemBoost.TierIndexForBuffer(ReadInt(cfg, "gc.buffer", 2048));
            for (int i = 0; i < _ramChips.Count; i++) _ramChips[i].Active = i == _selectedRamTier;
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
