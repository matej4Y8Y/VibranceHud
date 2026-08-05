using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using VibranceHud.Games;
using VibranceHud.Rust;
using VibranceHud.Controls;

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
        /// <summary>Text gutter inside a card; captions, readouts and slider tracks all
        /// line up on it.</summary>
        private const int Gutter = 18;
        private const int ContentW = CardW - 2 * Gutter;

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
        private readonly Audio.AudioEdgeService? _audio;
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

        // NVIDIA Tweaks card removed in v0.9.0 - see
        // docs/design/specs/2026-07-29-remove-nvidia-tweaks.md.

        public RustSettingsPage(DetectedGame game, AppSettings settings, SettingsStore store,
            Audio.AudioEdgeService? audio, Action onBack, IVibranceEngine? engine = null)
        {
            _game = game;
            _settings = settings;
            _store = store;
            _audio = audio;
            _service = new RustSettingsService(Path.Combine(game.InstallDir, "cfg", "client.cfg"));
            AutoScroll = true;
            // Centre the column instead of letting it hug the left edge of a wide window.
            ContentWidth = CardW + 2 * Pad;
            Font = new Font(Theme.FontFamily, 9.5f);
            Padding = new Padding(0, 0, 0, 28);

            var current = _service.ReadCurrent();
            int y = 26;

            // ---------- Header ----------
            // No "‹ Games" link. The app is pointed at one game from the chooser in the nav,
            // so there is no list behind this page to go back to - the link would have led
            // somewhere that no longer exists as a destination.
            Controls.Add(new Label
            {
                Text = "Rust",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(Pad - 2, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var launch = SettingsPage.PrimaryButton("▶  Launch Rust", Pad + CardW - 150, y + 2, 150);
            launch.Click += (s, e) => LaunchRust();
            Controls.Add(launch);
            y += 60;

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
            var presetCard = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 96) };
            presetCard.Controls.Add(UiHelpers.Caption("QUICK PRESETS", 18, 16, 260));
            presetCard.Controls.Add(new Label
            {
                Text = "One click to configure the settings below - then Apply.",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8f),
                Location = new Point(18, 34),
                AutoSize = true
            });
            int ppx = 18;
            foreach (var preset in RustPresets.All)
            {
                var btn = SettingsPage.FlatButton(preset.Name, ppx, 56, 160);
                var p = preset;
                btn.Click += (s, e) => ApplyPreset(p);
                presetCard.Controls.Add(btn);
                ppx += 172;
            }
            Controls.Add(presetCard);
            y += presetCard.Height + 16;

            // Resolution used to be a card here. It moved to the Monitor tab: it is a display
            // setting, not a Rust setting, and having it here meant a CS2 or Apex player could
            // not reach it at all while a Rust player only found it by accident. The launch
            // behaviour is unchanged - the rule now lives in AppSettings.MonitorRules and the
            // profile coordinator applies it for every game, not just this one.

            // ---------- Graphics ----------
            // 232, not 214: at 214 the FOV slider's bottom edge landed exactly on the card's
            // bottom rim, so the thumb sat right on the rounded border with no breathing room.
            var gfx = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 232) };
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
                Value = Math.Clamp(ReadInt(current, "graphics.fov", 90), 60, 90)
            };
            _fov.SetTrackBounds(Gutter, 184, ContentW);
            _fov.ValueChanged += (s, e) => _fovValue.Text = _fov.Value.ToString();
            gfx.Controls.Add(_fov);
            Controls.Add(gfx);
            y += gfx.Height + 16;

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

            // NVIDIA driver tweaks removed in v0.9.0 - the NVAPI path didn't
            // work on the user's hardware (driver mismatch + DRS writes need
            // admin). See docs/design/specs/2026-07-29-remove-nvidia-tweaks.md.

            // ---------- Audio Edge ----------
            if (_audio != null)
            {
                var audioCard = new CardPanel { Location = new Point(Pad, y), Size = new Size(CardW, 168) };
                audioCard.Controls.Add(UiHelpers.Caption("AUDIO EDGE", 18, 16, 260));
                audioCard.Controls.Add(RowLabel("Loudness limiter", 18, 44));
                audioCard.Controls.Add(RowHint(
                    "Caps how loud anything can get. Turn your game up and gun shots stay at the ceiling, " +
                    "so footsteps come through at nearly the same level.", 18, 62));

                var audioToggle = new ToggleSwitch
                {
                    Location = new Point(CardW - 62, 46),
                    Checked = _settings.AudioEdgeEnabled
                };
                audioCard.Controls.Add(audioToggle);

                audioCard.Controls.Add(UiHelpers.Caption("CEILING", 18, 100, 200));
                var ceilingValue = new Label
                {
                    Text = $"{_settings.AudioEdgeThresholdPercent}%",
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Location = new Point(CardW - 60, 100),
                    Size = new Size(42, 16),
                    TextAlign = ContentAlignment.MiddleRight
                };
                audioCard.Controls.Add(ceilingValue);

                var ceiling = new FlatSlider
                {
                    Minimum = 5,
                    Maximum = 100,
                    Value = Math.Clamp(_settings.AudioEdgeThresholdPercent, 5, 100)
                };
                ceiling.SetTrackBounds(Gutter, 122, ContentW);
                ceiling.ValueChanged += (s, e) =>
                {
                    ceilingValue.Text = $"{ceiling.Value}%";
                    _settings.AudioEdgeThresholdPercent = ceiling.Value;
                    _audio.Threshold = ceiling.Value / 100f;
                    _store.Save(_settings);
                };
                audioCard.Controls.Add(ceiling);

                audioToggle.CheckedChanged += (s, e) =>
                {
                    _settings.AudioEdgeEnabled = audioToggle.Checked;
                    _store.Save(_settings);
                    _audio.Threshold = ceiling.Value / 100f;
                    if (audioToggle.Checked) _audio.Start(); else _audio.Stop();
                    SetStatus(audioToggle.Checked
                        ? $"Audio Edge on — ceiling {ceiling.Value}%"
                        : "Audio Edge off — your volume is back to normal.", Theme.TextDim);
                };

                Controls.Add(audioCard);
                y += audioCard.Height + 16;
            }

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
            var apply = SettingsPage.PrimaryButton("Apply Changes", Pad, y, 180, height: 38);
            apply.Click += (s, e) => Apply();
            Controls.Add(apply);

            _status = new Label
            {
                Text = "Changes are written to client.cfg. A backup is saved automatically.",
                ForeColor = Theme.TextDim,
                Location = new Point(Pad + 194, y + 10),
                AutoSize = true,
                // Holds raw exception text on failure - uncapped it ran off the page.
                MaximumSize = new Size(CardW - 194, 0),
                BackColor = Color.Transparent
            };
            Controls.Add(_status);
        }

        // ---------- NVIDIA Tweaks card builder ----------
// Builds (or rebuilds) the NVIDIA Tweaks card. After a Scan, the row list is
// recomputed from the cached supported set in AppSettings; without a scan,
// the user sees all tier-allowed toggles (the pre-scan fallback).
        /// <summary>Set every control to the preset's loadout and write it in one click.</summary>
        private void ApplyPreset(RustPreset preset)
        {
            SelectChip(_qualityChips, preset.Quality, v => _selectedQuality = v);
            SelectChip(_fpsChips, preset.Fps, v => _selectedFps = v);
            _fov.Value = Math.Clamp(preset.Fov, 60, 90);
            foreach (var (tweak, chip) in _tweakChips)
                chip.Active = preset.TweaksOn.Contains(tweak.Label);
            Apply();
        }

        private void Apply()
        {
            if (RustSettingsService.IsRustRunning())
            {
                var proceed = GlassDialog.Show(FindForm(), "Rust is running",
                    "Rust will overwrite these changes when it exits.\n\n" +
                    "Close Rust first if you want them to stick. Apply anyway?",
                    GlassDialogButtons.YesNo, GlassDialogTone.Warning);
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

        /// <summary>
        /// Width a row's text may use before it has to wrap: the card's content column minus
        /// the toggle parked on the right and a gap.
        ///
        /// AutoSize on its own grows a Label to one endless line, and a Label just clips at
        /// its parent's edge - so the Audio Edge hint ran off the side of the card and stopped
        /// mid-word ("...at nearly the same lev"). Nothing warns you; it only shows up by
        /// looking at it.
        /// </summary>
        private const int RowTextW = ContentW - 56;

        private static Label RowLabel(string text, int x, int y) => new()
        {
            Text = text,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Location = new Point(x, y),
            AutoSize = true,
            MaximumSize = new Size(RowTextW, 0),
        };

        private static Label RowHint(string text, int x, int y) => new()
        {
            Text = text,
            ForeColor = Theme.TextDim,
            BackColor = Color.Transparent,
            Font = new Font(Theme.FontFamily, 8f),
            Location = new Point(x, y),
            AutoSize = true,
            MaximumSize = new Size(RowTextW, 0),
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
