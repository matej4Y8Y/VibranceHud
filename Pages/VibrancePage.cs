using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Controls;

namespace VibranceHud.Pages
{
    /// <summary>
    /// The Display page.
    ///
    /// Layout: a header band, then one card holding PRIMARY (saturation and vibrance, at the
    /// large size), FINE TUNE (brightness/gamma/contrast/temperature in a 2x2 grid), SCENE
    /// PRESETS (compact chips) and SHORTCUTS.
    ///
    /// Sliders come before presets on purpose. The presets are a shortcut to a slider
    /// position, so putting them first pushed the thing they are a shortcut to below the
    /// fold, and the page opened on decoration instead of on its own controls.
    ///
    /// Every visible thing on this page is a real child control. Nothing is painted at
    /// absolute coordinates in OnPaint, and that is the whole point: the page scrolls, and a
    /// scrolled page moves its children while leaving painted text exactly where it was. The
    /// previous version mixed the two and every caption ended up sitting on top of its own
    /// slider the moment a scrollbar appeared. Controls all move together, so the bug has
    /// nowhere left to live.
    ///
    /// The card's height comes from where its contents actually finish, so a longer section
    /// or a wider font pushes the card out rather than off the bottom of it.
    /// </summary>
    public sealed class VibrancePage : GlowPage
    {
        private const int SaveDebounceMs = 500;

        // Logical pixels, resolved at the current DPI. Properties rather than consts so a
        // window moved to a differently-scaled monitor re-lays out at the new size instead
        // of keeping the pixel counts it started with.
        private static int PageMargin => Design.Tokens.Scale(28);
        private static int CardPad => Design.Tokens.Scale(Design.Tokens.XL);
        private static int SectionGap => Design.Tokens.Scale(26);
        private static int ColGap => Design.Tokens.Scale(28);
        private static int ChipGap => Design.Tokens.Scale(Design.Tokens.M);
        /// <summary>Glyph and name on one line, the preset's own colour swatch beneath.</summary>
        private static int ChipHeight => Design.Tokens.Scale(74);
        private static int SectionLabelH => Design.Tokens.Scale(22);

        /// <summary>Width of the FINE TUNE section's Reset button.</summary>
        private static int ResetW => Design.Tokens.Scale(76);
        private static int ResetH => Design.Tokens.Scale(26);

        private readonly VibranceEngine _engine;
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly DebouncedAction _saveDebounce;

        private readonly CardPanel _card;
        private readonly Label _title, _subtitle;
        private readonly Label _presetsLabel, _primaryLabel, _fineLabel, _shortcutsLabel;
        private readonly Label _popupHotkeyLabel, _mainHotkeyLabel;

        private readonly SliderRow _saturation, _vibrance, _brightness, _gamma, _contrast, _temperature;
        private readonly HotkeyPicker _hotkeyPicker, _mainHotkeyPicker;

        private readonly GlassButton _resetFine;

        // ---- before / after ----
        //
        // The demo moment for a product whose promise is "your game looks way better": the
        // effect is invisible until you can see what it replaced.
        private readonly GlassButton _compare;
        private readonly Display.AbCompare _ab = new();

        // ---- advanced grade ----
        //
        // The engine has carried a full three-way corrector for a while - highlights,
        // shadows, whites, blacks, fade and split toning, all resolving to the same gamma
        // ramp - and it was saved, shared in profiles and applied, with no way to reach it.
        // Collapsed by default, because the six controls above are the front door.
        private readonly Label _advancedLabel;
        private readonly GlassButton _advancedToggle;
        private readonly SliderRow _highlights, _shadows, _whites, _blacks, _fade;
        private readonly SliderRow _shadowTint, _midTint, _highTint;
        private bool _advancedOpen;

        /// <summary>Set once the constructor has finished. LayoutContent touches every field
        /// on the page, so anything that calls it during construction dereferences whatever
        /// has not been assigned yet.</summary>
        private bool _built;


        // ---- game presets ----
        //
        // Pick a game, get looks tuned to its palette, hover one to see it at a readable size
        // before committing. This is what replaced the Games Hub: the Hub tried to be a worse
        // copy of each game's settings menu, and this does the thing no config file can.
        private readonly Label _gamePresetsLabel;
        private readonly GlassDropdown _gamePicker;
        private readonly PresetPreviewPanel _presetPreview;
        private readonly List<PresetTile> _tiles = new();

        // ---- share ----
        private readonly Label _shareLabel, _shareHint, _shareStatus;
        private readonly GlassTextBox _codeBox;
        private readonly GlassButton _copyCode, _applyCode;
        private readonly List<PresetChip> _chips = new();
        private readonly List<DisplayPreset> _presets = new();
        private readonly ToolTip _presetTips = new() { InitialDelay = 350, ReshowDelay = 120 };

        private bool _applyingPreset;
        private bool _dragging;

        public event Func<uint, uint, bool>? HotkeyChanged;
        public event Action<uint, uint, bool>? MainHotkeyChanged;

        public VibrancePage(VibranceEngine engine, AppSettings settings, SettingsStore store)
        {
            _engine = engine;
            _settings = settings;
            _store = store;
            _saveDebounce = new DebouncedAction(() => _store.Save(_settings), SaveDebounceMs);

            AutoScroll = true;
            Font = Design.Fonts.Label;

            _title = PageLabel("Display", Design.Fonts.Display, Theme.Text);
            _subtitle = PageLabel("Pick a scene, then fine-tune it.",
                Design.Fonts.Label, Theme.TextDim);

            _card = new CardPanel();
            Controls.Add(_card);

            // ---- advanced ----
            //
            // Every one of these is a field of ToneSettings, which the engine already applies
            // through ToneCurve. Nothing new is computed here; this is the missing UI for a
            // grade that has been shipping invisibly.
            _advancedLabel = SectionLabel("ADVANCED");
            _advancedToggle = new GlassButton { Text = "Show", Kind = GlassButtonKind.Ghost };
            _advancedToggle.Click += (_, _) => SetAdvancedOpen(!_advancedOpen);
            _card.Controls.Add(_advancedToggle);

            _highlights = ToneRow("Highlights", v => _engine.Tone = _engine.Tone with { Highlights = v },
                _engine.Tone.Highlights);
            _shadows = ToneRow("Shadows", v => _engine.Tone = _engine.Tone with { Shadows = v },
                _engine.Tone.Shadows);
            _whites = ToneRow("Whites", v => _engine.Tone = _engine.Tone with { Whites = v },
                _engine.Tone.Whites);
            _blacks = ToneRow("Blacks", v => _engine.Tone = _engine.Tone with { Blacks = v },
                _engine.Tone.Blacks);

            // Fade is the one that only runs one way - it lifts the black point toward grey,
            // and there is no meaningful negative of that.
            _fade = Row("Fade", 0, 100, 0, _engine.Tone.Fade,
                v => { _engine.Tone = _engine.Tone with { Fade = v }; _settings.Tone = _engine.Tone; },
                palette: SliderPalette.Luminance, format: v => $"{v}%");

            _shadowTint = ToneRow("Shadow tint", v => _engine.Tone = _engine.Tone with { ShadowTint = v },
                _engine.Tone.ShadowTint);
            _midTint = ToneRow("Midtone tint", v => _engine.Tone = _engine.Tone with { MidtoneTint = v },
                _engine.Tone.MidtoneTint);
            _highTint = ToneRow("Highlight tint", v => _engine.Tone = _engine.Tone with { HighlightTint = v },
                _engine.Tone.HighlightTint);

            Explain(_highlights, "The brightest parts only. Pull it down to recover detail in a blown-out sky.");
            Explain(_shadows, "The darkest parts only. Lift it to see into corners without washing the whole picture out.");
            Explain(_whites, "Where white begins. Lower it and more of the picture counts as fully bright.");
            Explain(_blacks, "Where black begins. Raise it and more of the picture counts as fully dark.");
            Explain(_fade, "Lifts black toward grey for a softer, filmic look. Zero is off.");
            Explain(_shadowTint, "Colours the dark parts. Cool to the left, warm to the right.");
            Explain(_midTint, "Colours the midtones, which is most of what you look at.");
            Explain(_highTint, "Colours the bright parts, including highlights on skin and metal.");

            // Open if the saved grade is actually using any of this. A non-neutral grade
            // behind a collapsed section is a screen that looks wrong with no visible cause.
            SetAdvancedOpen(!_engine.Tone.IsGammaOnly);

            // ---- game presets ----
            _gamePresetsLabel = SectionLabel("PRESETS FOR YOUR GAME");

            _gamePicker = new GlassDropdown { Size = new Size(200, 34) };
            _gamePicker.SetItems(Display.GameColourPresets.All.Select(g => g.Game));

            // Opens on the game the app has actually detected, rather than always on the first
            // entry. Somebody who plays CS2 should not have to tell the app that every launch.
            string detected = _settings.FavoriteGame ?? "";
            int match = Display.GameColourPresets.All
                .ToList()
                .FindIndex(g => string.Equals(g.Game, detected, StringComparison.OrdinalIgnoreCase));
            if (match >= 0) _gamePicker.SelectedIndex = match;

            _gamePicker.SelectedIndexChanged += (_, _) => RebuildGameTiles();
            _card.Controls.Add(_gamePicker);

            _presetPreview = new PresetPreviewPanel();
            _card.Controls.Add(_presetPreview);

            RebuildGameTiles();

            _presetsLabel = SectionLabel("SCENE PRESETS");
            foreach (var preset in DisplayPresets.All)
            {
                var captured = preset;
                var chip = new PresetChip
                {
                    Caption = preset.Name,
                    Subtitle = preset.Subtitle,
                    Kind = preset.Name.ToLowerInvariant(),
                    Photo = BrandAssets.PresetChip(preset.Name.ToLowerInvariant()),
                    // The chip previews the preset by running a grey ramp through the very
                    // matrix the overlay would apply, so the swatch can never drift from
                    // what the preset actually does.
                    Matrix = ColorAdjust.Build(
                        saturation: 1f,
                        vibrance: 1f,
                        contrast: preset.Contrast / 100f,
                        brightness: preset.Brightness / 100f,
                        warmth: preset.Temperature / 100f),
                };
                chip.Click += (_, _) => ApplyPreset(captured);
                // The compact chip only has room for the name, so what each look actually
                // does lives on hover rather than being dropped.
                _presetTips.SetToolTip(chip, preset.Subtitle);
                _presets.Add(preset);
                _chips.Add(chip);
                _card.Controls.Add(chip);
            }

            // The two headline controls, at the large size. Everything else on the page is
            // trim for these two.
            _primaryLabel = SectionLabel("PRIMARY");
            _saturation = Row("Saturation", 0, VibranceEngine.MaxSaturation, 100, _engine.Saturation,
                v => { _engine.Saturation = v; _settings.SaturationPercent = v; }, large: true,
                format: v => $"{v}%");
            _vibrance = Row("Vibrance", 0, VibranceEngine.MaxVibrance,
                VibranceEngine.DriverVibranceCeiling, _engine.Vibrance,
                v => { _engine.Vibrance = v; _settings.VibrancePercent = v; }, large: true,
                // Without a driver this says why instead of showing a number that does
                // nothing - "no NVIDIA GPU" was the wrong answer on every gaming laptop.
                format: v => VibranceStatus.Readout(_engine.DriverState, v));

            // Getting back to neutral meant dragging four sliders and knowing what neutral
            // was for each - 100, 100, 100, and 0, which is not something anyone should have
            // to remember. Presets can reach neutral too, but only via "Balanced", which
            // reads like a look rather than an undo.
            _compare = new GlassButton { Text = "Compare", Kind = GlassButtonKind.Ghost };
            _compare.Click += (_, _) => ToggleCompare();
            _card.Controls.Add(_compare);
            _presetTips.SetToolTip(_compare,
                "Hold your look against neutral, so you can see what it is actually doing.");

            _fineLabel = SectionLabel("FINE TUNE");
            _resetFine = new GlassButton { Text = "Reset", Kind = GlassButtonKind.Ghost };
            _resetFine.Click += (_, _) => ResetFineTune();
            _card.Controls.Add(_resetFine);
            // Each ramp is chosen to mean the control it belongs to: temperature really does
            // run blue to amber, brightness really does run dark to light. The slider ends up
            // describing itself.
            _brightness = Row("Brightness", VibranceEngine.MinBrightness, VibranceEngine.MaxBrightness,
                100, _engine.Brightness,
                v => { _engine.Brightness = v; _settings.BrightnessPercent = v; },
                palette: SliderPalette.Luminance, format: v => $"{v}%");
            _gamma = Row("Gamma", VibranceEngine.MinGamma, VibranceEngine.MaxGamma, 100, _engine.Gamma,
                v => { _engine.Gamma = v; _settings.GammaPercent = v; },
                // Invariant, so the decimal point stays a point. The app is English
                // throughout, but the format picked up the machine's locale - on a Czech
                // Windows the one number on the page with a decimal read "1,00".
                palette: SliderPalette.Luminance,
                format: v => (v / 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            _contrast = Row("Contrast", VibranceEngine.MinContrast, VibranceEngine.MaxContrast,
                100, _engine.Contrast,
                v => { _engine.Contrast = v; _settings.ContrastPercent = v; },
                palette: SliderPalette.Contrast, format: v => $"{v}%");
            _temperature = Row("Temperature", VibranceEngine.MinTemperature, VibranceEngine.MaxTemperature,
                0, _engine.Temperature,
                v => { _engine.Temperature = v; _settings.Temperature = v; },
                palette: SliderPalette.Temperature, format: TemperatureText);

            _shortcutsLabel = SectionLabel("SHORTCUTS");
            _popupHotkeyLabel = CardLabel("Quick colour popup");
            _mainHotkeyLabel = CardLabel("Open main window");

            _hotkeyPicker = new HotkeyPicker
            {
                ModifierMask = _settings.HotkeyModifierMask,
                VirtualKey = _settings.HotkeyVirtualKey,
            };
            _hotkeyPicker.HotkeyChanged += (mask, vk) =>
            {
                _settings.HotkeyModifierMask = mask;
                _settings.HotkeyVirtualKey = vk;
                _store.Save(_settings);
                // Report back whether it actually bound, so a combo another app owns is said
                // where the user is looking instead of only in the tray menu.
                _hotkeyPicker.ReportBindingResult(HotkeyChanged?.Invoke(mask, vk) ?? true);
            };
            _card.Controls.Add(_hotkeyPicker);

            _mainHotkeyPicker = new HotkeyPicker
            {
                ModifierMask = _settings.MainHotkeyModifierMask,
                VirtualKey = _settings.MainHotkeyEnabled ? _settings.MainHotkeyVirtualKey : 0,
            };
            _mainHotkeyPicker.HotkeyChanged += (mask, vk) =>
            {
                _settings.MainHotkeyModifierMask = mask;
                _settings.MainHotkeyVirtualKey = vk;
                _settings.MainHotkeyEnabled = true;
                _store.Save(_settings);
                MainHotkeyChanged?.Invoke(mask, vk, true);
            };
            _card.Controls.Add(_mainHotkeyPicker);

            // ---- share ----
            //
            // Lives here rather than at the bottom of Settings, where it used to be. It
            // describes the sliders on this page, and nobody was finding it three cards down
            // a different tab - which matters, because a code passed between friends is how
            // the app spreads.
            _shareLabel = SectionLabel("SHARE");
            _shareHint = CardLabel("Paste a friend's code to get their exact look, or copy yours to send.");

            _codeBox = new GlassTextBox
            {
                CharacterCasing = CharacterCasing.Upper,
                // An empty monospace box next to a button called Apply says nothing about
                // what belongs in it.
                PlaceholderText = "PX-XXXXXXXXX",
            };
            _codeBox.Inner.Font = new Font("Consolas", 10f);
            _card.Controls.Add(_codeBox);

            _copyCode = new GlassButton { Text = "Copy my code", Kind = GlassButtonKind.Ghost };
            _copyCode.Click += (_, _) => CopyMyCode();
            _card.Controls.Add(_copyCode);

            _applyCode = new GlassButton { Text = "Apply", Kind = GlassButtonKind.Primary };
            _applyCode.Click += (_, _) => ApplyCode();
            _card.Controls.Add(_applyCode);

            _shareStatus = CardLabel("");

            // What each control actually does. Every slider on this page had a one-word
            // caption and nothing else - "Gamma" tells somebody who already knows what gamma
            // is precisely nothing they did not know, and everybody else nothing at all.
            Explain(_saturation, "How strong every colour is. Push it too far and detail turns to blocks.");
            Explain(_vibrance, "Boosts dull colours and mostly leaves skin tones alone. Subtler than saturation.");
            Explain(_brightness, "Lifts or lowers the whole picture. Bright areas clip to white if you push it.");
            Explain(_gamma, "Midtones only — brightens shadows without washing out the highlights.");
            Explain(_contrast, "Distance between the darkest and lightest parts.");
            Explain(_temperature, "Warm pushes toward orange, cool toward blue. Neutral is untouched.");
            _presetTips.SetToolTip(_codeBox, "A code looks like PX-5C563R3P3M564. Paste one and press Apply.");
            _presetTips.SetToolTip(_copyCode, "Puts your exact look on the clipboard as a short code.");
            _presetTips.SetToolTip(_applyCode, "Switch to the look in the box. A wrong code changes nothing.");
            _presetTips.SetToolTip(_resetFine, "Put brightness, gamma, contrast and temperature back to neutral.");

            RefreshReadouts();
            UpdateActiveChip();

            Resize += (_, _) => LayoutContent();
            HandleCreated += (_, _) => LayoutContent();

            _built = true;
        }

        // ---- construction helpers ----------------------------------------------------

        private Label PageLabel(string text, Font font, Color colour)
        {
            var l = new Label
            {
                Text = text,
                Font = font,
                ForeColor = colour,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            Controls.Add(l);
            return l;
        }

        /// <summary>Attach the same explanation to a row's slider and its caption, so hovering
        /// anywhere on the row shows it rather than only the thin track.</summary>
        private void Explain(SliderRow row, string text)
        {
            _presetTips.SetToolTip(row.Slider, text);
            row.SetToolTip(_presetTips, text);
        }

        private Label SectionLabel(string text)
        {
            var l = new Label
            {
                Text = UiHelpers.Spaced(text),
                Font = Design.Fonts.Micro,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _card.Controls.Add(l);
            return l;
        }

        private Label CardLabel(string text)
        {
            var l = new Label
            {
                Text = text,
                Font = Design.Fonts.Label,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _card.Controls.Add(l);
            return l;
        }

        /// <summary>Build one slider row and wire it to the engine, the settings file and the
        /// preset highlight in one place, so no row can quietly forget one of the three.</summary>
        /// <summary>
        /// One advanced row. They all share the same shape - centred on zero, -100 to 100,
        /// signed readout - so the only thing that differs is which field of ToneSettings it
        /// writes and what it is called.
        /// </summary>
        private SliderRow ToneRow(string caption, Action<int> apply, int value) =>
            Row(caption, -100, 100, 0, value,
                v => { apply(v); _settings.Tone = _engine.Tone; },
                palette: SliderPalette.Luminance,
                // Signed, so "which side of neutral am I on" is answerable without looking at
                // the thumb. A bare "-40" reads as a measurement; "+40" reads as a choice.
                format: v => v > 0 ? "+" + v : v.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// Show or hide the advanced rows.
        ///
        /// Collapsed by default. The six controls above are the front door, and eight more
        /// sliders opening onto them turns the page into a mixing desk. Anything non-neutral
        /// forces it open on load, or a saved grade would be invisible and unexplainable.
        /// </summary>
        private void SetAdvancedOpen(bool open)
        {
            _advancedOpen = open;
            _advancedToggle.Text = open ? "Hide" : "Show";

            foreach (var row in AdvancedRows) row.Visible = open;

            if (_built) LayoutContent();
        }

        /// <summary>
        /// Flip between the user's look and neutral.
        ///
        /// Writes straight to the engine and deliberately does NOT move the sliders or save
        /// anything: neutral is a preview, not a change. If it touched the sliders, a compare
        /// interrupted by closing the window would persist as the user's settings.
        /// </summary>
        /// <summary>
        /// Flip between the user's look and neutral.
        ///
        /// The state lives on the engine, not here. This page can be destroyed while a
        /// comparison is running - a theme switch rebuilds the whole window, and the quick
        /// colour popup opens over the top from a global hotkey - and when the page held the
        /// only copy of the user's settings, either of those threw it away while the engine
        /// sat at neutral. The next slider nudge then saved neutral over their look for good.
        ///
        /// Because the engine only gates what it applies, its getters still report the real
        /// values, so nothing else has to know a comparison is happening.
        /// </summary>
        private void ToggleCompare()
        {
            if (!_ab.TryToggle()) return;   // inside the cooldown - do nothing at all

            _engine.PreviewNeutral(_ab.ShowingNeutral);
            _compare.Text = _ab.ShowingNeutral ? "Showing off" : "Compare";
        }

        private void RestoreFromCompare()
        {
            _ab.Reset();
            _engine.PreviewNeutral(false);
            _compare.Text = "Compare";
        }

        /// <summary>
        /// Leaving the page must not strand the user on neutral - their look would appear to
        /// have been wiped, with nothing on screen explaining why. Reset is not rate-limited,
        /// so this can never be refused.
        /// </summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible && _engine.IsPreviewingNeutral) RestoreFromCompare();
        }

        /// <summary>
        /// Rebuild the tiles for whichever game is selected.
        ///
        /// Torn down and rebuilt rather than reused: the groups have different lengths, and a
        /// pool of recycled tiles showing the wrong preset is a far worse bug than the cost of
        /// making six controls.
        /// </summary>
        private void RebuildGameTiles()
        {
            foreach (var tile in _tiles)
            {
                _card.Controls.Remove(tile);
                tile.Dispose();
            }
            _tiles.Clear();

            int index = Math.Max(0, _gamePicker.SelectedIndex);
            var group = Display.GameColourPresets.All[
                Math.Min(index, Display.GameColourPresets.All.Count - 1)];

            // The tiles need to know what this machine will actually do, or they preview a
            // driver contribution that only exists on NVIDIA.
            bool driver = _engine.DriverState == VibranceDriverState.Available;
            _presetPreview.DriverAvailable = driver;

            foreach (var preset in group.Presets)
            {
                var tile = new PresetTile(preset, driver);
                var captured = preset;

                tile.Click += (_, _) => ApplyGamePreset(captured);

                // Hovering shows it larger; leaving falls back to whatever is applied, so the
                // panel is never blank once something has been chosen.
                tile.HoverChanged += (_, _) => _presetPreview.Preset =
                    _tiles.FirstOrDefault(t => t.Hovered)?.Preset
                    ?? _tiles.FirstOrDefault(t => t.Active)?.Preset;

                _tiles.Add(tile);
                _card.Controls.Add(tile);
            }

            _presetPreview.Preset = _tiles.FirstOrDefault(t => t.Active)?.Preset;

            if (_built) LayoutContent();
        }

        /// <summary>
        /// Apply a preset to every channel it covers.
        ///
        /// Goes through the sliders rather than straight to the engine, so the page and the
        /// screen cannot disagree - the same reason the scene presets do it this way.
        /// </summary>
        private void ApplyGamePreset(Display.ColourPreset preset)
        {
            // Same reason as the sliders: picking a preset is an edit, and an edit ends the
            // comparison rather than racing it.
            if (_engine.IsPreviewingNeutral) RestoreFromCompare();

            _applyingPreset = true;
            try
            {
                _vibrance.Slider.Value = Math.Clamp(preset.Vibrance,
                    _vibrance.Slider.Minimum, _vibrance.Slider.Maximum);
                _saturation.Slider.Value = Math.Clamp(preset.Saturation,
                    _saturation.Slider.Minimum, _saturation.Slider.Maximum);
                _brightness.Slider.Value = Math.Clamp(preset.Brightness,
                    _brightness.Slider.Minimum, _brightness.Slider.Maximum);
                _contrast.Slider.Value = Math.Clamp(preset.Contrast,
                    _contrast.Slider.Minimum, _contrast.Slider.Maximum);
                _temperature.Slider.Value = Math.Clamp(preset.Temperature,
                    _temperature.Slider.Minimum, _temperature.Slider.Maximum);
                _gamma.Slider.Value = Math.Clamp(preset.Tone.ResolvedGamma,
                    _gamma.Slider.Minimum, _gamma.Slider.Maximum);

                // The advanced channels have no sliders of their own in the collapsed state,
                // so they go to the engine directly and the rows are synced afterwards.
                _engine.Tone = preset.Tone with { Gamma = preset.Tone.ResolvedGamma };
                _settings.Tone = _engine.Tone;

                SyncAdvancedRows();
            }
            finally { _applyingPreset = false; }

            foreach (var tile in _tiles) tile.Active = ReferenceEquals(tile.Preset, preset);
            _presetPreview.Preset = preset;

            // A preset that touches the advanced channels has to show them, or the page is
            // hiding the reason the screen changed.
            if (!_engine.Tone.IsGammaOnly && !_advancedOpen) SetAdvancedOpen(true);

            _store.Save(_settings);
            UpdateActiveChip();
        }

        /// <summary>Move the advanced rows to whatever the engine's grade now holds.</summary>
        private void SyncAdvancedRows()
        {
            var t = _engine.Tone;
            _highlights.Slider.Value = Math.Clamp(t.Highlights, -100, 100);
            _shadows.Slider.Value = Math.Clamp(t.Shadows, -100, 100);
            _whites.Slider.Value = Math.Clamp(t.Whites, -100, 100);
            _blacks.Slider.Value = Math.Clamp(t.Blacks, -100, 100);
            _fade.Slider.Value = Math.Clamp(t.Fade, 0, 100);
            _shadowTint.Slider.Value = Math.Clamp(t.ShadowTint, -100, 100);
            _midTint.Slider.Value = Math.Clamp(t.MidtoneTint, -100, 100);
            _highTint.Slider.Value = Math.Clamp(t.HighlightTint, -100, 100);
        }

        private SliderRow[] AdvancedRows => new[]
        {
            _highlights, _shadows, _whites, _blacks, _fade, _shadowTint, _midTint, _highTint,
        };

        private SliderRow Row(string caption, int min, int max, int? notch, int value,
            Action<int> apply, bool large = false, SliderPalette? palette = null,
            Func<int, string>? format = null)
        {
            var row = new SliderRow(_card, caption, min, max, notch, value, large, palette, format);

            row.Slider.ValueChanged += (_, _) =>
            {
                // Touching anything ends the comparison. Otherwise an edit made while neutral
                // is showing gets written to settings, then silently overwritten on screen the
                // next time Compare is pressed - the slider, the file and the screen all end
                // up saying different things.
                if (_engine.IsPreviewingNeutral && !_applyingPreset) RestoreFromCompare();

                apply(row.Slider.Value);
                _saveDebounce.Trigger();

                // The chip highlight is NOT recomputed here. It compares four values against
                // four presets and can invalidate four transparent chips, all of which repaint
                // the card's glass underneath them - per mouse-move, to answer a question whose
                // answer only matters once the user has stopped moving. Settled on DragEnd.
                if (!_dragging && !_applyingPreset) UpdateActiveChip();
            };

            // Suppress overlay writes for the duration of a drag: the thumb tracks the cursor
            // from WinForms' own repaint, and the screen catches up in one write on release.
            row.Slider.DragBegin += (_, _) => { _dragging = true; _engine.BeginDrag(); };
            row.Slider.DragEnd += (_, _) =>
            {
                _dragging = false;
                _engine.EndDrag();
                UpdateActiveChip();
            };
            return row;
        }

        // ---- layout ------------------------------------------------------------------

        private void LayoutContent()
        {
            if (Width <= 0) return;

            // Capped, not just floored. This page stretches its card to the window, which was
            // right while the window was welded at 1040px. Once it could be dragged wide, the
            // two-column grid stretched with it and a slider's readout ended up hundreds of
            // pixels from its own caption - a long way to look to read one number.
            // No scrollbar width reserved: GlowPage hides the native bars, so that gutter
            // would just be an unexplained gap down the right-hand side.
            int available = Width - 2 * PageMargin;
            int cardW = Math.Clamp(available, Design.Tokens.Scale(520), Design.Tokens.Scale(980));
            int innerW = cardW - 2 * CardPad;

            // Centre whatever is left over, so a wide window frames the card instead of
            // leaving it against the left edge.
            int leftMargin = PageMargin + Math.Max(0, (available - cardW) / 2);
            int colW = (innerW - ColGap) / 2;
            int leftX = CardPad;
            int rightX = CardPad + colW + ColGap;

            // Title box is sized from the font, not from a number that happened to fit the
            // old one. At 30px the 'p' and 'y' descenders in "Display" were being sliced off.
            _title.SetBounds(leftMargin, Design.Tokens.Scale(16), cardW, Design.Tokens.Scale(38));
            _subtitle.SetBounds(leftMargin, Design.Tokens.Scale(56), cardW, Design.Tokens.Scale(22));

            int cardTop = Design.Tokens.Scale(86);
            int y = CardPad;

            // ---- primary: the two headline sliders, side by side and larger ----
            //
            // The label stops short of Compare for the same reason FINE TUNE stops short of
            // Reset: a transparent label added to the card first paints over any button
            // sitting inside its bounds, which is how Reset once rendered as an empty outline.
            _primaryLabel.SetBounds(leftX, y, innerW - ResetW - Design.Tokens.Scale(Design.Tokens.S), SectionLabelH);
            _compare.SetBounds(leftX + innerW - ResetW, y - Design.Tokens.Scale(4), ResetW, ResetH);
            y += SectionLabelH + Design.Tokens.Scale(8);
            _saturation.Place(leftX, y, colW);
            _vibrance.Place(rightX, y, colW);
            y += SliderRow.LargeRowHeight + SectionGap;

            // ---- fine tune: 2x2 of the smaller controls ----
            //
            // The label stops short of the Reset button rather than running the full inner
            // width. It used to span the whole row with the button sitting inside it, and
            // because the label is transparent and added to the card first, it painted over
            // the button - which is why Reset showed up as an empty outline with no text.
            _fineLabel.SetBounds(leftX, y, innerW - ResetW - Design.Tokens.Scale(Design.Tokens.S), SectionLabelH);
            _resetFine.SetBounds(leftX + innerW - ResetW, y - Design.Tokens.Scale(4), ResetW, ResetH);
            y += SectionLabelH + Design.Tokens.Scale(6);
            _brightness.Place(leftX, y, colW);
            _gamma.Place(rightX, y, colW);
            y += SliderRow.RowHeight;
            _contrast.Place(leftX, y, colW);
            _temperature.Place(rightX, y, colW);
            y += SliderRow.RowHeight + SectionGap;

            // ---- advanced: four more 2x2 rows, hidden until asked for ----
            //
            // The label stops short of its toggle for the same reason FINE TUNE does: a
            // transparent label added to the card first paints straight over a button that
            // sits inside its bounds.
            _advancedLabel.SetBounds(leftX, y, innerW - ResetW - Design.Tokens.Scale(Design.Tokens.S), SectionLabelH);
            _advancedToggle.SetBounds(leftX + innerW - ResetW, y - Design.Tokens.Scale(4), ResetW, ResetH);
            y += SectionLabelH + Design.Tokens.Scale(6);

            if (_advancedOpen)
            {
                _highlights.Place(leftX, y, colW);
                _shadows.Place(rightX, y, colW);
                y += SliderRow.RowHeight;
                _whites.Place(leftX, y, colW);
                _blacks.Place(rightX, y, colW);
                y += SliderRow.RowHeight;
                _fade.Place(leftX, y, colW);
                _shadowTint.Place(rightX, y, colW);
                y += SliderRow.RowHeight;
                _midTint.Place(leftX, y, colW);
                _highTint.Place(rightX, y, colW);
                y += SliderRow.RowHeight;
            }

            y += SectionGap;

            // ---- game presets: a picker, a row of tiles, and the larger preview ----
            _gamePresetsLabel.SetBounds(leftX, y, innerW - Design.Tokens.Scale(210), SectionLabelH);
            _gamePicker.SetBounds(leftX + innerW - Design.Tokens.Scale(200), y - Design.Tokens.Scale(8),
                Design.Tokens.Scale(200), Design.Tokens.Scale(30));
            y += SectionLabelH + Design.Tokens.Scale(14);

            if (_tiles.Count > 0)
            {
                // Wraps at three across rather than squeezing six into one row - a 90px tile
                // cannot show a six-colour strip and its own name.
                const int PerRow = 3;
                int tileGap = Design.Tokens.Scale(10);
                int tileW = (innerW - (PerRow - 1) * tileGap) / PerRow;
                int tileH = Design.Tokens.Scale(74);

                for (int i = 0; i < _tiles.Count; i++)
                {
                    int col = i % PerRow, row = i / PerRow;
                    _tiles[i].SetBounds(leftX + col * (tileW + tileGap),
                        y + row * (tileH + tileGap), tileW, tileH);
                }

                int rows = (_tiles.Count + PerRow - 1) / PerRow;
                y += rows * (tileH + tileGap);
            }

            _presetPreview.SetBounds(leftX, y, innerW, Design.Tokens.Scale(96));
            y += Design.Tokens.Scale(96) + SectionGap;

            // ---- scene presets: compact chips, under the controls they drive ----
            _presetsLabel.SetBounds(leftX, y, innerW, SectionLabelH);
            y += SectionLabelH + Design.Tokens.Scale(8);

            int chipW = (innerW - (_chips.Count - 1) * ChipGap) / Math.Max(1, _chips.Count);
            for (int i = 0; i < _chips.Count; i++)
                _chips[i].SetBounds(leftX + i * (chipW + ChipGap), y, chipW, ChipHeight);
            y += ChipHeight + SectionGap;

            // ---- shortcuts ----
            _shortcutsLabel.SetBounds(leftX, y, innerW, SectionLabelH);
            y += SectionLabelH + Design.Tokens.Scale(6);

            int pickerH = Design.Tokens.Scale(HotkeyPicker.PickerDefaultSize.Height);
            _popupHotkeyLabel.SetBounds(leftX, y, colW, Design.Tokens.Scale(18));
            _mainHotkeyLabel.SetBounds(rightX, y, colW, Design.Tokens.Scale(18));
            y += Design.Tokens.Scale(20);
            _hotkeyPicker.SetBounds(leftX, y, colW, pickerH);
            _mainHotkeyPicker.SetBounds(rightX, y, colW, pickerH);
            y += pickerH + SectionGap;

            // ---- share ----
            _shareLabel.SetBounds(leftX, y, innerW, SectionLabelH);
            y += SectionLabelH + Design.Tokens.Scale(4);

            _shareHint.SetBounds(leftX, y, innerW, Design.Tokens.Scale(20));
            y += Design.Tokens.Scale(26);

            int btnH = Design.Tokens.Scale(30);
            int applyW = Design.Tokens.Scale(84);
            int copyW = Design.Tokens.Scale(120);
            int gap = Design.Tokens.Scale(Design.Tokens.S);
            int boxW = innerW - applyW - copyW - 2 * gap;

            // The box is a stock TextBox, which centres its own text vertically inside a
            // height it picks from the font. Matching the buttons' height would leave the
            // caret sitting high, so it keeps its natural height and is centred against them.
            _codeBox.SetBounds(leftX, y + (btnH - _codeBox.Height) / 2, boxW, _codeBox.Height);
            _copyCode.SetBounds(leftX + boxW + gap, y, copyW, btnH);
            _applyCode.SetBounds(leftX + boxW + copyW + 2 * gap, y, applyW, btnH);
            y += btnH + Design.Tokens.Scale(6);

            _shareStatus.SetBounds(leftX, y, innerW, Design.Tokens.Scale(20));
            y += Design.Tokens.Scale(20);

            // The card ends where its contents do, plus the same padding it started with.
            _card.SetBounds(leftMargin, cardTop, cardW, y + CardPad);

            // Scroll extent covers the header band, the card and a margin underneath.
            AutoScrollMinSize = new Size(0, cardTop + _card.Height + PageMargin);
        }

        // ---- behaviour ---------------------------------------------------------------

        private void ApplyPreset(DisplayPreset preset)
        {
            // One flag around the whole set: each slider's ValueChanged would otherwise
            // recompute the highlight against a half-applied preset and clear it.
            // Saturation and vibrance are deliberately untouched: they are the user's own
            // taste, and a preset that reset them would throw that away on every biome change.
            _applyingPreset = true;
            try
            {
                _brightness.Slider.Value = preset.Brightness;
                _gamma.Slider.Value = preset.Gamma;
                _contrast.Slider.Value = preset.Contrast;
                _temperature.Slider.Value = preset.Temperature;
            }
            finally
            {
                _applyingPreset = false;
            }

            RefreshReadouts();
            UpdateActiveChip();
            _store.Save(_settings);
        }

        /// <summary>Put the four tone controls back to neutral, leaving saturation and
        /// vibrance - the user's own look - exactly where they are.</summary>
        private void ResetFineTune()
        {
            _applyingPreset = true;
            try
            {
                _brightness.Slider.Value = 100;
                _gamma.Slider.Value = 100;
                _contrast.Slider.Value = 100;
                _temperature.Slider.Value = 0;
            }
            finally { _applyingPreset = false; }

            RefreshReadouts();
            UpdateActiveChip();
            _store.Save(_settings);
        }

        // ---- share ----------------------------------------------------------------------

        /// <summary>Everything on this page, as one code.</summary>
        private ProfileCode CurrentLook() => new(
            _engine.Vibrance, _engine.Saturation, _engine.Brightness, _engine.Gamma,
            _engine.Contrast, _engine.Temperature);

        private void CopyMyCode()
        {
            var code = ProfileCode.Encode(CurrentLook());
            _codeBox.Text = code;

            // Another process holding the clipboard open makes SetText throw, and that was an
            // unhandled exception on a button click - the crash dialog - for something as
            // ordinary as a clipboard manager being busy. The code is in the box either way,
            // so the user can still copy it by hand.
            try
            {
                Clipboard.SetText(code);
                SetShareStatus(code + "  —  copied, paste it anywhere", ok: true);
            }
            catch
            {
                SetShareStatus("Couldn't reach the clipboard — copy the code above.", ok: false);
            }
        }

        private void ApplyCode()
        {
            if (!ProfileCode.TryDecode(_codeBox.Text, out var incoming))
            {
                // Never half-apply. A wrong character means we don't know what they meant,
                // and guessing lands someone on a stranger's screen.
                SetShareStatus("That code isn't right — check it and try again.", ok: false);
                return;
            }

            // Driven through the sliders rather than straight at the engine, because the
            // sliders are on this page. Setting the engine alone would change the screen and
            // leave every control showing the old numbers - which is what made this feature
            // safe to hide at the bottom of Settings and wrong to move here unchanged.
            _applyingPreset = true;
            try
            {
                _saturation.Slider.Value = incoming.Saturation;
                _vibrance.Slider.Value = incoming.Vibrance;
                _brightness.Slider.Value = incoming.Brightness;
                _gamma.Slider.Value = incoming.Gamma;
                _contrast.Slider.Value = incoming.Contrast;
                _temperature.Slider.Value = incoming.Temperature;
            }
            finally { _applyingPreset = false; }

            RefreshReadouts();
            UpdateActiveChip();
            _store.Save(_settings);

            SetShareStatus("Applied — that's their exact look.", ok: true);
        }

        private void SetShareStatus(string text, bool ok)
        {
            _shareStatus.ForeColor = ok ? Theme.TextDim : Theme.Accent;
            _shareStatus.Text = text;
        }

        private void UpdateActiveChip()
        {
            for (int i = 0; i < _chips.Count; i++)
                _chips[i].Active = _presets[i].Matches(
                    _brightness.Slider.Value, _gamma.Slider.Value,
                    _contrast.Slider.Value, _temperature.Slider.Value);
        }

        /// <summary>Re-read every readout. Only for programmatic changes (a preset, a load,
        /// the tray reset) - a moving slider updates its own row and nothing else.</summary>
        private void RefreshReadouts()
        {
            foreach (var row in new[] { _saturation, _vibrance, _brightness, _gamma, _contrast, _temperature })
                row.SyncValueText();
        }

        /// <summary>A bare signed number means nothing here; the direction is the point.</summary>
        internal static string TemperatureText(int value) => value switch
        {
            0 => "Neutral",
            < 0 => $"Cool {-value}",
            _ => $"Warm {value}",
        };

        public new void Refresh()
        {
            _saturation.Slider.Value = _engine.Saturation;
            _vibrance.Slider.Value = _engine.Vibrance;
            _brightness.Slider.Value = _engine.Brightness;
            _gamma.Slider.Value = _engine.Gamma;
            _contrast.Slider.Value = _engine.Contrast;
            _temperature.Slider.Value = _engine.Temperature;

            _hotkeyPicker.ModifierMask = _settings.HotkeyModifierMask;
            _hotkeyPicker.VirtualKey = _settings.HotkeyVirtualKey;
            _mainHotkeyPicker.ModifierMask = _settings.MainHotkeyModifierMask;
            _mainHotkeyPicker.VirtualKey = _settings.MainHotkeyEnabled ? _settings.MainHotkeyVirtualKey : 0;

            RefreshReadouts();
            UpdateActiveChip();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _saveDebounce.Dispose();
                _presetTips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
