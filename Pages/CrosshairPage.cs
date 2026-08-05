using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Crosshair;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Build a crosshair, see it live, save it under a name. Deliberately small: shape,
    /// colour, and three sliders cover what people actually change.
    /// </summary>
    public sealed class CrosshairPage : GlowPage
    {
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly CrosshairService _service;

        private readonly PreviewBox _preview;
        private readonly CardPanel _card;
        private readonly FlowLayoutPanel _savedFlow;
        private readonly Label _savedEmpty;
        private readonly ToggleSwitch _enabled;
        private readonly Label _status;
        private readonly List<ChipButton> _shapes = new();

        // Held so a preset - or loading a saved crosshair - can drive them. Without this the
        // config changes and every readout keeps showing the old number, because each slider
        // captured its starting value when it was built.
        private FlatSlider _sizeSlider = null!, _thicknessSlider = null!, _gapSlider = null!;

        /// <summary>Suppresses the per-slider save while several are being set at once.</summary>
        private bool _applyingPreset;
        private readonly List<SwatchDot> _colourDots = new();

        private const int BaseCardHeight = 630;
        private const int CardW = 620;
        private const int Gutter = 18;
        private const int ContentW = CardW - 2 * Gutter;
        /// <summary>X for a control pinned to the card's right gutter.</summary>
        private static int RightOf(int width) => CardW - Gutter - width;

        private CrosshairConfig _current;

        public CrosshairPage(AppSettings settings, SettingsStore store, CrosshairService service)
        {
            _settings = settings;
            _store = store;
            _service = service;
            _current = service.Config.Clone();

            Font = new Font(Theme.FontFamily, 9.5f);
            AutoScroll = true;

            // Tall enough for every row including SAVED at the bottom (y=300 sliders start,
            // combo/buttons land around y=572-600): a shorter card clips its own last row
            // regardless of the page's AutoScroll, since a child can't render past its
            // immediate parent's bounds.
            _card = new CardPanel { Location = new Point(40, 34), Size = new Size(CardW, BaseCardHeight) };
            _card.Controls.Add(UiHelpers.Caption("CROSSHAIR", Gutter, 16, 240));

            _enabled = new ToggleSwitch { Location = new Point(RightOf(44), 14), Checked = _service.IsVisible };
            _enabled.CheckedChanged += (s, e) =>
            {
                if (_enabled.Checked) _service.Show(); else _service.Hide();
                _settings.CrosshairEnabled = _enabled.Checked;
                _store.Save(_settings);
                UpdateStatus();
            };
            _card.Controls.Add(_enabled);

            _preview = new PreviewBox { Location = new Point(Gutter, 44), Size = new Size(ContentW, 150) };
            _card.Controls.Add(_preview);

            _status = new Label
            {
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(Gutter, 200),
                // 26, not 18: the exclusive-fullscreen message measured within a few pixels
                // of the column width, so on any machine whose font came out slightly wider
                // it wrapped to a second line that the 18px box then cut off.
                Size = new Size(ContentW, 26)
            };
            _card.Controls.Add(_status);

            // ---- Presets ----
            //
            // Replaces the old four shape chips. Every shape is still reachable - the presets
            // between them cover Cross, T, Dot and Circle - but each one also brings sensible
            // dimensions, so picking a starting point is one click instead of a shape plus
            // three sliders. Applying one deliberately keeps the user's colour.
            _card.Controls.Add(UiHelpers.Caption("PRESETS", 18, 228, 200));

            const int ChipW = 140, ChipH = 34, ChipGapX = 8, ChipGapY = 8, PerRow = 4;
            int presetTop = 250;

            for (int i = 0; i < CrosshairPresets.All.Count; i++)
            {
                var preset = CrosshairPresets.All[i];
                var chip = new ChipButton
                {
                    Text = preset.Name,
                    Location = new Point(
                        18 + (i % PerRow) * (ChipW + ChipGapX),
                        presetTop + (i / PerRow) * (ChipH + ChipGapY)),
                    Size = new Size(ChipW, ChipH),
                    Font = new Font(Theme.FontFamily, 9f),
                };
                chip.Click += (s, e) => ApplyPreset(preset);
                _shapes.Add(chip);
                _card.Controls.Add(chip);
            }

            HighlightActivePreset();

            // ---- Sliders ----
            // Two rows of preset chips sit above, so everything below starts lower than it
            // did when this was a single row of four.
            int y = presetTop + 2 * ChipH + ChipGapY + 16;
            // Ranges are in tenths: 0.5-30.0 size, 0.5-10.0 thickness, 0-30.0 gap. Same
            // limits as before, ten times the resolution inside them.
            _sizeSlider = AddSlider(_card, "SIZE", y, 5, 300,
                (int)Math.Round(_current.ResolvedSize * 10),
                v => { _current.SetSizeTenths(v); OnSliderMoved(); });
            _thicknessSlider = AddSlider(_card, "THICKNESS", y + 62, 5, 100,
                (int)Math.Round(_current.ResolvedThickness * 10),
                v => { _current.SetThicknessTenths(v); OnSliderMoved(); });
            _gapSlider = AddSlider(_card, "GAP", y + 124, 0, 300,
                (int)Math.Round(_current.ResolvedGap * 10),
                v => { _current.SetGapTenths(v); OnSliderMoved(); });

            // ---- Colour + options ----
            _card.Controls.Add(UiHelpers.Caption("COLOUR", 18, y + 186, 200));
            int cx = Gutter;
            foreach (var colour in new[]
            {
                Color.FromArgb(255, 0, 255, 102), Color.FromArgb(255, 0, 255, 255),
                Color.FromArgb(255, 255, 0, 170), Color.FromArgb(255, 255, 60, 60),
                Color.FromArgb(255, 255, 220, 0), Color.White
            })
            {
                var dot = new SwatchDot(colour)
                {
                    Location = new Point(cx, y + 208),
                    Active = colour.ToArgb() == _current.ColourArgb,
                };
                var captured = colour;
                dot.Click += (s, e) =>
                {
                    _current.ColourArgb = captured.ToArgb();
                    // Nothing used to mark the chosen colour, so clicking a dot changed the
                    // crosshair and left the row looking exactly as it did before.
                    foreach (var d in _colourDots) d.Active = ReferenceEquals(d, dot);
                    Push();
                };
                _colourDots.Add(dot);
                _card.Controls.Add(dot);
                cx += 44;
            }

            var outline = new ToggleSwitch { Location = new Point(RightOf(44), y + 206), Checked = _current.Outline };
            outline.CheckedChanged += (s, e) => { _current.Outline = outline.Checked; Push(); };
            // Pinned to its own switch rather than floating at a fixed x in the dead space
            // after the colour dots, which read as an unrelated stray word.
            _card.Controls.Add(new Label
            {
                Text = "Outline",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(RightOf(44) - 12 - 100, y + 206),
                Size = new Size(100, 22),
                TextAlign = ContentAlignment.MiddleRight
            });
            _card.Controls.Add(outline);

            // ---- Saved configs ----
            _card.Controls.Add(UiHelpers.Caption("SAVED", 18, y + 250, 200));

            var saveBtn = Button("Save as…", 472, y + 272, 130);
            saveBtn.Click += (s, e) => SaveCurrent();
            _card.Controls.Add(saveBtn);

            // Fixed width so WrapContents actually wraps; height grows with the row count
            // (RefreshSavedList grows the card to match, same trick already used for the
            // card's own AutoScrollMinSize - see the comment at the top of this file about
            // a child positioned outside its parent's bounds being invisible regardless of
            // the page's own scroll setting).
            _savedFlow = new FlowLayoutPanel
            {
                Location = new Point(18, y + 272),
                Size = new Size(444, 36),
                MaximumSize = new Size(444, 0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = Theme.Surface
            };
            _card.Controls.Add(_savedFlow);

            _savedEmpty = new Label
            {
                Text = "No saved crosshairs yet — tweak, then Save as…",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(18, y + 272),
                Size = new Size(444, 36),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _card.Controls.Add(_savedEmpty);

            Controls.Add(_card);

            RefreshSavedList();
            Push();
        }

        /// <summary>
        /// One crosshair slider, working in TENTHS of a pixel.
        ///
        /// Whole pixels were too coarse to aim with: at the sizes people actually use, one
        /// step of thickness is the difference between a usable crosshair and an unusable
        /// one, and there was nothing between 2 and 3. The slider is still an integer control
        /// - it just counts tenths - so the value shown is divided by ten and the callback
        /// receives tenths.
        /// </summary>
        private FlatSlider AddSlider(Control parent, string label, int y, int minTenths, int maxTenths,
            int valueTenths, Action<int> onChangeTenths)
        {
            parent.Controls.Add(UiHelpers.Caption(label, 18, y, 200));

            static string Format(int tenths) => (tenths / 10f).ToString("0.0");

            var readout = new Label
            {
                Text = Format(valueTenths),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = new Font(Theme.FontFamily, 8.5f),
                Location = new Point(RightOf(42), y),
                Size = new Size(42, 16),
                TextAlign = ContentAlignment.MiddleRight
            };
            parent.Controls.Add(readout);

            var slider = new FlatSlider
            {
                Minimum = minTenths,
                Maximum = maxTenths,
                Value = Math.Clamp(valueTenths, minTenths, maxTenths)
            };
            slider.SetTrackBounds(Gutter, y + 20, ContentW);
            slider.ValueChanged += (s, e) =>
            {
                readout.Text = Format(slider.Value);
                onChangeTenths(slider.Value);
            };
            parent.Controls.Add(slider);
            return slider;
        }

        /// <summary>
        /// Apply a preset, then rebuild the page so the sliders show what it set.
        ///
        /// The whole card is rebuilt rather than each slider being poked individually: the
        /// sliders were created with their starting values captured in closures, so setting
        /// the config alone would change the crosshair and leave every readout showing the
        /// old numbers - the same trap the share-code button had on the Display page.
        /// </summary>
        private void ApplyPreset(CrosshairPreset preset)
        {
            CrosshairPresets.Apply(_current, preset);

            // Drive the sliders so their readouts follow. Guarded, because each one fires
            // ValueChanged and would otherwise apply and save three times for one click.
            _applyingPreset = true;
            try
            {
                _sizeSlider.Value = Math.Clamp(preset.SizeTenths, _sizeSlider.Minimum, _sizeSlider.Maximum);
                _thicknessSlider.Value = Math.Clamp(preset.ThicknessTenths, _thicknessSlider.Minimum, _thicknessSlider.Maximum);
                _gapSlider.Value = Math.Clamp(preset.GapTenths, _gapSlider.Minimum, _gapSlider.Maximum);
            }
            finally { _applyingPreset = false; }

            // Re-apply the preset's own values afterwards: the sliders clamp to their track
            // range, and writing them back would otherwise let a clamp silently rewrite the
            // preset the user just picked.
            CrosshairPresets.Apply(_current, preset);

            Push();
            HighlightActivePreset();
        }

        /// <summary>A slider the user moved. Applies immediately, and drops the preset
        /// highlight once the crosshair no longer matches one.</summary>
        private void OnSliderMoved()
        {
            if (_applyingPreset) return;
            Push();
            HighlightActivePreset();
        }

        /// <summary>Mark whichever preset the current crosshair matches, or none once the
        /// user has moved a slider off all of them.</summary>
        private void HighlightActivePreset()
        {
            var active = CrosshairPresets.Matching(_current);
            foreach (var chip in _shapes)
                chip.Active = active != null && chip.Text == active.Name;
        }

        /// <summary>Push the edited config to the preview and the live overlay together, so
        /// what's on screen always matches what's in the card.</summary>
        private void Push()
        {
            _preview.Config = _current;
            _preview.Invalidate();
            _service.Apply(_current.Clone());
            _settings.ActiveCrosshair = _current.Clone();
            _store.Save(_settings);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (!_enabled.Checked)
            {
                _status.Text = "Crosshair is off.";
                _status.ForeColor = Theme.TextDim;
                return;
            }

            if (CrosshairService.IsExclusiveFullscreen())
            {
                _status.Text = "A game is in exclusive fullscreen - no overlay can draw there. "
                             + "Switch it to borderless.";
                _status.ForeColor = Theme.Accent;
            }
            else
            {
                _status.Text = "Crosshair is showing, centred on your screen.";
                _status.ForeColor = Theme.TextDim;
            }
        }

        /// <summary>Rebuilds the chip list from <c>_settings.SavedCrosshairs</c> and grows the
        /// card to fit however many rows that wraps to (a fixed card height would clip chips
        /// past its bottom edge regardless of the page's own AutoScroll - see the comment
        /// on the card's construction above).</summary>
        private void RefreshSavedList()
        {
            _savedFlow.SuspendLayout();
            _savedFlow.Controls.Clear();
            foreach (var saved in _settings.SavedCrosshairs)
            {
                var chip = new SavedChip(saved)
                {
                    Active = saved.Name == _current.Name,
                    Margin = new Padding(0, 0, 8, 8)
                };
                chip.LoadRequested += (s, e) => LoadSaved(saved.Name);
                chip.DeleteRequested += (s, e) => DeleteSaved(saved.Name);
                _savedFlow.Controls.Add(chip);
            }
            _savedFlow.ResumeLayout(true);

            bool any = _settings.SavedCrosshairs.Count > 0;
            _savedFlow.Visible = any;
            _savedEmpty.Visible = !any;

            // Set explicitly rather than relying on WinForms to infer it from children - a
            // UserControl doesn't always recompute this reliably on its own.
            _card.Height = Math.Max(BaseCardHeight, _savedFlow.Bottom + 20);
            AutoScrollMinSize = new Size(0, _card.Bottom + 20);
        }

        private void LoadSaved(string name)
        {
            var found = _settings.SavedCrosshairs.FirstOrDefault(c => c.Name == name);
            if (found == null) return;
            _current = found.Clone();

            // Move the sliders to the loaded crosshair, not just the config behind them.
            _applyingPreset = true;
            try
            {
                _sizeSlider.Value = Math.Clamp((int)Math.Round(_current.ResolvedSize * 10),
                    _sizeSlider.Minimum, _sizeSlider.Maximum);
                _thicknessSlider.Value = Math.Clamp((int)Math.Round(_current.ResolvedThickness * 10),
                    _thicknessSlider.Minimum, _thicknessSlider.Maximum);
                _gapSlider.Value = Math.Clamp((int)Math.Round(_current.ResolvedGap * 10),
                    _gapSlider.Minimum, _gapSlider.Maximum);
            }
            finally { _applyingPreset = false; }

            HighlightActivePreset();
            SyncColourDots();
            Push();
            RefreshSavedList();
        }

        private void SaveCurrent()
        {
            string name = Prompt.Ask("Save crosshair as", _current.Name);
            if (string.IsNullOrWhiteSpace(name)) return;

            _current.Name = name.Trim();
            _settings.SavedCrosshairs.RemoveAll(c => c.Name == _current.Name);
            _settings.SavedCrosshairs.Add(_current.Clone());
            _store.Save(_settings);
            RefreshSavedList();
        }

        /// <summary>Deletes immediately, no confirmation - matches the approved chip design.
        /// If the deleted config was the active one, the on-screen config is left as-is and
        /// only the highlight disappears (there's nothing left to highlight).</summary>
        private void DeleteSaved(string name)
        {
            _settings.SavedCrosshairs.RemoveAll(c => c.Name == name);
            _store.Save(_settings);
            RefreshSavedList();
        }

        /// <summary>Mark whichever preset colour matches the live config. A loaded or
        /// hand-picked colour that isn't one of the six presets simply leaves them all
        /// unringed, which is honest - none of them is the current colour.</summary>
        private void SyncColourDots()
        {
            foreach (var d in _colourDots) d.Active = d.Colour.ToArgb() == _current.ColourArgb;
        }

        private static Button Button(string text, int x, int y, int w)
        {
            // Same styling as the rest of the app's secondary buttons, hover included -
            // this one used to skip MouseOverBackColor and flash the Windows default blue.
            var b = SettingsPage.FlatButton(text, x, y, w);
            b.Height = 28;
            return b;
        }

        /// <summary>Draws the crosshair over a checkerboard, so light and dark colours can
        /// both be judged before taking it into a game.</summary>
        private sealed class PreviewBox : Panel
        {
            public CrosshairConfig Config { get; set; } = new();

            public PreviewBox()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                       | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                CrosshairRender.DrawCheckerboard(g, new Rectangle(0, 0, Width, Height), 16);

                var state = g.Save();
                g.TranslateTransform(Width / 2f, Height / 2f);
                CrosshairRender.Draw(g, Config);
                g.Restore(state);
                base.OnPaint(e);
            }
        }

        private sealed class SwatchDot : Control
        {
            private readonly Color _colour;
            private bool _active;
            private bool _hover;

            public Color Colour => _colour;

            /// <summary>The currently-chosen colour, ringed the same way the theme swatches
            /// in Settings are.</summary>
            public bool Active
            {
                get => _active;
                set { if (_active == value) return; _active = value; Invalidate(); }
            }

            public SwatchDot(Color colour)
            {
                // SupportsTransparentBackColor must be enabled BEFORE assigning a
                // transparent BackColor - a plain Control rejects it otherwise.
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor
                       | ControlStyles.ResizeRedraw, true);
                _colour = colour;
                Size = new Size(30, 30);
                Cursor = Cursors.Hand;
                BackColor = Color.Transparent;
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Same geometry as the theme swatches in Settings, so "this one is chosen"
                // looks the same wherever the app asks you to pick a colour.
                if (_active)
                    using (var ring = new Pen(Theme.Text, 2f))
                        g.DrawEllipse(ring, 1, 1, Width - 3, Height - 3);

                var disc = new Rectangle(4, 4, Width - 9, Height - 9);
                using (var b = new SolidBrush(_colour))
                    g.FillEllipse(b, disc);
                using (var p = new Pen(Color.FromArgb(_hover ? 220 : 120, 255, 255, 255), 1f))
                    g.DrawEllipse(p, disc);
            }
        }

        /// <summary>One saved crosshair, shown as a pill: mini preview, name, and a "×" that
        /// deletes it. Sits inside the (opaque, Theme.Surface) saved-list FlowLayoutPanel and
        /// uses the same transparent-control trick as SwatchDot/PreviewBox - one level of it
        /// against an opaque parent is the pattern proven safe elsewhere in this app; the
        /// FlowLayoutPanel is deliberately NOT transparent itself, since stacking two levels
        /// of the trick is what caused the ghosting bug on the Games Hub (see HANDOFF.md).</summary>
        private sealed class SavedChip : Control
        {
            private const int PreviewSize = 22;
            private const int DeleteWidth = 24;

            private readonly CrosshairConfig _config;
            private bool _hover;
            private bool _active;

            public bool Active
            {
                get => _active;
                set { if (_active == value) return; _active = value; Invalidate(); }
            }

            public event EventHandler? LoadRequested;
            public event EventHandler? DeleteRequested;

            public SavedChip(CrosshairConfig config)
            {
                _config = config;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor
                       | ControlStyles.ResizeRedraw, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
                Font = new Font(Theme.FontFamily, 9f);
                Size = new Size(190, 36);
            }

            private Rectangle DeleteRect => new(Width - DeleteWidth, 0, DeleteWidth, Height);

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hover = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hover = false;
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left) return;
                if (DeleteRect.Contains(e.Location)) DeleteRequested?.Invoke(this, EventArgs.Empty);
                else LoadRequested?.Invoke(this, EventArgs.Empty);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
                using (var path = Glass.RoundedPath(rect, (Height - 1) / 2f))
                {
                    using (var fill = new SolidBrush(_hover ? Theme.SurfaceHover : Theme.Surface))
                        g.FillPath(fill, path);
                    using (var border = new Pen(_active ? Theme.Accent : Theme.Border, _active ? 1.6f : 1f))
                        g.DrawPath(border, path);
                }

                var previewRect = new Rectangle(7, (Height - PreviewSize) / 2, PreviewSize, PreviewSize);
                CrosshairRender.DrawCheckerboard(g, previewRect, 6);
                CrosshairRender.Draw(g, _config, previewRect);

                var textRect = new Rectangle(previewRect.Right + 8, 0,
                    Width - previewRect.Right - 8 - DeleteWidth, Height);
                TextRenderer.DrawText(g, _config.Name, Font, textRect, Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                TextRenderer.DrawText(g, "×", Font, DeleteRect, Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>Minimal name prompt - WinForms has no built-in one.</summary>
        private static class Prompt
        {
            public static string Ask(string title, string initial)
            {
                using var form = new Form
                {
                    Text = title,
                    ClientSize = new Size(344, 116),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterParent,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    // A one-field prompt has no business claiming its own taskbar button.
                    ShowInTaskbar = false,
                    Icon = AppIcon.Value,
                    BackColor = Theme.Background,
                    ForeColor = Theme.Text,
                    Font = new Font(Theme.FontFamily, 9.5f)
                };
                var box = new TextBox
                {
                    Text = initial,
                    Location = new Point(18, 20),
                    Width = 308,
                    BackColor = Theme.Surface,
                    ForeColor = Theme.Text,
                    BorderStyle = BorderStyle.FixedSingle
                };
                var ok = SettingsPage.PrimaryButton("Save", 230, 62, 96, height: 30);
                ok.DialogResult = DialogResult.OK;
                // There was no way out of this dialog except the title bar's X - Escape did
                // nothing, because a FixedDialog without a CancelButton swallows it.
                var cancel = SettingsPage.FlatButton("Cancel", 126, 62, 96);
                cancel.Height = 30;
                cancel.DialogResult = DialogResult.Cancel;

                form.Controls.Add(box);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? box.Text : "";
            }
        }
    }
}
