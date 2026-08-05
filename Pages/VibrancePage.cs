using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

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

        private readonly AdvancedColorSection _advanced;

        // ---- share ----
        private readonly Label _shareLabel, _shareHint, _shareStatus;
        private readonly TextBox _codeBox;
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
                palette: SliderPalette.Luminance, format: v => $"{v / 100f:0.00}");
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

            // ---- advanced colour ----
            //
            // Collapsed by default, so the page people already use looks exactly as it did.
            // Everything in it resolves to the display gamma ramp, so the section asks the
            // machine probe whether that ramp works before offering the controls.
            _advanced = new AdvancedColorSection(_card,
                new Font(Theme.FontFamily, 7.5f, FontStyle.Bold), Design.Fonts.Caption);
            _advanced.Tone = _settings.ResolvedTone;
            _advanced.ExpandedChanged += (_, _) => LayoutContent();
            _advanced.ToneChanged += (_, _) => OnAdvancedChanged();

            // ---- share ----
            //
            // Lives here rather than at the bottom of Settings, where it used to be. It
            // describes the sliders on this page, and nobody was finding it three cards down
            // a different tab - which matters, because a code passed between friends is how
            // the app spreads.
            _shareLabel = SectionLabel("SHARE");
            _shareHint = CardLabel("Paste a friend's code to get their exact look, or copy yours to send.");

            _codeBox = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Background,
                ForeColor = Theme.Text,
                Font = new Font("Consolas", 10f),
                CharacterCasing = CharacterCasing.Upper,
                // An empty monospace box next to a button called Apply says nothing about
                // what belongs in it.
                PlaceholderText = "PX-XXXXXXXXX",
            };
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
        private SliderRow Row(string caption, int min, int max, int? notch, int value,
            Action<int> apply, bool large = false, SliderPalette? palette = null,
            Func<int, string>? format = null)
        {
            var row = new SliderRow(_card, caption, min, max, notch, value, large, palette, format);

            row.Slider.ValueChanged += (_, _) =>
            {
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
            int available = Width - 2 * PageMargin - SystemInformation.VerticalScrollBarWidth;
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
            _primaryLabel.SetBounds(leftX, y, innerW, SectionLabelH);
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

            // ---- advanced colour, directly under the controls it extends ----
            _advanced.Place(leftX, y, innerW, colW, ColGap);
            y += _advanced.PreferredHeight + SectionGap;

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

        /// <summary>
        /// Push the advanced grade at the engine, merging in the gamma slider.
        ///
        /// Gamma lives in FINE TUNE rather than in the advanced section, because it predates
        /// it and is on every saved settings file and every share code. It is still part of
        /// the same curve, so the two are recombined here - one place, so they cannot drift.
        /// </summary>
        private void OnAdvancedChanged()
        {
            var tone = _advanced.Tone with { Gamma = _gamma.Slider.Value };

            _engine.Tone = tone;
            _settings.Tone = tone;
            _saveDebounce.Trigger();
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
